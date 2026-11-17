using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Right-hand column listing collected evidence. While interrogating, each card has a "Show" button.</summary>
public class EvidenceTray
{
    public static Color LeadColor(string leadId) => leadId switch
    {
        "motive" => new Color(0.95f, 0.45f, 0.35f),
        "opportunity" => new Color(0.25f, 0.65f, 0.95f),
        "method" => new Color(0.85f, 0.35f, 0.85f),
        "accomplice" => new Color(0.35f, 0.75f, 0.35f),
        _ => new Color(0.55f, 0.55f, 0.65f),
    };

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

        ui.Game.Evidence.Removed += _ => Refresh();
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
                : null, onRemove: ui.Game.Phase == GamePhase.Investigating ? () => RemoveItem(item) : null);
            if (pulseLast && i == items.Count - 1) ui.StartCoroutine(DeskUI.PopModal(card.transform));
        }
    }

    void RemoveItem(EvidenceItem item)
    {
        Sfx.Play("whoosh", 1.3f);
        ui.Game.Evidence.Remove(item.Id);
        ui.Toast($"Threw out: {item.Title}", UiKit.Theme.muted, "delete_1");
    }

    /// <summary>Shared evidence card visual (tray, picker, accusation). Shows which leads the item helps with.</summary>
    public static Image EvidenceCard(Transform parent, EvidenceItem item, System.Action onShow, string buttonLabel = "Show",
        System.Action onRemove = null)
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
        if (onRemove != null)
        {
            var x = UiKit.Button(top, "", onRemove, new Color(0, 0, 0, 0.08f), T.cardText, 30, 15);
            x.Size(prefW: 30, prefH: 30, minW: 30);
            var xi = UiKit.Icon(x.transform, "mark_x", 18, new Color(T.cardText.r, T.cardText.g, T.cardText.b, 0.6f));
            var xr = (RectTransform)xi.transform;
            xr.anchorMin = xr.anchorMax = new Vector2(0.5f, 0.5f);
            xr.sizeDelta = new Vector2(18, 18);
        }
        UiKit.Text(card.transform, item.Content, T.smallSize - 1, new Color(T.cardText.r, T.cardText.g, T.cardText.b, 0.85f));

        // Lead chips: what this piece of evidence is good for.
        var c = LeakDeskGame.Instance != null ? LeakDeskGame.Instance.Case : null;
        if (c != null)
        {
            var leads = Leads.For(c, item).ToList();
            if (leads.Count > 0)
            {
                var row = UiKit.Rect("Leads", card.transform);
                row.HLayout(6);
                foreach (var l in leads)
                {
                    var chip = UiKit.Chip(row, LeadColor(l.Id), 0, 24, "Lead");
                    chip.GetComponent<LayoutElement>().preferredWidth = -1;
                    chip.GetComponent<LayoutElement>().minWidth = -1;
                    chip.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(10, 10, 2, 2);
                    chip.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
                    var lt = UiKit.Text(chip.transform, l.Title.ToUpperInvariant(), 13, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                    lt.textWrappingMode = TextWrappingModes.NoWrap;
                    chip.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                }
                UiKit.Spacer(row, 1);
            }
        }
        card.gameObject.AddComponent<EvidenceDrag>().Item = item;
        card.gameObject.AddComponent<ButtonPop>();
        return card;
    }
}
