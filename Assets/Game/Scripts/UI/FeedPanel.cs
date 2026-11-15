using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Logs app: list of case documents on the left, the selected document's lines (flaggable) on the right.</summary>
public class FeedPanel
{
    readonly DeskUI ui;
    readonly GameObject root;
    readonly RectTransform listContent;
    readonly RectTransform linesContent;
    readonly TextMeshProUGUI docTitle, docMeta, docTag;
    readonly Image docTagBg;
    readonly Button summarizeButton;
    readonly Dictionary<string, Image> docCards = new();
    readonly HashSet<string> read = new();
    readonly TextMeshProUGUI incoming;
    CaseDocument current;

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

        // Document list
        var listPanel = UiKit.Panel(rt, T.panel, T.panelSprite, "DocList");
        listPanel.Size(prefW: 400, flexH: 1);
        listPanel.VLayout(10, 16);
        UiKit.Heading(listPanel.transform, "Overnight logs", 26);
        UiKit.Text(listPanel.transform, "Tue 17:00 to Wed 08:00. More arrives as you dig.", T.smallSize, T.muted);
        var scroll = UiKit.ScrollList(listPanel.transform, out listContent, 8, 2);
        scroll.Size(flexH: 1);
        incoming = UiKit.Text(listPanel.transform, "", T.smallSize, T.warning, TextAlignmentOptions.Center);
        incoming.Size(prefH: 44);

        foreach (var doc in Game.Case.Documents)
            if (Game.IsDocumentUnlocked(doc.Id)) AddDocCard(doc);
        Game.WaveUnlocked += OnWave;

        // Viewer
        var viewer = UiKit.Panel(rt, T.panel, T.panelSprite, "DocViewer");
        viewer.Size(flexW: 1, flexH: 1);
        viewer.VLayout(12, 22);

        var header = UiKit.Rect("Header", viewer.transform);
        header.HLayout(14);
        docTagBg = UiKit.Panel(header, Color.white, T.pillSprite, "Tag");
        docTagBg.HLayout(0, 8, TextAnchor.MiddleCenter);
        docTagBg.Size(prefW: 130, prefH: 34);
        docTag = UiKit.Text(docTagBg.transform, "", T.smallSize, T.cardText, TextAlignmentOptions.Center, FontStyles.Bold);
        var titles = UiKit.Rect("Titles", header);
        titles.VLayout(2);
        titles.Size(flexW: 1);
        docTitle = UiKit.Heading(titles, "", 30);
        docMeta = UiKit.Text(titles, "", T.smallSize, T.muted);
        summarizeButton = UiKit.Button(header, "Ask Jensenbot to summarize", Summarize, T.secondary, Color.white, 52, 19);
        summarizeButton.Size(prefW: 320, prefH: 52);

        UiKit.Text(viewer.transform, "Read the raw lines. <b>Flag</b> anything suspicious to add it to your evidence tray.",
            T.smallSize, T.muted);

        var linesScroll = UiKit.ScrollList(viewer.transform, out linesContent, 10, 4);
        linesScroll.Size(flexH: 1);

        Game.Evidence.Added += _ => RefreshLines();
        Select(Game.Case.Documents[0]);
    }

    public void SetVisible(bool v) => root.SetActive(v);

    void OnWave(CaseWave wave)
    {
        // Newest deliveries go on top so they are seen without scrolling.
        for (int i = wave.Documents.Count - 1; i >= 0; i--)
        {
            var doc = Game.Case.Documents.Find(d => d.Id == wave.Documents[i]);
            if (doc == null || docCards.ContainsKey(doc.Id)) continue;
            AddDocCard(doc).transform.SetSiblingIndex(0);
        }
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
        btn.onClick.AddListener(() => Select(doc));
        card.gameObject.AddComponent<ButtonPop>();

        var tag = UiKit.Panel(card.transform, SourceStyle.Color(doc.Source), T.pillSprite, "Tag");
        tag.Size(prefW: 10, minW: 10, prefH: 54);

        var col = UiKit.Rect("Col", card.transform);
        col.VLayout(2);
        col.Size(flexW: 1);
        UiKit.Text(col, $"<b>{doc.Title}</b>", T.bodySize - 1, T.text).textWrappingMode = TextWrappingModes.NoWrap;
        var meta = UiKit.Text(col, $"{SourceStyle.Label(doc.Source)}  ·  {doc.Time}", T.smallSize - 1, T.muted);
        meta.textWrappingMode = TextWrappingModes.NoWrap;
        meta.overflowMode = TextOverflowModes.Ellipsis;

        var unread = UiKit.Panel(card.transform, T.accent, T.pillSprite, "Unread");
        unread.Size(prefW: 12, prefH: 12, minW: 12);
        docCards[doc.Id] = card;
        return card;
    }

    public void Select(CaseDocument doc)
    {
        current = doc;
        read.Add(doc.Id);
        foreach (var kv in docCards)
        {
            bool on = kv.Key == doc.Id;
            kv.Value.color = on ? Color.Lerp(T.panelAlt, T.secondary, 0.35f) : T.panelAlt;
            var dot = kv.Value.transform.Find("Unread");
            if (dot != null) dot.gameObject.SetActive(!read.Contains(kv.Key));
        }

        docTag.text = SourceStyle.Label(doc.Source);
        docTagBg.color = SourceStyle.Color(doc.Source);
        docTitle.text = doc.Title;
        docMeta.text = $"From {doc.From}  ·  {doc.Time}  ·  {doc.Lines.Count} lines";
        RefreshLines();
    }

    void RefreshLines()
    {
        if (current == null) return;
        UiKit.Clear(linesContent);
        for (int i = 0; i < current.Lines.Count; i++)
        {
            int index = i;
            bool flagged = Game.Evidence.IsLineFlagged(current, i);
            var row = UiKit.Panel(linesContent, flagged ? new Color(1f, 0.95f, 0.75f) : T.card, T.cardSprite, "Line");
            row.HLayout(14, 14);

            var text = UiKit.Text(row.transform, current.Lines[i].Text, T.bodySize, T.cardText);
            text.Size(flexW: 1);
            text.textWrappingMode = TextWrappingModes.Normal;

            var flag = UiKit.Button(row.transform, flagged ? "Flagged" : "Flag", () => Flag(index),
                flagged ? new Color(0.8f, 0.8f, 0.8f) : T.warning, T.cardText, 44, 18);
            flag.Size(prefW: 120, prefH: 44, minW: 120);
            flag.interactable = !flagged && Game.Phase is GamePhase.Investigating or GamePhase.Briefing;
        }
    }

    void Flag(int index)
    {
        var item = Game.Evidence.FlagLine(current, index);
        ui.Toast($"Added to evidence: {item.Title}", SourceStyle.Color(item.Source));
    }

    void Summarize()
    {
        if (current == null) return;
        ui.ShowTab(DeskUI.Tab.Jensenbot);
        ui.JensenbotView.RequestSummary(current);
    }
}
