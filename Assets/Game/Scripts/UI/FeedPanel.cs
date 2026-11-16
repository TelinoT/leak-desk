using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Logs app. Left: delivered documents + a search box over every log. Right: the selected document rendered like
/// its real app (Slack chat, email, console log, news article). Any line can be flagged into the evidence tray.
/// </summary>
public class FeedPanel
{
    static readonly HashSet<string> ConsoleSources = new() { "badge", "vpn", "transfer", "audit", "facilities", "ticket" };
    static readonly Regex ChatLine = new(@"^(\d{1,2}:\d{2})\s+([^:]{1,24}):\s*(.*)$");
    static readonly Regex TimePrefix = new(@"^(\d{1,2}:\d{2})\s+(.*)$");

    readonly DeskUI ui;
    readonly GameObject root;
    readonly RectTransform listContent;
    readonly RectTransform linesContent;
    readonly TextMeshProUGUI docTitle, docMeta, docTag;
    readonly Image docTagBg, docTagIcon, linesBg;
    readonly Button summarizeButton;
    readonly TMP_InputField search;
    readonly Dictionary<string, Image> docCards = new();
    readonly HashSet<string> read = new();
    readonly TextMeshProUGUI incoming;
    CaseDocument current;
    string query = "";
    string justFlagged; // "docId:index" of the line to play the highlighter swipe on

    public int UnreadCount
    {
        get
        {
            int n = 0;
            foreach (var id in docCards.Keys) if (!read.Contains(id)) n++;
            return n;
        }
    }

    UiTheme T => UiKit.Theme;
    LeakDeskGame Game => ui.Game;

    public FeedPanel(DeskUI ui, Transform parent)
    {
        this.ui = ui;
        var rt = UiKit.Rect("FeedPanel", parent).Stretch();
        root = rt.gameObject;
        rt.HLayout(16);
        rt.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;

        // ---------- document list ----------
        var listPanel = UiKit.Panel(rt, T.panel, T.panelSprite, "DocList");
        listPanel.Size(prefW: 380, flexH: 1);
        listPanel.VLayout(10, 16);
        UiKit.Heading(listPanel.transform, "Overnight logs", 28);

        var searchRow = UiKit.Rect("SearchRow", listPanel.transform);
        searchRow.HLayout(8);
        search = UiKit.Input(searchRow, "Search all logs... e.g. 02:11", height: 50);
        search.onValueChanged.AddListener(OnSearch);
        var clear = UiKit.Button(searchRow, "", () => search.text = "", T.panelAlt, T.text, 50);
        clear.Size(prefW: 50, prefH: 50);
        var ci = UiKit.Icon(clear.transform, "magnifier", 24, T.text);
        var cir = (RectTransform)ci.transform;
        cir.anchorMin = cir.anchorMax = new Vector2(0.5f, 0.55f);
        cir.sizeDelta = new Vector2(24, 24);

        var scroll = UiKit.ScrollList(listPanel.transform, out listContent, 8, 2);
        scroll.Size(flexH: 1);
        incoming = UiKit.Text(listPanel.transform, "", T.smallSize, T.warning, TextAlignmentOptions.Center);
        incoming.Size(prefH: 46);

        foreach (var doc in Game.Case.Documents)
            if (Game.IsDocumentUnlocked(doc.Id)) AddDocCard(doc);
        Game.WaveUnlocked += OnWave;

        // ---------- viewer ----------
        var viewer = UiKit.Panel(rt, T.panel, T.panelSprite, "DocViewer");
        viewer.Size(flexW: 1, flexH: 1);
        viewer.VLayout(14, 22);

        var header = UiKit.Rect("Header", viewer.transform);
        header.HLayout(14);
        docTagBg = UiKit.Chip(header, Color.white, 150, 36, "Tag");
        docTagBg.GetComponent<HorizontalLayoutGroup>().spacing = 6;
        docTagIcon = UiKit.Icon(docTagBg.transform, "paper_1", 20, T.cardText);
        docTag = UiKit.Text(docTagBg.transform, "", T.smallSize - 1, T.cardText, TextAlignmentOptions.Center, FontStyles.Bold);
        var titles = UiKit.Rect("Titles", header);
        titles.VLayout(2);
        titles.Size(flexW: 1);
        docTitle = UiKit.Heading(titles, "", 32);
        docMeta = UiKit.Text(titles, "", T.smallSize, T.muted);
        summarizeButton = UiKit.Button(header, "Summarize with Jensenbot", Summarize, T.secondary, Color.white, 52, 18);
        summarizeButton.Size(prefW: 300, prefH: 52);

        linesBg = UiKit.Panel(viewer.transform, new Color(0, 0, 0, 0.18f), T.panelSprite, "LinesBg");
        linesBg.Size(flexH: 1);
        var linesScroll = UiKit.ScrollList(linesBg.transform, out linesContent, 8, 14);
        ((RectTransform)linesScroll.transform).Stretch();

        Game.Evidence.Added += _ => Refresh();
        Select(Game.Case.Documents[0]);
    }

