using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Accusation screen: pick a suspect, attach up to 3 evidence items, flag Jensenbot, write a motive.</summary>
public class AccusationOverlay
{
    const int MaxEvidence = 3;

    readonly DeskUI ui;
    readonly GameObject root;
    readonly Transform modal;
    readonly TextMeshProUGUI subtitle, evidenceCount;
    readonly RectTransform evidenceContent;
    readonly Dictionary<string, Image> suspectCards = new();
    readonly TMP_InputField motive;
    readonly Button jensenToggle, submit, back;
    readonly HashSet<string> selectedEvidence = new();
    readonly Dictionary<string, Image> evidenceCards = new();
    string selectedSuspect;
    bool namesJensenbot;
    bool forced;

    UiTheme T => UiKit.Theme;
    LeakDeskGame Game => ui.Game;

    public AccusationOverlay(DeskUI ui, Transform overlayRoot)
    {
        this.ui = ui;
        var card = DeskUI.Modal(overlayRoot, "Accusation", 1500, 960, out root);
        modal = card.transform;

        UiKit.Heading(card.transform, "Present your case to the board", 38);
        subtitle = UiKit.Text(card.transform, "", T.bodySize, T.muted);

        // 1. Suspect
        UiKit.Text(card.transform, "<b>1. Who leaked the keynote?</b>", T.bodySize + 2, T.text);
        var row = UiKit.Rect("Suspects", card.transform);
        row.HLayout(14, 0, TextAnchor.MiddleLeft, expandWidth: true);
        foreach (var s in Game.Case.Suspects)
        {
            var sc = UiKit.Panel(row, T.panelAlt, T.cardSprite, "Pick: " + s.Id);
            sc.HLayout(12, 12);
            sc.Size(prefH: 92, flexW: 1);
            sc.gameObject.AddComponent<Button>().onClick.AddListener(() => PickSuspect(s.Id));
            sc.gameObject.AddComponent<ButtonPop>();
            var av = UiKit.Panel(sc.transform, s.Color, UiKit.AvatarSprite, "Avatar");
            av.Size(prefW: 64, prefH: 64, minW: 64);
            var ini = UiKit.Text(av.transform, InterrogationPanel.Initials(s.Name), 24, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            ((RectTransform)ini.transform).Stretch();
            var col = UiKit.Rect("Col", sc.transform);
            col.VLayout(2);
            col.Size(flexW: 1);
            UiKit.Text(col, $"<b>{s.Name}</b>", T.bodySize - 1, T.text);
            UiKit.Text(col, s.Role, T.smallSize - 3, T.muted);
            suspectCards[s.Id] = sc;
        }

        // 2. Evidence
        var evHeader = UiKit.Rect("EvidenceHeader", card.transform);
        evHeader.HLayout(12);
        UiKit.Text(evHeader, "<b>2. Attach up to 3 pieces of evidence</b>", T.bodySize + 2, T.text).Size(flexW: 1);
        evidenceCount = UiKit.Text(evHeader, "0 / 3", T.bodySize, T.muted, TextAlignmentOptions.Right);
        evidenceCount.Size(prefW: 120);
        var evBg = UiKit.Panel(card.transform, new Color(0, 0, 0, 0.22f), T.panelSprite, "EvidenceBg");
        evBg.Size(flexH: 1);
        var scroll = UiKit.ScrollList(evBg.transform, out evidenceContent, 10, 12);
        ((RectTransform)scroll.transform).Stretch();
        var grid = evidenceContent.gameObject;
        Object.DestroyImmediate(grid.GetComponent<VerticalLayoutGroup>());
        var g = grid.AddComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(452, 130);
        g.spacing = new Vector2(12, 12);
        g.padding = new RectOffset(12, 12, 12, 12);

        // 3. Accomplice + motive
        var bottom = UiKit.Rect("Bottom", card.transform);
        bottom.HLayout(14);
        jensenToggle = UiKit.Button(bottom, "[ ] Jensenbot helped them", ToggleJensenbot, T.panelAlt, T.text, 56, 20);
        jensenToggle.Size(prefW: 360, prefH: 56);
        motive = UiKit.Input(bottom, "3. Motive in one sentence: why did they do it?");

        var actions = UiKit.Rect("Actions", card.transform);
        actions.HLayout(14);
        back = UiKit.Button(actions, "Keep investigating", () => { Close(); Game.CancelAccusation(); }, T.panelAlt, T.text, 60);
        back.Size(prefW: 300, prefH: 60);
        UiKit.Spacer(actions, 1);
        submit = UiKit.Button(actions, "Present to the board", () => SubmitNow(false), T.danger, Color.white, 60, 24);
        submit.Size(prefW: 380, prefH: 60);

        root.SetActive(false);
    }

    public void Open(bool isForced)
    {
        forced = isForced;
        BuildEvidence();
        Refresh();
        root.SetActive(true);
        root.transform.SetAsLastSibling();
        ui.StartCoroutine(DeskUI.PopModal(modal));
    }

    public void Close() => root.SetActive(false);

    void BuildEvidence()
    {
        UiKit.Clear(evidenceContent);
        evidenceCards.Clear();
        selectedEvidence.RemoveWhere(id => !Game.Evidence.Has(id));
        var items = Game.Evidence.Items;
        if (items.Count == 0)
        {
            UiKit.Text(evidenceContent, "You haven't collected any evidence. The board will not be impressed.", T.bodySize, T.muted);
            return;
        }
        foreach (var item in items)
        {
            var c = EvidenceTray.EvidenceCard(evidenceContent, item, null);
            c.gameObject.AddComponent<Button>().onClick.AddListener(() => ToggleEvidence(item.Id));
            c.gameObject.AddComponent<ButtonPop>();
            evidenceCards[item.Id] = c;
        }
    }

    void PickSuspect(string id)
    {
        selectedSuspect = id;
        Refresh();
    }

    void ToggleEvidence(string id)
    {
        if (!selectedEvidence.Remove(id))
        {
            if (selectedEvidence.Count >= MaxEvidence) { ui.Toast("Max 3 pieces of evidence. Choose your strongest.", T.warning); return; }
            selectedEvidence.Add(id);
        }
        Refresh();
    }

    void ToggleJensenbot()
    {
        namesJensenbot = !namesJensenbot;
        Refresh();
    }

    void Refresh()
    {
        subtitle.text = forced
            ? $"<color=#{UiKit.Hex(T.danger)}><b>It's 09:00. The board is seated.</b></color> Pick what you have. They will take it as-is when time runs out."
            : "The CEO is tapping his leather sleeve. One shot: wrong names get people fired.";
        back.gameObject.SetActive(!forced);

        foreach (var kv in suspectCards)
        {
            var s = Game.Case.Suspect(kv.Key);
            kv.Value.color = kv.Key == selectedSuspect ? Color.Lerp(T.panelAlt, s.Color, 0.6f) : T.panelAlt;
        }
        foreach (var kv in evidenceCards)
            kv.Value.color = selectedEvidence.Contains(kv.Key) ? new Color(1f, 0.9f, 0.55f) : T.card;
        evidenceCount.text = $"{selectedEvidence.Count} / {MaxEvidence}";

        jensenToggle.SetLabel(namesJensenbot ? "[X] Jensenbot helped them" : "[ ] Jensenbot helped them");
        jensenToggle.image.color = namesJensenbot ? new Color(0.46f, 0.85f, 0.25f) : T.panelAlt;
        jensenToggle.GetComponentInChildren<TMP_Text>().color = namesJensenbot ? T.accentText : T.text;

        submit.interactable = forced || !string.IsNullOrEmpty(selectedSuspect);
    }

    public void SubmitNow(bool timedOut)
    {
        if (!root.activeSelf && !timedOut) return;
        var accusation = new Accusation
        {
            SuspectId = selectedSuspect,
            Evidence = selectedEvidence.Select(id => Game.Evidence.Get(id)).Where(e => e != null).ToList(),
            Motive = motive.text,
            NamesJensenbot = namesJensenbot,
            TimedOut = timedOut,
        };
        Close();
        Game.SubmitAccusation(accusation);
    }
}
