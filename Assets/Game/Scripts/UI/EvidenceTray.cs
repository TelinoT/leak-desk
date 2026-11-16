using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Right-hand column listing collected evidence. While interrogating, each card has a "Show" button.</summary>
public class EvidenceTray
{
    readonly DeskUI ui;
    readonly RectTransform content;

    // Tutorial targets.
    public RectTransform Root { get; private set; }
    public RectTransform FirstCard
    {
        get
        {
            foreach (Transform c in content)
                if (c.gameObject.activeSelf && c.GetComponent<EvidenceDrag>() != null) return (RectTransform)c;
            return null;
        }
    }
    readonly TextMeshProUGUI header;
    readonly GameObject emptyHint;

    UiTheme T => UiKit.Theme;

    public EvidenceTray(DeskUI ui, Transform parent)
    {
        this.ui = ui;
        var panel = UiKit.Panel(parent, T.panel, T.panelSprite, "EvidenceTray");
        Root = (RectTransform)panel.transform;
        panel.Size(prefW: 400, flexH: 1);
        panel.VLayout(10, 16);
        header = UiKit.Heading(panel.transform, "Evidence (0)", 26);
        UiKit.Text(panel.transform, "Everything you've found so far.", T.smallSize, T.muted);

        var scroll = UiKit.ScrollList(panel.transform, out content, 10, 2);
        scroll.Size(flexH: 1);

        var hint = UiKit.Card(content, new Color(1, 1, 1, 0.06f), 24, 10, "EmptyHint");
        hint.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;
        var ir = UiKit.Rect("IconRow", hint.transform);
        ir.HLayout(0, 0, TextAnchor.MiddleCenter);
        UiKit.Icon(ir, "folder", 56, new Color(1, 1, 1, 0.25f));
        UiKit.Text(hint.transform, "<b>No evidence yet</b>", T.bodySize, T.text, TextAlignmentOptions.Center);
        UiKit.Text(hint.transform, "Flag log lines, search desks or get someone talking. It all ends up here.",
            T.smallSize, T.muted, TextAlignmentOptions.Center);
        emptyHint = hint.gameObject;

        ui.Game.Evidence.Added += _ =>
        {
            Refresh(pulseLast: true);
            UiFx.Punch(header.transform, 0.18f, 0.4f);
        };
    }

    public void Refresh(bool pulseLast = false)
    {
        var items = ui.Game.Evidence.Items;
        header.text = $"Evidence ({items.Count})";
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            var child = content.GetChild(i).gameObject;
            if (child != emptyHint) Object.Destroy(child);
        }
        emptyHint.SetActive(items.Count == 0);

        bool canPresent = (ui.CurrentTab == DeskUI.Tab.Interrogate || ui.CurrentTab == DeskUI.Tab.Jensenbot)
                          && ui.Game.Phase == GamePhase.Investigating;
        // Newest first, so fresh finds are visible without scrolling.
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            var card = EvidenceCard(content, item, canPresent
                ? () => { if (ui.CurrentTab == DeskUI.Tab.Jensenbot) ui.JensenbotView.Confront(item); else ui.Interrogation.Present(item); }
                : null);
            if (pulseLast && i == items.Count - 1) ui.StartCoroutine(DeskUI.PopModal(card.transform));
        }
    }

    /// <summary>Shared evidence card visual (tray, picker, accusation).</summary>
    public static Image EvidenceCard(Transform parent, EvidenceItem item, System.Action onShow, string buttonLabel = "Show")
    {
        var T = UiKit.Theme;
        var card = UiKit.Card(parent, T.card, 12, 4, "Evidence: " + item.Id);
        var top = UiKit.Rect("Top", card.transform);
        top.HLayout(8);
        var tag = UiKit.Chip(top, SourceStyle.Color(item.Source), 124, 28, "Tag");
        tag.GetComponent<HorizontalLayoutGroup>().spacing = 4;
        UiKit.Icon(tag.transform, SourceStyle.Icon(item.Source), 18, T.cardText);
        UiKit.Text(tag.transform, SourceStyle.Label(item.Source), 13, T.cardText, TextAlignmentOptions.Center, FontStyles.Bold);
        var title = UiKit.Text(top, $"<b>{item.Title}</b>", T.smallSize, T.cardText);
        title.Size(flexW: 1);
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.overflowMode = TextOverflowModes.Ellipsis;
        if (onShow != null)
        {
            var b = UiKit.Button(top, buttonLabel, onShow, T.warning, T.cardText, 30, 15);
            b.Size(prefW: 74, prefH: 30, minW: 74);
        }
        UiKit.Text(card.transform, item.Content, T.smallSize - 1, new Color(T.cardText.r, T.cardText.g, T.cardText.b, 0.85f));
        card.gameObject.AddComponent<EvidenceDrag>().Item = item;
        card.gameObject.AddComponent<ButtonPop>();
        return card;
    }
}
