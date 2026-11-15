using TMPro;
using UnityEngine;

/// <summary>Start screen: case briefing and how to play. Starting it starts the 12-minute clock.</summary>
public class BriefingOverlay
{
    readonly DeskUI ui;
    readonly GameObject root;
    readonly Transform modal;

    public BriefingOverlay(DeskUI ui, Transform overlayRoot)
    {
        this.ui = ui;
        var T = UiKit.Theme;
        var c = ui.Game.Case;
        var card = DeskUI.Modal(overlayRoot, "Briefing", 1180, 880, out root);
        modal = card.transform;

        UiKit.Text(card.transform, $"<color=#{UiKit.Hex(T.accent)}><b>{c.Company.ToUpperInvariant()}</b></color> // SECURITY DESK  ·  WED 08:48",
            T.smallSize + 2, T.muted);
        UiKit.Heading(card.transform, $"LEAK DESK: {c.Title}", 52);
        UiKit.Text(card.transform, c.Briefing, T.bodySize + 2, T.text);

        var how = UiKit.Card(card.transform, new Color(1, 1, 1, 0.07f), 22, 10, "HowTo");
        UiKit.Text(how.transform, "HOW TO PLAY", T.smallSize, T.accent, style: FontStyles.Bold);
        UiKit.Text(how.transform,
            "<b>1. Logs</b> - read the overnight emails, Slack, badge, VPN and file logs. <b>Flag</b> suspicious lines to collect evidence.\n" +
            "<b>2. Interrogate</b> - question the 4 suspects in free text. <b>Present evidence</b> to break their stories. Watch their stress.\n" +
            "<b>3. Jensenbot</b> - the company AI can summarize logs for you. It's fast. It's confident. Is it right?\n" +
            "<b>4. Accuse</b> - name the leaker, attach up to 3 pieces of evidence and a motive before the board meets at 09:00.",
            T.bodySize, T.text);

        UiKit.Spacer(card.transform, 0, 1);
        var start = UiKit.Button(card.transform, "Start investigation  (12 minutes)", Start, T.accent, T.accentText, 72, 28);
        start.Size(prefH: 72);
        root.SetActive(false);
    }

    public void Show()
    {
        root.SetActive(true);
        root.transform.SetAsLastSibling();
        ui.StartCoroutine(DeskUI.PopModal(modal));
    }

    void Start()
    {
        root.SetActive(false);
        ui.Game.StartInvestigation();
        ui.Toast("Clock is running. Board meets at 09:00.", UiKit.Theme.accent);
    }
}
