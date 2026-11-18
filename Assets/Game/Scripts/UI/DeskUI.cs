using System.Linq;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Root of the Leak Desk screen: top bar (clock, countdown, Accuse), left navigation, center app panels,
/// right evidence tray, toasts and modal overlays. Built entirely from code via UiKit.
/// </summary>
public class DeskUI : MonoBehaviour
{
    public enum Tab { Logs, Desks, Interrogate, Jensenbot }

    public LeakDeskGame Game { get; private set; }
    public Canvas Canvas { get; private set; }
    public FeedPanel Feed { get; private set; }
    public InterrogationPanel Interrogation { get; private set; }
    public JensenbotPanel JensenbotView { get; private set; }
    public DeskSearchPanel Desks { get; private set; }
    public EvidenceTray Tray { get; private set; }
    public AccusationOverlay Accusation { get; private set; }
    public Tab CurrentTab { get; private set; } = Tab.Logs;

    RectTransform overlayRoot;
    RectTransform centerRoot;
    Button muteButton;
    Image muteIcon;
    TextMeshProUGUI leadsHeader;
    readonly Dictionary<string, (Image bg, Image icon)> pips = new();
    readonly Dictionary<string, RawImage> boardHeads = new();
    public RectTransform BoardRect { get; private set; }
    public Button AccuseButton => accuseButton;
    public RectTransform NavRect(Tab tab) => navButtons.TryGetValue(tab, out var b) ? (RectTransform)b.transform : null;
    public Tutorial Tutorial { get; private set; }
    public TextMeshProUGUI ClockLabel => clockText;
    RectTransform toastRoot;
    TextMeshProUGUI clockText, countdownText;
    Image timeBar;
    Button accuseButton;
    readonly Dictionary<Tab, Button> navButtons = new();
    readonly Dictionary<Tab, TextMeshProUGUI> navLabels = new();
    readonly Dictionary<Tab, Image> navIcons = new();
    VerdictOverlay verdict;
    BriefingOverlay briefing;

    UiTheme T => UiKit.Theme;

    public static DeskUI Build(LeakDeskGame game)
    {
        var canvas = UiKit.CreateCanvas("LeakDeskCanvas");
        var ui = canvas.gameObject.AddComponent<DeskUI>();
        ui.Game = game;
        ui.Canvas = canvas;
        ui.BuildLayout();
        game.PhaseChanged += ui.OnPhaseChanged;
        game.WaveUnlocked += ui.OnWave;
        game.BoardChanged += ui.OnBoardChanged;
        canvas.gameObject.AddComponent<DeskDirector>();
        ui.Tutorial = canvas.gameObject.AddComponent<Tutorial>();
        return ui;
    }

    void BuildLayout()
    {
        var root = (RectTransform)Canvas.transform;

        // Soft vignette over the 3D office so the panels pop.
        var dim = UiKit.Panel(root, T.background, name: "Backdrop");
        ((RectTransform)dim.transform).Stretch();
        dim.raycastTarget = false;

        BuildTopBar(root);

        var body = UiKit.Rect("Body", root).Stretch(20, 124, 20, 20);
        body.HLayout(16);
        ((HorizontalLayoutGroup)body.GetComponent<HorizontalLayoutGroup>()).childForceExpandHeight = true;

        BuildNav(body);

        var center = UiKit.Rect("Center", body);
        centerRoot = center;
        center.Size(flexW: 1, flexH: 1);
        Feed = new FeedPanel(this, center);
        Interrogation = new InterrogationPanel(this, center);
        JensenbotView = new JensenbotPanel(this, center);
        Desks = new DeskSearchPanel(this, center);

        Tray = new EvidenceTray(this, body);

        toastRoot = UiKit.Rect("Toasts", root);
        toastRoot.anchorMin = toastRoot.anchorMax = new Vector2(1, 1);
        toastRoot.pivot = new Vector2(1, 1);
        toastRoot.anchoredPosition = new Vector2(-24, -128);
        toastRoot.sizeDelta = new Vector2(520, 0);
        toastRoot.VLayout(8, 0, TextAnchor.UpperRight);
        toastRoot.FitHeight();

        overlayRoot = UiKit.Rect("Overlays", root).Stretch();
        Accusation = new AccusationOverlay(this, overlayRoot);
        verdict = new VerdictOverlay(this, overlayRoot);
        briefing = new BriefingOverlay(this, overlayRoot);

        toastRoot.SetAsLastSibling(); // notifications stay readable above modals
        ShowTab(Tab.Logs);
    }

