using System;
using TMPro;
using UnityEngine;

/// <summary>Modal list of collected evidence; clicking one calls back and closes.</summary>
public class EvidencePicker
{
    readonly DeskUI ui;
    GameObject root;
    RectTransform content;
    TextMeshProUGUI title;
    Action<EvidenceItem> onPick;

    public EvidencePicker(DeskUI ui) => this.ui = ui;

    public void Open(string heading, Action<EvidenceItem> pick)
    {
        onPick = pick;
        if (root == null) Build();
        title.text = heading;
        UiKit.Clear(content);
        var items = ui.Game.Evidence.Items;
        if (items.Count == 0)
            UiKit.Text(content, "No evidence yet. Flag lines in Logs first.", UiKit.Theme.bodySize, UiKit.Theme.muted, TextAlignmentOptions.Center);
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            EvidenceTray.EvidenceCard(content, item, () => Pick(item), "Show");
        }
        root.SetActive(true);
        root.transform.SetAsLastSibling();
        ui.StartCoroutine(DeskUI.PopModal(root.transform.GetChild(0)));
    }

    void Build()
    {
        var modal = DeskUI.Modal(ui.OverlayRoot, "EvidencePicker", 860, 820, out root);
        title = UiKit.Heading(modal.transform, "Show evidence");
        UiKit.Text(modal.transform, "Pick what to put on the table.", UiKit.Theme.smallSize, UiKit.Theme.muted);
        var scroll = UiKit.ScrollList(modal.transform, out content, 10, 4);
        scroll.Size(flexH: 1);
        var close = UiKit.Button(modal.transform, "Cancel", Close, UiKit.Theme.panelAlt, UiKit.Theme.text);
        close.Size(prefH: 52);
    }

    void Pick(EvidenceItem item)
    {
        Close();
        onPick?.Invoke(item);
    }

    void Close() => root.SetActive(false);
}
