using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Jensenbot app: chat with the company AI and ask it to summarize documents.</summary>
public class JensenbotPanel
{
    readonly DeskUI ui;
    readonly GameObject root;
    readonly ChatView chat;
    readonly TMP_InputField input;
    readonly Button askButton;
    readonly RectTransform quickRow;
    readonly JensenbotFace face;
    readonly RectTransform chatArea;
    readonly CanvasGroup exposedGroup;
    readonly Button confrontButton;
    EvidencePicker picker;

    UiTheme T => UiKit.Theme;
    JensenbotAgent Bot => ui.Game.Jensenbot;
    static readonly Color BotColor = new(0.46f, 0.85f, 0.25f);

    public JensenbotPanel(DeskUI ui, Transform parent)
    {
        this.ui = ui;
        var panel = UiKit.Panel(parent, T.panel, T.panelSprite, "JensenbotPanel");
        ((RectTransform)panel.transform).Stretch();
        root = panel.gameObject;
        panel.VLayout(12, 20);

        var header = UiKit.Rect("Header", panel.transform);
        header.HLayout(16);
        face = JensenbotFace.Create(header, 112);
        var col = UiKit.Rect("Col", header);
        col.VLayout(4);
        col.Size(flexW: 1);
        UiKit.Heading(col, Bot.Name, 34);
        UiKit.Text(col, "Envydia's AI desk assistant. Summarizes any log in seconds! Trusted by leadership.",
            T.bodySize - 2, T.muted);

        var chatBg = UiKit.Panel(panel.transform, new Color(0, 0, 0, 0.22f), T.panelSprite, "ChatBg");
        chatBg.Size(flexH: 1, flexW: 1);
        chatArea = UiKit.Rect("Content", chatBg.transform).Stretch();
        chat = ChatView.Create(chatArea);
        ((RectTransform)chat.transform).Stretch();

        // Drag raw evidence onto the chat to confront Jensenbot with what its summaries left out.
        var drop = UiKit.Panel(chatArea, new Color(1f, 0.35f, 0.45f, 0.25f), T.panelSprite, "DropHighlight");
        ((RectTransform)drop.transform).Stretch(6, 6, 6, 6);
        var dt = UiKit.Text(drop.transform, "DROP TO CONFRONT JENSENBOT", 30, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        ((RectTransform)dt.transform).Stretch();
        drop.raycastTarget = false;
        drop.gameObject.SetActive(false);
        var zone = chatBg.gameObject.AddComponent<PresentDropZone>();
        zone.Highlight = drop;
        zone.ShakeTarget = chatArea;
        zone.CanAccept = () => !Bot.Busy && ui.Game.Phase == GamePhase.Investigating;
        zone.OnPresent = item => Confront(item, slammed: true);

        var exposed = UiKit.Rect("Exposed", chatArea).Stretch();
        exposedGroup = exposed.gameObject.AddComponent<CanvasGroup>();
        exposedGroup.alpha = 0;
        exposedGroup.blocksRaycasts = false;
        var stamp = UiKit.Panel(exposed, T.danger, T.buttonSprite, "Stamp");
        var sr = (RectTransform)stamp.transform;
        sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0.55f);
        sr.sizeDelta = new Vector2(520, 120);
        var st = UiKit.Text(stamp.transform, "AI EXPOSED!", 58, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        ((RectTransform)st.transform).Stretch();
        if (T.headingFont != null) st.font = T.headingFont;

        UiKit.Text(panel.transform, "Quick summaries:", T.smallSize, T.muted);
        quickRow = UiKit.Rect("Quick", panel.transform);
        quickRow.HLayout(8);
        RebuildQuick();
        ui.Game.WaveUnlocked += _ => RebuildQuick();

        var row = UiKit.Rect("InputRow", panel.transform);
        row.HLayout(10);
        input = UiKit.Input(row, "Ask Jensenbot anything...");
        input.onSubmit.AddListener(_ => Ask());
        askButton = UiKit.Button(row, "Ask", Ask, BotColor, T.accentText);
        askButton.Size(prefW: 120, prefH: 56);
        confrontButton = UiKit.Button(row, "Confront", OpenPicker, T.danger, Color.white);
        confrontButton.Size(prefW: 170, prefH: 56);

        chat.AddBubble(Bot.Name, "Hi teammate! ^_^ I already ran a preliminary analysis: anomalies point to Lab B2. " +
                                 "Want me to summarize a log so you don't have to read all that raw data?", ChatView.Side.Left,
            new Color(0.88f, 1f, 0.82f));
    }

    /// <summary>One-click summaries for the 4 most recently delivered logs.</summary>
    void RebuildQuick()
    {
        UiKit.Clear(quickRow);
        var docs = ui.Game.Case.Documents.FindAll(d => ui.Game.IsDocumentUnlocked(d.Id) && d.Source != "email" && d.Source != "news");
        for (int i = docs.Count - 1, n = 0; i >= 0 && n < 4; i--, n++)
        {
            var doc = docs[i];
            var b = UiKit.Button(quickRow, doc.Title, () => RequestSummary(doc), T.panelAlt, T.text, 44, 17);
            b.Size(flexW: 1, prefH: 44);
            var label = b.GetComponentInChildren<TMP_Text>();
            ((RectTransform)label.transform).Stretch(44, 4, 12, 4);
            var ic = UiKit.Icon(b.transform, SourceStyle.Icon(doc.Source), 22, SourceStyle.Color(doc.Source));
            var ir = (RectTransform)ic.transform;
            ir.anchorMin = ir.anchorMax = new Vector2(0, 0.55f);
            ir.anchoredPosition = new Vector2(26, 0);
            ir.sizeDelta = new Vector2(22, 22);
        }
    }

    public void SetVisible(bool v)
    {
        root.SetActive(v);
        if (v) chat.ScrollToBottom();
        else Voice.Stop();
    }

    public void RequestSummary(CaseDocument doc)
    {
        if (Bot.Busy) { ui.Toast("Jensenbot is still typing...", T.warning); return; }
        chat.AddBubble("You", $"Summarize: {doc.Title}", ChatView.Side.Right, T.bubblePlayer, T.accentText);
        Send(done => Bot.Summarize(doc, done));
    }

    void Ask()
    {
        string q = input.text.Trim();
        if (q.Length == 0 || Bot.Busy) return;
        input.text = "";
        chat.AddBubble("You", q, ChatView.Side.Right, T.bubblePlayer, T.accentText);
        Send(done => Bot.Ask(q, done));
    }

    public void Confront(EvidenceItem item) => Confront(item, slammed: false);

    void Confront(EvidenceItem item, bool slammed)
    {
        if (Bot.Busy || ui.Game.Phase != GamePhase.Investigating) return;
        ui.ShowTab(DeskUI.Tab.Jensenbot);
        if (!slammed)
        {
            Sfx.Play("slam");
            UiFx.Shake(chatArea, 12f, 0.3f);
        }
        var b = chat.AddBubble($"EVIDENCE  ·  {SourceStyle.Label(item.Source)}", item.Content, ChatView.Side.Right, new Color(1f, 0.94f, 0.74f));
        var stripe = UiKit.Panel(b.transform, SourceStyle.Color(item.Source), T.pillSprite, "Stripe").Pill(6);
        stripe.Size(prefH: 6);
        stripe.transform.SetAsFirstSibling();

        askButton.interactable = confrontButton.interactable = false;
        chat.ShowTyping(Bot.Name);
        face.SetMood(JensenbotFace.Mood.Thinking);
        Bot.Confront(item, ui.Game.Evidence.Items, (reply, exposed) => Voice.Speak(BotVoice, Clean(reply), clip =>
        {
            chat.HideTyping();
            if (exposed) ui.StartCoroutine(Exposed());
            var bubble = chat.AddBubble(Bot.Name, Clean(reply), ChatView.Side.Left,
                exposed || Bot.Exposed ? new Color(1f, 0.86f, 0.86f) : new Color(0.88f, 1f, 0.82f));
            var texts = bubble.GetComponentsInChildren<TextMeshProUGUI>();
            Voice.TypeAlong(texts[^1], clip, 70f, exposed ? 1.5f : 1.9f);
            askButton.interactable = confrontButton.interactable = true;
            if (!exposed) face.SetMood(JensenbotFace.Mood.Talking);
            if (exposed)
            {
                var jb = ui.Game.Case.Jensenbot;
                var ev = ui.Game.Evidence.AddCaseEvidence(jb.ConfessionEvidence);
                if (ev != null) ui.Toast($"New evidence: {ev.Title}", T.danger);
            }
        }));
    }

    System.Collections.IEnumerator Exposed()
    {
        Sfx.Play("crack", 1.3f);
        face.SetCompromised();
        UiFx.Shake(chatArea, 24f, 0.6f);
        var stamp = exposedGroup.transform.GetChild(0);
        exposedGroup.alpha = 1;
        for (float t = 0; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            float k = 1 - Mathf.Pow(1 - t / 0.35f, 3);
            stamp.localScale = Vector3.one * Mathf.Lerp(2.6f, 1f, k);
            stamp.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(20f, 6f, k));
            yield return null;
        }
        Sfx.Play("stamp");
        UiFx.Punch(stamp, 0.12f, 0.3f);
        yield return new WaitForSecondsRealtime(1.8f);
        for (float t = 0; t < 0.4f; t += Time.unscaledDeltaTime)
        {
            exposedGroup.alpha = 1 - t / 0.4f;
            yield return null;
        }
        exposedGroup.alpha = 0;
    }