    void BuildTopBar(RectTransform root)
    {
        var bar = UiKit.Panel(root, T.panel, T.panelSprite, "TopBar");
        var rt = (RectTransform)bar.transform;
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.offsetMin = new Vector2(20, -108);
        rt.offsetMax = new Vector2(-20, -16);
        bar.HLayout(24, 18);

        var brandRow = UiKit.Rect("BrandRow", bar.transform);
        brandRow.HLayout(14);
        brandRow.Size(prefW: 540);
        var badge = UiKit.Panel(brandRow, T.accent, T.buttonSprite, "Badge");
        badge.Size(prefW: 60, prefH: 60, minW: 60);
        ((RectTransform)UiKit.Icon(UiKit.Rect("H", badge.transform).Stretch(12, 10, 12, 14), "thunder", 34, T.accentText).transform).Stretch();
        var brand = UiKit.Rect("Brand", brandRow);
        brand.VLayout(0);
        brand.Size(flexW: 1);
        UiKit.Text(brand, $"<color=#{UiKit.Hex(T.accent)}><b>{Game.Case.Company.ToUpperInvariant()}</b></color> // SECURITY DESK", 30,
            style: FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
        UiKit.Text(brand, "Internal investigations unit", T.smallSize, T.muted);

        var title = UiKit.Rect("CaseTitle", bar.transform);
        title.VLayout(0);
        title.Size(flexW: 1);
        foreach (var t in new[]
                 {
                     UiKit.Text(title, $"CASE: {Game.Case.Title.ToUpperInvariant()}", 24, T.text, style: FontStyles.Bold),
                     UiKit.Text(title, Game.Case.Tagline, T.smallSize, T.muted),
                 })
        {
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
        }

        var clock = UiKit.Rect("Clock", bar.transform);
        clock.VLayout(4);
        clock.Size(prefW: 330);
        var clockRow = UiKit.Rect("ClockRow", clock);
        clockRow.HLayout(12);
        UiKit.Icon(clockRow, "hourglass", 30, T.muted);
        clockText = UiKit.Text(clockRow, "08:48", 40, T.text, TextAlignmentOptions.Left, FontStyles.Bold);
        clockText.textWrappingMode = TextWrappingModes.NoWrap;
        clockText.Size(prefW: 140);
        countdownText = UiKit.Text(clockRow, "12:00 left", T.smallSize, T.muted, TextAlignmentOptions.Left);
        countdownText.Size(flexW: 1);
        timeBar = UiKit.Bar(clock, T.accent, 10);

        muteButton = UiKit.Button(bar.transform, "", ToggleMute, T.panelAlt, T.text, 64);
        muteButton.Size(prefW: 64, prefH: 64);
        Destroy(muteButton.GetComponentInChildren<TMP_Text>().gameObject);
        muteIcon = UiKit.Icon(muteButton.transform, "sound_on", 30, T.text);
        var mr = (RectTransform)muteIcon.transform;
        mr.anchorMin = mr.anchorMax = new Vector2(0.5f, 0.55f);
        mr.sizeDelta = new Vector2(30, 30);

        accuseButton = UiKit.Button(bar.transform, "ACCUSE", () => { Sfx.Play("click"); Game.OpenAccusation(false); }, T.danger, Color.white, 64, 26);
        accuseButton.Size(prefW: 220, prefH: 64);
    }

    void BuildNav(RectTransform body)
    {
        var nav = UiKit.Panel(body, T.panel, T.panelSprite, "Nav");
        nav.VLayout(12, 14);
        nav.Size(prefW: 200, flexH: 1);

        AddNav(nav.transform, Tab.Logs, "folder", "Logs");
        AddNav(nav.transform, Tab.Desks, "find", "Desks");
        AddNav(nav.transform, Tab.Interrogate, "mic", "Interrogate");
        AddNav(nav.transform, Tab.Jensenbot, "brain", "Jensenbot");
        UiKit.Spacer(nav.transform, 0, 1);
        // SUSPECT BOARD: why / when / how for each suspect. Click a pip to see what you've got on it.
        var card = UiKit.Card(nav.transform, new Color(0, 0, 0, 0.25f), 10, 6, "Board");
        BoardRect = (RectTransform)card.transform;
        leadsHeader = UiKit.Text(card.transform, "SUSPECT BOARD", 14, T.accent, style: FontStyles.Bold);
        var head = UiKit.Rect("Head", card.transform);
        head.HLayout(6);
        UiKit.Spacer(head, 0).Size(prefW: 38);
        foreach (var slot in Board.Slots)
        {
            var h = UiKit.Text(head, Board.Short(slot), 15, T.muted, TextAlignmentOptions.Center, FontStyles.Bold);
            h.textWrappingMode = TextWrappingModes.NoWrap;
            h.overflowMode = TextOverflowModes.Overflow;
            h.Size(prefW: 38);
        }
        foreach (var row in Game.Case.Board.Rows) AddBoardRow(card.transform, row.Suspect);
        var ai = UiKit.Rect("AiRow", card.transform);
        ai.HLayout(6);
        var aiLabel = UiKit.Text(ai, "AI helped?", 15, T.text, TextAlignmentOptions.Left, FontStyles.Bold);
        aiLabel.Size(prefW: 38 * 3 + 12);
        aiLabel.textWrappingMode = TextWrappingModes.NoWrap;
        Pip(ai, null, "ai");
    }

    void AddBoardRow(Transform parent, string suspectId)
    {
        var sd = Game.Case.Suspect(suspectId);
        var row = UiKit.Rect("Row: " + suspectId, parent);
        row.HLayout(6);
        var av = UiKit.Panel(row, sd.Color, UiKit.AvatarSprite, "Avatar");
        av.Size(prefW: 38, prefH: 38, minW: 38);
        av.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        var raw = UiKit.Rect("Head", av.transform).Stretch().gameObject.AddComponent<RawImage>();
        raw.raycastTarget = false;
        raw.enabled = false;
        boardHeads[suspectId] = raw;
        foreach (var slot in Board.Slots) Pip(row, suspectId, slot);
    }

    void Pip(Transform parent, string suspectId, string slot)
    {
        var bg = UiKit.Panel(parent, new Color(1, 1, 1, 0.08f), UiKit.AvatarSprite, "Pip");
        bg.Size(prefW: 38, prefH: 38, minW: 38);
        var holder = UiKit.Rect("H", bg.transform).Stretch(9, 9, 9, 9);
        var icon = UiKit.Icon(holder, "mark_question", 20, new Color(1, 1, 1, 0.3f));
        ((RectTransform)icon.transform).Stretch();
        bg.gameObject.AddComponent<Button>().onClick.AddListener(() => ExplainPip(suspectId, slot));
        bg.gameObject.AddComponent<ButtonPop>();
        pips[(suspectId ?? "ai") + ":" + slot] = (bg, icon);
    }

    void ExplainPip(string suspectId, string slot)
    {
        Sfx.Play("click");
        if (suspectId == null)
        {
            bool found = Board.AiFound(Game.Case, Game.Evidence.Items, Game.Jensenbot.Exposed);
            Toast(found ? "<b>AI helped?</b> You've got evidence that someone (or something) helped from the inside."
                        : "<b>AI helped?</b> Nothing yet. Does the company assistant's story match the raw logs?", found ? T.accent : T.secondary, "brain");
            return;
        }
        var sd = Game.Case.Suspect(suspectId);
        var cell = Board.Row(Game.Case, suspectId).Cell(slot);
        var st = Board.Status(Game.Case, Game.Evidence.Items, suspectId, slot);
        var support = Game.Evidence.Items.FirstOrDefault(i => Board.Supports(cell, i));
        string msg = st switch
        {
            CellStatus.RuledOut => $"<b>{sd.FirstName} · {Board.Long(slot)}: doesn't add up.</b> {cell.AgainstNote}",
            CellStatus.Supported => $"<b>{sd.FirstName} · {Board.Long(slot)}:</b> {support?.Title}.",
            _ => $"<b>{sd.FirstName} · {Board.Long(slot)}:</b> nothing yet.",
        };
        Toast(msg, st == CellStatus.RuledOut ? T.danger : st == CellStatus.Supported ? T.accent : T.muted, "find");
    }

    public void UpdateProgress()
    {
        foreach (var row in Game.Case.Board.Rows)
        {
            if (boardHeads.TryGetValue(row.Suspect, out var raw) && !raw.enabled && SuspectPortraits.Instance != null)
            {
                raw.texture = SuspectPortraits.Instance.Headshot(row.Suspect);
                raw.enabled = raw.texture != null;
            }
            foreach (var slot in Board.Slots)
                SetPip(row.Suspect + ":" + slot, Board.Status(Game.Case, Game.Evidence.Items, row.Suspect, slot));
        }
        SetPip("ai:ai", Board.AiFound(Game.Case, Game.Evidence.Items, Game.Jensenbot.Exposed) ? CellStatus.Supported : CellStatus.Unknown);
        var (done, total) = Board.Progress(Game.Case, Game.Evidence.Items, Game.Jensenbot.Exposed);
        leadsHeader.text = $"SUSPECT BOARD  {done}/{total}";
    }

    void SetPip(string key, CellStatus st)
    {
        if (!pips.TryGetValue(key, out var p)) return;
        p.icon.sprite = T.Icon(st switch { CellStatus.Supported => "mark_check", CellStatus.RuledOut => "mark_x", _ => "mark_question" });
        p.icon.color = st == CellStatus.Unknown ? new Color(1, 1, 1, 0.3f) : Color.white;
        p.bg.color = st switch
        {
            CellStatus.Supported => T.accent,
            CellStatus.RuledOut => new Color(0.75f, 0.25f, 0.32f),
            _ => new Color(1, 1, 1, 0.08f),
        };
    }

    void OnBoardChanged(string suspectId, string slot, CellStatus st)
    {
        if (st == CellStatus.Unknown) return;
        if (suspectId == null)
        {
            Toast("<b>New on the board:</b> someone had help from the inside.", T.accent, "brain");
        }
        else
        {
            var sd = Game.Case.Suspect(suspectId);
            var cell = Board.Row(Game.Case, suspectId).Cell(slot);
            if (st == CellStatus.RuledOut) Toast($"<b>{sd.FirstName}'s {Board.Long(slot)} doesn't add up.</b>\n{cell.AgainstNote}", T.danger, "mark_x");
            else Toast($"<b>New on the board:</b> {sd.FirstName} · {Board.Long(slot)}", T.accent, "mark_check");
        }
        Sfx.Play("unlock", st == CellStatus.RuledOut ? 0.8f : 1.2f);
        if (pips.TryGetValue((suspectId ?? "ai") + ":" + slot, out var p)) UiFx.Punch(p.bg.transform, 0.3f, 0.45f);
    }

    void ToggleMute()
    {
        Sfx.Muted = !Sfx.Muted;
        muteIcon.sprite = T.Icon(Sfx.Muted ? "sound_off" : "sound_on");
        if (!Sfx.Muted) Sfx.Play("click");
    }

    void AddNav(Transform parent, Tab tab, string icon, string label)
    {
        var b = UiKit.Button(parent, "", () => { Sfx.Play("click"); ShowTab(tab); }, T.panelAlt, T.text, 92, 21);
        navButtons[tab] = b;
        Destroy(b.GetComponentInChildren<TMP_Text>().gameObject);
        b.image.VLayout(4, 12, TextAnchor.MiddleCenter);
        var iconRow = UiKit.Rect("IconRow", b.transform);
        iconRow.HLayout(0, 0, TextAnchor.MiddleCenter);
        navIcons[tab] = UiKit.Icon(iconRow, icon, 34, T.text);
        var t = UiKit.Text(b.transform, label, 20, T.text, TextAlignmentOptions.Center, FontStyles.Bold);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        navLabels[tab] = t;
    }

    public void ShowTab(Tab tab)
    {
        CurrentTab = tab;
        Feed.SetVisible(tab == Tab.Logs);
        Desks.SetVisible(tab == Tab.Desks);
        Interrogation.SetVisible(tab == Tab.Interrogate);
        JensenbotView.SetVisible(tab == Tab.Jensenbot);
        foreach (var kv in navButtons)
        {
            bool on = kv.Key == tab;
            kv.Value.image.color = on ? T.accent : T.panelAlt;
            navLabels[kv.Key].color = on ? T.accentText : T.text;
            navIcons[kv.Key].color = on ? T.accentText : T.text;
        }
        Tray.Refresh();
        // Fade + slide the newly shown app in.
        foreach (Transform child in centerRoot)
        {
            if (!child.gameObject.activeSelf) continue;
            var cg = child.GetComponent<CanvasGroup>() ?? child.gameObject.AddComponent<CanvasGroup>();
            UiFx.FadeIn(cg, 0.2f, -16f);
        }
        if (navButtons.TryGetValue(tab, out var nb)) UiFx.Punch(nb.transform, 0.06f, 0.25f);
    }

    void OnWave(CaseWave wave)
    {
        Toast($"<b>NEW: {wave.Title}</b>\n{wave.Message}", T.secondary, "folder");
        StartCoroutine(PulseNav(Tab.Logs));
        if (wave.Suspects.Count > 0) StartCoroutine(PulseNav(Tab.Interrogate));
    }

    IEnumerator PulseNav(Tab tab)
    {
        var t = navButtons[tab].transform;
        for (int i = 0; i < 3; i++)
        {
            for (float k = 0; k < 1; k += Time.unscaledDeltaTime * 5)
            {
                float s = 1 + Mathf.Sin(k * Mathf.PI) * 0.08f;
                t.localScale = new Vector3(s, s, 1);
                yield return null;
            }
        }
        t.localScale = Vector3.one;
    }

    void Update()
    {
        if (Game == null || Game.Case == null) return;
        Feed.Tick();
        int unread = Feed.UnreadCount;
        navLabels[Tab.Logs].text = unread > 0 ? $"Logs <color=#{Hex(CurrentTab == Tab.Logs ? T.accentText : T.warning)}>({unread})</color>" : "Logs";
        clockText.text = Game.ClockText;
        countdownText.text = Game.CountdownText;
        float left = Game.RoundSeconds <= 0 ? 0 : Game.TimeLeft / Game.RoundSeconds;
        UiKit.SetBar(timeBar, left);
        var c = left > 0.33f ? T.accent : left > 0.12f ? T.warning : T.danger;
        timeBar.color = c;
        countdownText.color = Game.Overtime ? T.danger : T.muted;
        if (Game.Overtime || left < 0.12f)
            clockText.color = Color.Lerp(T.text, T.danger, Mathf.PingPong(Time.time * 2f, 1f));
    }

    void OnPhaseChanged()
    {
        accuseButton.interactable = Game.Phase == GamePhase.Investigating;
    }

    // ---------- overlays ----------

    public void ShowBriefing() => briefing.Show();
    public void ShowDeliberation(Accusation a) { Accusation.Close(); verdict.ShowDeliberating(a); }
    public void ShowVerdict(Accusation a, Verdict v) => verdict.ShowResult(a, v);

    public RectTransform OverlayRoot => overlayRoot;

    static string Hex(Color c) => UiKit.Hex(c);

    // ---------- toasts ----------

    /// <summary>Phone-style notification that slides in at the top right.</summary>
    public void Toast(string message, Color? color = null, string icon = "bell_1")
    {
        if (Tutorial != null && Tutorial.Active) return; // Rosa is talking; no competing notifications
        var c = color ?? T.accent;
        // Never stack more than 3: the oldest one leaves early.
        while (toastRoot.childCount >= 3) UiKit.Remove(toastRoot.GetChild(0));
        var holder = UiKit.Rect("Toast", toastRoot);
        holder.Size(prefW: 500);
        holder.VLayout(0);
        var card = UiKit.Panel(holder, new Color(0.11f, 0.08f, 0.24f, 0.97f), T.cardSprite, "Card");
        card.HLayout(14, 14);
        var ib = UiKit.Panel(card.transform, c, UiKit.AvatarSprite, "IconBg");
        ib.Size(prefW: 46, prefH: 46, minW: 46);
        var ih = UiKit.Rect("H", ib.transform).Stretch(11, 11, 11, 11);
        ((RectTransform)UiKit.Icon(ih, icon, 24, new Color(0.1f, 0.08f, 0.2f)).transform).Stretch();
        var t = UiKit.Text(card.transform, message, T.smallSize, T.text);
        t.Size(flexW: 1);
        var stripe = UiKit.Panel(card.transform, c, T.pillSprite, "Stripe").Pill(6);
        stripe.Size(prefW: 6, prefH: 40);
        StartCoroutine(ToastLife(holder.gameObject, (RectTransform)card.transform));
    }

    IEnumerator ToastLife(GameObject go, RectTransform card)
    {
        var cg = go.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        yield return null; // let the layout place it
        if (go == null) yield break;
        Vector2 basePos = card.anchoredPosition;
        for (float t = 0; t < 0.28f && go != null; t += Time.unscaledDeltaTime)
        {
            float k = 1 - Mathf.Pow(1 - t / 0.28f, 3);
            cg.alpha = k;
            card.anchoredPosition = basePos + new Vector2(80f * (1 - k), 0);
            yield return null;
        }
        if (go == null) yield break;
        cg.alpha = 1;
        card.anchoredPosition = basePos;
        yield return new WaitForSecondsRealtime(3.6f);
        for (float t = 0; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            if (go == null) yield break;
            cg.alpha = 1 - t / 0.35f;
            card.anchoredPosition = basePos + new Vector2(60f * t / 0.35f, 0);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    /// <summary>Full-screen dimmer + centered modal card. Returns the card (vertical layout).</summary>
    public static Image Modal(Transform overlayRoot, string name, float width, float height, out GameObject root)
    {
        var dim = UiKit.Panel(overlayRoot, new Color(0.03f, 0.02f, 0.08f, 0.88f), name: name);
        ((RectTransform)dim.transform).Stretch();
        root = dim.gameObject;

        var card = UiKit.Panel(dim.transform, UiKit.Theme.panel, UiKit.Theme.panelSprite, "Modal");
        var rt = (RectTransform)card.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        card.VLayout(16, 32);
        // The frame sprite is semi-transparent in the middle; a second layer makes modals properly opaque.
        var backing = UiKit.Panel(card.transform, UiKit.Theme.panel, UiKit.Theme.panelSprite, "Backing");
        backing.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        ((RectTransform)backing.transform).Stretch();
        backing.raycastTarget = false;
        backing.transform.SetAsFirstSibling();
        return card;
    }

    public static IEnumerator PopModal(Transform modal)
    {
        float time = 0f;
        while (modal != null && time < 0.25f)
        {
            time += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(time / 0.25f);
            float s = 0.9f + 0.1f * (1f - Mathf.Pow(1f - k, 3f)) + Mathf.Sin(k * Mathf.PI) * 0.03f;
            modal.localScale = new Vector3(s, s, 1);
            yield return null;
        }
        if (modal != null) modal.localScale = Vector3.one;
    }
}