    public void SetVisible(bool v) => root.SetActive(v);

    void OnWave(CaseWave wave)
    {
        Sfx.Play("unlock");
        // Newest deliveries go on top so they are seen without scrolling.
        for (int i = wave.Documents.Count - 1; i >= 0; i--)
        {
            var doc = Game.Case.Documents.Find(d => d.Id == wave.Documents[i]);
            if (doc == null || docCards.ContainsKey(doc.Id)) continue;
            var card = AddDocCard(doc);
            card.transform.SetSiblingIndex(0);
            UiFx.Punch(card.transform, 0.08f, 0.4f);
        }
        if (query.Length >= 2) Refresh();
    }

    /// <summary>Called every frame by DeskUI.</summary>
    public void Tick()
    {
        float next = Game.NextWaveIn;
        incoming.text = next < 0 ? "All logs received."
            : Game.Phase == GamePhase.Briefing ? ""
            : $"Next delivery in ~{Mathf.CeilToInt(next / 60f)} min\n<size=80%><color=#{UiKit.Hex(T.muted)}>(sooner if you find good leads)</color></size>";
    }

    Image AddDocCard(CaseDocument doc)
    {
        var card = UiKit.Panel(listContent, T.panelAlt, T.cardSprite, "Doc: " + doc.Id);
        card.HLayout(12, 12);
        var btn = card.gameObject.AddComponent<Button>();
        btn.onClick.AddListener(() => { Sfx.Play("click"); search.text = ""; Select(doc); });
        card.gameObject.AddComponent<ButtonPop>();

        var iconBg = UiKit.Panel(card.transform, SourceStyle.Color(doc.Source), UiKit.AvatarSprite, "IconBg");
        iconBg.Size(prefW: 46, prefH: 46, minW: 46);
        var ib = UiKit.Rect("Holder", iconBg.transform).Stretch(11, 11, 11, 11);
        ((RectTransform)UiKit.Icon(ib, SourceStyle.Icon(doc.Source), 24, T.cardText).transform).Stretch();

        var col = UiKit.Rect("Col", card.transform);
        col.VLayout(2);
        col.Size(flexW: 1);
        var t = UiKit.Text(col, $"<b>{doc.Title}</b>", T.bodySize - 2, T.text);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        var meta = UiKit.Text(col, $"{SourceStyle.Label(doc.Source)}  ·  {doc.Time}", T.smallSize - 2, T.muted);
        meta.textWrappingMode = TextWrappingModes.NoWrap;
        meta.overflowMode = TextOverflowModes.Ellipsis;

        var unread = UiKit.Panel(card.transform, T.accent, UiKit.AvatarSprite, "Unread");
        unread.Size(prefW: 14, prefH: 14, minW: 14);
        docCards[doc.Id] = card;
        return card;
    }

    public void Select(CaseDocument doc)
    {
        current = doc;
        read.Add(doc.Id);
        foreach (var kv in docCards)
        {
            bool on = kv.Key == doc.Id && query.Length < 2;
            kv.Value.color = on ? Color.Lerp(T.panelAlt, T.secondary, 0.4f) : T.panelAlt;
            var dot = kv.Value.transform.Find("Unread");
            if (dot != null) dot.gameObject.SetActive(!read.Contains(kv.Key));
        }
        Refresh();
    }

    void OnSearch(string q)
    {
        query = (q ?? "").Trim();
        Refresh();
    }

    void Refresh()
    {
        if (query.Length >= 2) ShowSearchResults();
        else ShowDocument();
    }

    // ---------- document view ----------

