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
    readonly Dictionary<string, RawImage> headshots = new();
    readonly TMP_InputField motive;
    readonly Button jensenToggle, submit, back;
    readonly HashSet<string> selectedEvidence = new();
    readonly TextMeshProUGUI countdown;
    int lastSecond = -1;

    /// <summary>Called every frame by DeskUI: the board's patience, shown right in the dialog.</summary>
    public void Tick()
    {
        if (!root.activeSelf || !Game.Overtime) { if (countdown.text.Length > 0) countdown.text = ""; return; }
        int s = Mathf.CeilToInt(Mathf.Max(0, Game.GraceLeft));
        countdown.text = $"0:{s:00}";
        if (s != lastSecond)
        {
            lastSecond = s;
            UiFx.Punch(countdown.transform, s <= 10 ? 0.15f : 0.06f, 0.25f);
        }
    }
    readonly (Image bg, Image icon) slotMethod, slotWhy;
    readonly TextMeshProUGUI strengthText;

    (Image, Image) Slot(Transform parent, string title, string sub)
    {
        var bg = UiKit.Panel(parent, new Color(1, 1, 1, 0.06f), T.cardSprite, "Slot");
        bg.HLayout(10, 10);
        bg.Size(prefW: 330, prefH: 62);
        var icon = UiKit.Icon(bg.transform, "mark_x", 26, new Color(1, 1, 1, 0.3f));
        var col = UiKit.Rect("Col", bg.transform);
        col.VLayout(0);
        col.Size(flexW: 1);
        UiKit.Text(col, $"<b>{title}</b>", 16, T.text);
        UiKit.Text(col, sub, 15, T.muted);
        return (bg, icon);
    }

    void SetSlot((Image bg, Image icon) s, bool on)
    {
        s.icon.sprite = T.Icon(on ? "mark_check" : "mark_x");
        s.icon.color = on ? T.accent : new Color(1, 1, 1, 0.3f);
        s.bg.color = on ? new Color(T.accent.r, T.accent.g, T.accent.b, 0.2f) : new Color(1, 1, 1, 0.06f);
    }
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

        var titleRow = UiKit.Rect("TitleRow", card.transform);
        titleRow.HLayout(16);
        UiKit.Heading(titleRow, "Present your case to the board", 38).Size(flexW: 1);
        countdown = UiKit.Text(titleRow, "", 44, T.danger, TextAlignmentOptions.Right, FontStyles.Bold);
        countdown.Size(prefW: 220);
        if (T.headingFont != null) countdown.font = T.headingFont;
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
            av.Size(prefW: 72, prefH: 72, minW: 72);
            av.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var head = UiKit.Rect("Headshot", av.transform).Stretch().gameObject.AddComponent<RawImage>();
            head.raycastTarget = false;
            var tex = SuspectPortraits.Instance != null ? SuspectPortraits.Instance.Headshot(s.Id) : null;
            head.texture = tex;
            head.enabled = tex != null;
            headshots[s.Id] = head;
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

        // Case strength: a case holds up when it shows HOW the deck got out plus WHY or WHEN they did it.
        var strength = UiKit.Rect("Strength", card.transform);
        strength.HLayout(12);
        slotMethod = Slot(strength, "HOW IT GOT OUT", "Method");
        slotWhy = Slot(strength, "WHY OR WHEN", "Motive or Opportunity");
        strengthText = UiKit.Text(strength, "", T.smallSize, T.muted);
        strengthText.Size(flexW: 1);
        var evBg = UiKit.Panel(card.transform, new Color(0, 0, 0, 0.22f), T.panelSprite, "EvidenceBg");
        evBg.Size(flexH: 1);
        var scroll = UiKit.ScrollList(evBg.transform, out evidenceContent, 10, 12);
        ((RectTransform)scroll.transform).Stretch();
        var grid = evidenceContent.gameObject;
        Object.DestroyImmediate(grid.GetComponent<VerticalLayoutGroup>());
        var g = grid.AddComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(452, 168);
        g.spacing = new Vector2(12, 12);
        g.padding = new RectOffset(12, 12, 12, 12);

        // 3. Accomplice + motive
        var bottom = UiKit.Rect("Bottom", card.transform);
        bottom.HLayout(14);
        jensenToggle = UiKit.Button(bottom, "Jensenbot helped them", ToggleJensenbot, T.panelAlt, T.text, 56, 20).AddCheckBox(32);
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

    public bool IsOpen => root != null && root.activeSelf;

    public void Open(bool isForced)
    {
        ui.ClearToasts(); // the case-strength line already says what matters; no notifications over the modal
        forced = isForced;
        Sfx.Play(forced ? "crack" : "chime");
        if (SuspectPortraits.Instance != null)
            foreach (var kv in headshots)
            {
                kv.Value.texture = SuspectPortraits.Instance.Headshot(kv.Key);
                kv.Value.enabled = kv.Value.texture != null;
            }
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
        Sfx.Play("click");
        selectedSuspect = id;
        if (suspectCards.TryGetValue(id, out var picked)) UiFx.Punch(picked.transform, 0.06f, 0.25f);
        Refresh();
    }

    void ToggleEvidence(string id)
    {
        if (!selectedEvidence.Remove(id))
        {
            if (selectedEvidence.Count >= MaxEvidence) { ui.Toast("Three pieces, max. Pick your best ones.", T.warning); return; }
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
            ? $"<color=#{UiKit.Hex(T.danger)}><b>It's nine. They're sitting down.</b></color> Go with what you've got."
            : "Dex is drumming on the table. You get one shot, and a wrong name gets someone fired.";
        back.gameObject.SetActive(!forced);

        foreach (var kv in suspectCards)
        {
            var s = Game.Case.Suspect(kv.Key);
            kv.Value.color = kv.Key == selectedSuspect ? Color.Lerp(T.panelAlt, s.Color, 0.6f) : T.panelAlt;
        }
        foreach (var kv in evidenceCards)
            kv.Value.color = selectedEvidence.Contains(kv.Key) ? new Color(1f, 0.9f, 0.55f) : T.card;
        evidenceCount.text = $"{selectedEvidence.Count} / {MaxEvidence}";
        var chosen = selectedEvidence.Select(id => Game.Evidence.Get(id)).Where(e => e != null).ToList();
        // Judged for the suspect you picked: attached evidence must support them, and nothing you found may rule it out.
        string who = selectedSuspect;
        var sname = who != null ? Game.Case.Suspect(who).FirstName : null;
        bool Supported(params string[] slots) => who != null && Board.Covers(Game.Case, chosen, who, slots);
        bool Ruled(string slot) => who != null && Board.Status(Game.Case, Game.Evidence.Items, who, slot) == CellStatus.RuledOut;
        bool how = Supported("method") && !Ruled("method");
        bool why = (Supported("motive") && !Ruled("motive")) || (Supported("opportunity") && !Ruled("opportunity"));
        SetSlot(slotMethod, how);
        SetSlot(slotWhy, why);
        strengthText.text = who == null ? "Pick who you're accusing first."
            : how && why ? "<color=#" + UiKit.Hex(T.accent) + $"><b>That holds up against {sname}.</b></color>"
            : Ruled("method") ? $"Careful: what you found says {sname} couldn't have done it that way."
            : Ruled("opportunity") && !Supported("motive") ? $"Careful: {sname} wasn't in a position to do it."
            : chosen.Count == 0 ? $"Attach evidence that shows how {sname} got the deck out, and why or when."
            : !how ? $"Missing: something that shows <b>how</b> {sname} got the deck out."
            : $"Missing: something that shows <b>why</b> {sname} did it, or that they were <b>awake</b> at 02:14.";

        jensenToggle.SetChecked(namesJensenbot, T.danger);
        jensenToggle.image.color = namesJensenbot ? Color.Lerp(T.panelAlt, T.danger, 0.35f) : T.panelAlt;

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
