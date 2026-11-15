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
        var av = UiKit.Panel(header, BotColor, T.pillSprite, "Avatar");
        av.Size(prefW: 90, prefH: 90, minW: 90);
        var face = UiKit.Text(av.transform, "^_^", 32, T.accentText, TextAlignmentOptions.Center, FontStyles.Bold);
        ((RectTransform)face.transform).Stretch();
        var col = UiKit.Rect("Col", header);
        col.VLayout(4);
        col.Size(flexW: 1);
        UiKit.Heading(col, Bot.Name, 34);
        UiKit.Text(col, "Envydia's AI desk assistant. Summarizes any log in seconds! Trusted by leadership. (!)",
            T.bodySize - 2, T.muted);

        var chatBg = UiKit.Panel(panel.transform, new Color(0, 0, 0, 0.22f), T.panelSprite, "ChatBg");
        chatBg.Size(flexH: 1, flexW: 1);
        chat = ChatView.Create(chatBg.transform);
        ((RectTransform)chat.transform).Stretch();

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
        askButton.Size(prefW: 140, prefH: 56);

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
        }
    }

    public void SetVisible(bool v)
    {
        root.SetActive(v);
        if (v) chat.ScrollToBottom();
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

    // The UI font has no emoji glyphs: turn Slack-style :codes: into cheerful ASCII instead.
    static string Clean(string text)
    {
        text = Regex.Replace(text, @":(rocket|chart_with_upwards_trend|fire|star2?|sparkles|tada|100):", "<b>(!)</b>");
        text = Regex.Replace(text, @":(wave|smile|slightly_smiling_face|blush|grin|sweat_smile|wink):", "^_^");
        return Regex.Replace(text, @":[a-z][a-z0-9_+-]{1,29}:", "");
    }

    void Send(System.Action<System.Action<string>> call)
    {
        askButton.interactable = false;
        chat.ShowTyping(Bot.Name);
        call(reply =>
        {
            chat.HideTyping();
            chat.AddBubble(Bot.Name, Clean(reply), ChatView.Side.Left, new Color(0.88f, 1f, 0.82f));
            askButton.interactable = true;
        });
    }
}