    void ShowDocument()
    {
        if (current == null) return;
        var doc = current;
        docTag.text = SourceStyle.Label(doc.Source);
        docTagBg.color = SourceStyle.Color(doc.Source);
        docTagIcon.sprite = T.Icon(SourceStyle.Icon(doc.Source));
        docTitle.text = doc.Title;
        docMeta.text = $"From {doc.From}  ·  {doc.Time}  ·  {doc.Lines.Count} lines";
        summarizeButton.gameObject.SetActive(true);
        bool console = ConsoleSources.Contains(doc.Source);
        linesBg.color = console ? new Color(0.05f, 0.04f, 0.11f, 0.9f) : new Color(0, 0, 0, 0.18f);

        UiKit.Clear(linesContent);
        if (doc.Source == "email") EmailHeader(doc);
        for (int i = 0; i < doc.Lines.Count; i++) AddLine(doc, i, console, showDoc: false);
        PlayFlagSwipe();
    }

    void EmailHeader(CaseDocument doc)
    {
        var card = UiKit.Card(linesContent, new Color(1, 1, 1, 0.95f), 16, 4, "MailHeader");
        UiKit.Text(card.transform, $"<b>{doc.Title}</b>", T.bodySize + 2, T.cardText);
        UiKit.Text(card.transform, $"From: <b>{doc.From}</b>    Sent: {doc.Time}", T.smallSize, new Color(T.cardText.r, T.cardText.g, T.cardText.b, 0.7f));
    }