    void OpenPicker()
    {
        if (Bot.Busy) return;
        picker ??= new EvidencePicker(ui);
        picker.Open("Confront Jensenbot with the raw logs...", item => Confront(item, slammed: false));
    }

    // The UI font has no emoji glyphs: turn Slack-style :codes: into cheerful ASCII instead.
    static string Clean(string text)
    {
        text = Regex.Replace(text, @"\s*:(rocket|chart_with_upwards_trend|fire|star2?|sparkles|tada|100):", "");
        text = Regex.Replace(text, @":(wave|smile|slightly_smiling_face|blush|grin|sweat_smile|wink):", "^_^");
        return Regex.Replace(text, @":[a-z][a-z0-9_+-]{1,29}:", "");
    }

    void Send(System.Action<System.Action<string>> call)
    {
        askButton.interactable = false;
        chat.ShowTyping(Bot.Name);
        face.SetMood(JensenbotFace.Mood.Thinking);
        Sfx.Play("click", 1.4f);
        call(reply => Voice.Speak(BotVoice, SpokenPart(Clean(reply)), clip =>
        {
            chat.HideTyping();
            var bubble = chat.AddBubble(Bot.Name, Clean(reply), ChatView.Side.Left, new Color(0.88f, 1f, 0.82f));
            var texts = bubble.GetComponentsInChildren<TextMeshProUGUI>();
            if (clip != null) { Voice.Play(clip); UiFx.Typewrite(texts[^1], 90f); }
            else UiFx.Typewrite(texts[^1], 90f, 1.9f);
            askButton.interactable = true;
            face.SetMood(JensenbotFace.Mood.Talking);
            // A barely-there tell: the face glitches when its feed was doctored.
            if (Bot.LastWasTampered) ui.StartCoroutine(GlitchLater());
        }));
    }

    string BotVoice => ui.Game.Case.Jensenbot?.Voice;

    /// <summary>Summaries are bullet lists; Jensenbot only reads out its own take (the non-bullet lines).</summary>
    static string SpokenPart(string reply)
    {
        var lines = reply.Split('\n');
        var said = System.Linq.Enumerable.Where(lines, l => l.Trim().Length > 0 && !l.TrimStart().StartsWith("-") && !l.TrimStart().StartsWith("•"));
        string text = string.Join(" ", said);
        return text.Length > 0 ? text : reply;
    }

    System.Collections.IEnumerator GlitchLater()
    {
        yield return new WaitForSecondsRealtime(1.2f);
        face.SetMood(JensenbotFace.Mood.Glitch);
    }
}
