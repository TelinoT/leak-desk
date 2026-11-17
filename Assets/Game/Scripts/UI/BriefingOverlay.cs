using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Opening sequence: a title card over the office (camera glides in), then the briefing styled as the CEO's email
/// plus four "how to play" tiles. Starting the investigation starts the clock.
/// </summary>
public class BriefingOverlay
{
    readonly DeskUI ui;
    readonly GameObject titleRoot, briefRoot;
    readonly CanvasGroup titleGroup;
    readonly RectTransform titleText;
    readonly Transform modal;
    readonly TextMeshProUGUI mailBody;
    bool skipped;
    readonly Button startButton;
    readonly Button tutorialToggle;

    void ToggleTutorial()
    {
        Sfx.Play("click");
        if (Tutorial.ShouldRun) Tutorial.MarkSeen(); else Tutorial.Reset();
        RefreshToggle();
    }

    void RefreshToggle() =>
        tutorialToggle.SetLabel(Tutorial.ShouldRun ? "[X] Guided tutorial" : "[ ] Guided tutorial");

    public BriefingOverlay(DeskUI ui, Transform overlayRoot)
    {
        this.ui = ui;
        var T = UiKit.Theme;
        var c = ui.Game.Case;

        // ---------- title card ----------
        var title = UiKit.Panel(overlayRoot, new Color(0.06f, 0.03f, 0.16f, 0.95f), name: "TitleCard");
        ((RectTransform)title.transform).Stretch();
        titleRoot = title.gameObject;
        titleGroup = titleRoot.AddComponent<CanvasGroup>();
        title.gameObject.AddComponent<Button>().onClick.AddListener(() => skipped = true);
        var col = UiKit.Rect("Col", title.transform);
        col.anchorMin = col.anchorMax = new Vector2(0.5f, 0.55f);
        col.sizeDelta = new Vector2(1400, 420);
        col.VLayout(8, 0, TextAnchor.MiddleCenter);
        var kicker = UiKit.Text(col, $"{c.Company.ToUpperInvariant()} // INTERNAL SECURITY", 26, T.accent, TextAlignmentOptions.Center, FontStyles.Bold);
        kicker.characterSpacing = 12;
        var big = UiKit.Heading(col, "LEAK DESK", 170, Color.white);
        big.alignment = TextAlignmentOptions.Center;
        big.textWrappingMode = TextWrappingModes.NoWrap;
        big.fontMaterial.EnableKeyword("UNDERLAY_ON");
        big.fontMaterial.SetColor("_UnderlayColor", new Color(0.3f, 0.1f, 0.5f, 0.9f));
        big.fontMaterial.SetFloat("_UnderlayOffsetX", 0.6f);
        big.fontMaterial.SetFloat("_UnderlayOffsetY", -0.6f);
        titleText = (RectTransform)big.transform;
        UiKit.Text(col, $"Case 01: <b>{c.Title}</b>", 34, T.text, TextAlignmentOptions.Center);
        UiKit.Text(col, "click to continue", 18, T.muted, TextAlignmentOptions.Center);
        titleRoot.SetActive(false);

        // ---------- briefing ----------
        var card = DeskUI.Modal(overlayRoot, "Briefing", 1500, 860, out briefRoot);
        modal = card.transform;
        UiKit.Text(card.transform, $"<color=#{UiKit.Hex(T.accent)}><b>{c.Company.ToUpperInvariant()}</b></color> // SECURITY DESK  ·  WED {c.StartTime}",
            T.smallSize + 1, T.muted);
        UiKit.Heading(card.transform, c.Tagline, 40);

        var row = UiKit.Rect("Row", card.transform);
        row.HLayout(24, 0, TextAnchor.UpperLeft);
        row.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;
        row.Size(flexH: 1);

        // CEO email
        var mail = UiKit.Card(row, T.card, 24, 10, "Mail");
        mail.Size(prefW: 760, flexH: 1);
        var mh = UiKit.Rect("Header", mail.transform);
        mh.HLayout(12);
        var mi = UiKit.Panel(mh, SourceStyle.Color("email"), UiKit.AvatarSprite, "Icon");
        mi.Size(prefW: 52, prefH: 52, minW: 52);
        ((RectTransform)UiKit.Icon(UiKit.Rect("H", mi.transform).Stretch(12, 12, 12, 12), "mail", 28, Color.white).transform).Stretch();
        var mcol = UiKit.Rect("Col", mh);
        mcol.VLayout(0);
        mcol.Size(flexW: 1);
        UiKit.Text(mcol, "<b>Dex Halloran</b>  <color=#888888>CEO</color>", T.bodySize, T.cardText);
        UiKit.Text(mcol, "To: Security Desk  ·  Subject: <b>the leak</b>", T.smallSize, new Color(T.cardText.r, T.cardText.g, T.cardText.b, 0.65f));
        var line = UiKit.Panel(mail.transform, new Color(0, 0, 0, 0.08f), name: "Rule");
        line.Size(prefH: 2);
        mailBody = UiKit.Text(mail.transform, string.IsNullOrEmpty(c.CeoMail) ? c.Briefing : c.CeoMail, T.bodySize + 1, T.cardText);
        UiKit.Spacer(mail.transform, 0, 1);
        UiKit.Text(mail.transform, "<i>P.S. Jensenbot already had a look. Use it, it's what we pay for.</i>", T.smallSize + 1, new Color(T.cardText.r, T.cardText.g, T.cardText.b, 0.75f));

        // How to play
        var how = UiKit.Rect("HowTo", row);
        how.VLayout(14);
        how.Size(flexW: 1, flexH: 1);
        UiKit.Text(how, "WHAT YOU'VE GOT", T.smallSize, T.accent, style: FontStyles.Bold);
        Tile(how, "folder", "Logs", "Emails, Snack chats, badge swipes, VPN and file logs from last night. Flag anything odd, or search for a name.", SourceStyle.Color("email"));
        Tile(how, "find", "Desks", "Go through monitors, drawers and bins. Each search takes 30 seconds.", SourceStyle.Color("desk"));
        Tile(how, "mic", "Interrogate", "Talk to the four people who had the deck. Drag evidence onto them and see what happens.", SourceStyle.Color("badge"));
        Tile(how, "mallet", "Accuse", "Name one person before 09:00, with up to three pieces of evidence.", T.danger);

        var startRow = UiKit.Rect("StartRow", card.transform);
        startRow.HLayout(14);
        tutorialToggle = UiKit.Button(startRow, "", ToggleTutorial, T.panelAlt, T.text, 76, 20);
        tutorialToggle.Size(prefW: 300, prefH: 76);
        RefreshToggle();
        startButton = UiKit.Button(startRow, $"Start investigation  ·  {Mathf.RoundToInt(c.RoundMinutes)} minutes", Start, T.accent, T.accentText, 76, 30);
        startButton.Size(flexW: 1, prefH: 76);
        briefRoot.SetActive(false);
    }