    void AddLine(CaseDocument doc, int i, bool console, bool showDoc)
    {
        var line = doc.Lines[i];
        bool flagged = Game.Evidence.IsLineFlagged(doc, i);
        string text = Highlight(line.Text);

        Color bg = console ? new Color(1, 1, 1, flagged ? 0.10f : 0.03f) : flagged ? new Color(1f, 0.95f, 0.76f) : T.card;
        var row = UiKit.Panel(linesContent, bg, console ? T.bubbleSprite : T.cardSprite, $"Line {doc.Id}:{i}");
        row.HLayout(14, 12);

        // Highlighter marker behind flagged lines (animated on the line just flagged).
        if (flagged)
        {
            var marker = UiKit.Panel(row.transform, new Color(1f, 0.82f, 0.2f, console ? 0.9f : 1f), T.pillSprite, "Marker").Pill(8);
            marker.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var mr = (RectTransform)marker.transform;
            mr.anchorMin = new Vector2(0, 0);
            mr.anchorMax = new Vector2(0, 1);
            mr.pivot = new Vector2(0, 0.5f);
            mr.sizeDelta = new Vector2(8, -16);
            mr.anchoredPosition = new Vector2(4, 0);
        }

        var col = UiKit.Rect("Col", row.transform);
        col.VLayout(2);
        col.Size(flexW: 1);
        if (showDoc)
        {
            UiKit.Text(col, $"<b>{SourceStyle.Label(doc.Source)}</b>  ·  {doc.Title}", T.smallSize - 2,
                console ? new Color(0.7f, 0.68f, 0.9f) : new Color(T.cardText.r, T.cardText.g, T.cardText.b, 0.6f));
        }

        var chat = ChatLine.Match(line.Text);
        if (doc.Source == "slack" && chat.Success)
        {
            var r = UiKit.Rect("Chat", col);
            r.HLayout(10, 0, TextAnchor.UpperLeft);
            string who = chat.Groups[2].Value.Trim();
            var av = UiKit.Panel(r, NameColor(who), UiKit.AvatarSprite, "Avatar");
            av.Size(prefW: 40, prefH: 40, minW: 40);
            var ini = UiKit.Text(av.transform, who.Substring(0, 1), 20, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            ((RectTransform)ini.transform).Stretch();
            var c2 = UiKit.Rect("Msg", r);
            c2.VLayout(0);
            c2.Size(flexW: 1);
            UiKit.Text(c2, $"<b><color=#{UiKit.Hex(Color.Lerp(NameColor(who), Color.black, 0.35f))}>{who}</color></b>  <size=80%><color=#999999>{chat.Groups[1].Value}</color></size>",
                T.smallSize + 1, T.cardText);
            UiKit.Text(c2, Highlight(chat.Groups[3].Value), T.bodySize - 1, T.cardText);
        }
        else if (console)
        {
            var tm = TimePrefix.Match(line.Text);
            string body = tm.Success
                ? $"<color=#FFC24A>{tm.Groups[1].Value}</color>  {Highlight(tm.Groups[2].Value)}"
                : text;
            var t = UiKit.Text(col, $"<mspace=0.56em>{body}</mspace>", T.bodySize - 3, new Color(0.62f, 1f, 0.72f));
            t.textWrappingMode = TextWrappingModes.Normal;
        }
        else if (doc.Source == "news" && i == 0)
        {
            UiKit.Heading(col, text, 30, T.cardText);
        }
        else
        {
            UiKit.Text(col, text, T.bodySize, T.cardText);
        }

        int index = i;
        var flag = UiKit.Button(row.transform, flagged ? "Flagged" : "Flag", () => Flag(doc, index),
            flagged ? new Color(0.75f, 0.75f, 0.8f) : T.warning, T.cardText, 44, 18);
        flag.Size(prefW: 110, prefH: 44);
        flag.interactable = !flagged && Game.Phase is GamePhase.Investigating or GamePhase.Briefing;

        if (showDoc)
        {
            var open = row.gameObject.AddComponent<Button>();
            open.transition = Selectable.Transition.None;
            open.onClick.AddListener(() => { search.text = ""; Select(doc); });
        }
    }

    // ---------- search ----------

    void ShowSearchResults()
    {
        docTag.text = "SEARCH";
        docTagBg.color = T.secondary;
        docTagIcon.sprite = T.Icon("magnifier");
        docTitle.text = $"\"{query}\"";
        summarizeButton.gameObject.SetActive(false);
        linesBg.color = new Color(0, 0, 0, 0.18f);
        foreach (var kv in docCards) kv.Value.color = T.panelAlt;

        UiKit.Clear(linesContent);
        int hits = 0;
        foreach (var doc in Game.Case.Documents)
        {
            if (!Game.IsDocumentUnlocked(doc.Id)) continue;
            for (int i = 0; i < doc.Lines.Count; i++)
            {
                if (doc.Lines[i].Text.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                AddLine(doc, i, ConsoleSources.Contains(doc.Source), showDoc: true);
                hits++;
            }
        }
        docMeta.text = hits == 0 ? "No matches in the logs you have so far." : $"{hits} matching line{(hits == 1 ? "" : "s")} across all delivered logs. Click a line to open its log.";
        PlayFlagSwipe();
    }

    string Highlight(string text)
    {
        if (query.Length < 2) return text;
        return Regex.Replace(text, Regex.Escape(query), m => $"<mark=#4FA3FF66>{m.Value}</mark>", RegexOptions.IgnoreCase);
    }

    // ---------- flagging ----------

    void Flag(CaseDocument doc, int index)
    {
        justFlagged = $"{doc.Id}:{index}";
        Sfx.Play("flag");
        var item = Game.Evidence.FlagLine(doc, index);
        ui.Toast($"Added to evidence: {item.Title}", SourceStyle.Color(item.Source));
    }

    void PlayFlagSwipe()
    {
        if (justFlagged == null) return;
        var row = linesContent.Find($"Line {justFlagged}");
        justFlagged = null;
        if (row == null) return;
        var marker = row.Find("Marker") as RectTransform;
        if (marker != null) ui.StartCoroutine(Swipe(marker, (RectTransform)row));
    }

    static IEnumerator Swipe(RectTransform marker, RectTransform row)
    {
        // The marker sweeps across the whole line, then settles into a slim bar on the left.
        yield return null;
        float full = row.rect.width - 8;
        var img = marker.GetComponent<Image>();
        var c = img.color;
        for (float t = 0; t < 0.22f; t += Time.unscaledDeltaTime)
        {
            marker.sizeDelta = new Vector2(Mathf.Lerp(8, full, t / 0.22f), marker.sizeDelta.y);
            img.color = new Color(c.r, c.g, c.b, 0.55f);
            yield return null;
        }
        for (float t = 0; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.3f;
            marker.sizeDelta = new Vector2(Mathf.Lerp(full, 8, k * k), marker.sizeDelta.y);
            img.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0.55f, c.a, k));
            yield return null;
        }
        marker.sizeDelta = new Vector2(8, marker.sizeDelta.y);
        img.color = c;
    }

    static Color NameColor(string name)
    {
        int h = 0;
        foreach (char ch in name) h = h * 31 + ch;
        return Color.HSVToRGB(Mathf.Abs(h % 360) / 360f, 0.55f, 0.95f);
    }

    void Summarize()
    {
        if (current == null) return;
        Sfx.Play("click");
        ui.ShowTab(DeskUI.Tab.Jensenbot);
        ui.JensenbotView.RequestSummary(current);
    }
}