    static void Tile(Transform parent, string icon, string title, string text, Color color)
    {
        var T = UiKit.Theme;
        var tile = UiKit.Panel(parent, T.panelAlt, T.cardSprite, "Tile: " + title);
        tile.HLayout(16, 16);
        var ib = UiKit.Panel(tile.transform, color, UiKit.AvatarSprite, "IconBg");
        ib.Size(prefW: 64, prefH: 64, minW: 64);
        ((RectTransform)UiKit.Icon(UiKit.Rect("H", ib.transform).Stretch(15, 15, 15, 15), icon, 34, T.cardText).transform).Stretch();
        var col = UiKit.Rect("Col", tile.transform);
        col.VLayout(2);
        col.Size(flexW: 1);
        UiKit.Text(col, $"<b>{title}</b>", T.bodySize, T.text);
        UiKit.Text(col, text, T.smallSize, T.muted);
    }

    public void Show()
    {
        RefreshToggle();
        ui.StartCoroutine(Sequence());
    }

    IEnumerator Sequence()
    {
        CameraDrift.PlayIntro();
        titleRoot.SetActive(true);
        titleRoot.transform.SetAsLastSibling();
        titleGroup.alpha = 0;
        for (float t = 0; t < 0.6f; t += Time.unscaledDeltaTime)
        {
            float k = 1 - Mathf.Pow(1 - t / 0.6f, 3);
            titleGroup.alpha = k;
            titleText.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, k);
            yield return null;
        }
        titleText.localScale = Vector3.one;
        Sfx.Play("stamp");
        UiFx.Punch(titleText, 0.08f, 0.35f);

        for (float t = 0; t < 3.2f && !skipped; t += Time.unscaledDeltaTime) yield return null;

        for (float t = 0; t < 0.4f; t += Time.unscaledDeltaTime)
        {
            titleGroup.alpha = 1 - t / 0.4f;
            yield return null;
        }
        titleRoot.SetActive(false);

        briefRoot.SetActive(true);
        briefRoot.transform.SetAsLastSibling();
        Sfx.Play("chime");
        ui.StartCoroutine(DeskUI.PopModal(modal));
        UiFx.Typewrite(mailBody, 140f);
        // The start button breathes until it's pressed.
        var label = startButton.GetComponentInChildren<TMP_Text>().transform;
        while (briefRoot.activeSelf)
        {
            float k = 1f + Mathf.Sin(Time.unscaledTime * 3f) * 0.025f;
            label.localScale = new Vector3(k, k, 1);
            yield return null;
        }
    }

    void Start()
    {
        briefRoot.SetActive(false);
        Sfx.Play("click");
        ui.Game.StartInvestigation();
        if (Tutorial.ShouldRun) ui.Tutorial.Begin();
        else ui.Toast("Clock's running. Board meets at 09:00.", UiKit.Theme.accent);
    }
}
