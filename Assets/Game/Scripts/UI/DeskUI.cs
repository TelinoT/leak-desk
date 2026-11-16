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

        var brand = UiKit.Rect("Brand", bar.transform);
        brand.VLayout(0);
        brand.Size(prefW: 500);
        UiKit.Text(brand, $"<color=#{UiKit.Hex(T.accent)}><b>{Game.Case.Company.ToUpperInvariant()}</b></color> // SECURITY DESK", 30,
            style: FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
        UiKit.Text(brand, "Internal investigations unit", T.smallSize, T.muted);

        var title = UiKit.Rect("CaseTitle", bar.transform);
        title.VLayout(0);
        title.Size(flexW: 1);
        UiKit.Text(title, $"CASE: {Game.Case.Title.ToUpperInvariant()}", 24, T.text, style: FontStyles.Bold);
        UiKit.Text(title, Game.Case.Tagline, T.smallSize, T.muted);

        var clock = UiKit.Rect("Clock", bar.transform);
        clock.VLayout(4);
        clock.Size(prefW: 300);
        var clockRow = UiKit.Rect("ClockRow", clock);
        clockRow.HLayout(12);
        clockText = UiKit.Text(clockRow, "08:48", 40, T.text, TextAlignmentOptions.Left, FontStyles.Bold);
        clockText.textWrappingMode = TextWrappingModes.NoWrap;
        clockText.Size(prefW: 140);
        countdownText = UiKit.Text(clockRow, "12:00 left", T.smallSize, T.muted, TextAlignmentOptions.Left);
        countdownText.Size(flexW: 1);
        timeBar = UiKit.Bar(clock, T.accent, 10);

        accuseButton = UiKit.Button(bar.transform, "ACCUSE", () => Game.OpenAccusation(false), T.danger, Color.white, 64, 26);
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
        var help = UiKit.Text(nav.transform, "Board meeting at 09:00.\nName the leaker, attach proof.", T.smallSize - 2, T.muted,
            TextAlignmentOptions.Center);
        help.Size(prefH: 80);
    }

    void AddNav(Transform parent, Tab tab, string icon, string label)
    {
        var b = UiKit.Button(parent, "", () => { Sfx.Play("click"); ShowTab(tab); }, T.panelAlt, T.text, 112, 21);
        navButtons[tab] = b;
        Destroy(b.GetComponentInChildren<TMP_Text>().gameObject);
        b.image.VLayout(4, 12, TextAnchor.MiddleCenter);
        var iconRow = UiKit.Rect("IconRow", b.transform);
        iconRow.HLayout(0, 0, TextAnchor.MiddleCenter);
        navIcons[tab] = UiKit.Icon(iconRow, icon, 40, T.text);
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
    }

    void OnWave(CaseWave wave)
    {
        Toast($"<b>NEW: {wave.Title}</b>\n{wave.Message}", T.secondary);
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
    public void ShowDeliberation() { Accusation.Close(); verdict.ShowDeliberating(); }
    public void ShowVerdict(Accusation a, Verdict v) => verdict.ShowResult(a, v);

    public RectTransform OverlayRoot => overlayRoot;

    static string Hex(Color c) => UiKit.Hex(c);

    // ---------- toasts ----------

    public void Toast(string message, Color? color = null)
    {
        var card = UiKit.Card(toastRoot, T.card, 14, 0, "Toast");
        card.Size(prefW: 480);
        var row = UiKit.Rect("Row", card.transform);
        row.HLayout(10);
        var dot = UiKit.Panel(row, color ?? T.accent, UiKit.AvatarSprite, "Dot");
        dot.Size(prefW: 14, prefH: 14, minW: 14);
        var t = UiKit.Text(row, message, T.smallSize + 1, T.cardText);
        t.Size(flexW: 1);
        StartCoroutine(ToastLife(card.gameObject));
    }

    IEnumerator ToastLife(GameObject go)
    {
        var cg = go.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        for (float t = 0; t < 0.2f; t += Time.unscaledDeltaTime) { cg.alpha = t / 0.2f; yield return null; }
        cg.alpha = 1;
        yield return new WaitForSecondsRealtime(3.2f);
        for (float t = 0; t < 0.4f; t += Time.unscaledDeltaTime) { if (go == null) yield break; cg.alpha = 1 - t / 0.4f; yield return null; }
        if (go != null) Destroy(go);
    }

    /// <summary>Full-screen dimmer + centered modal card. Returns the card (vertical layout).</summary>
    public static Image Modal(Transform overlayRoot, string name, float width, float height, out GameObject root)
    {
        var dim = UiKit.Panel(overlayRoot, new Color(0.03f, 0.04f, 0.08f, 0.78f), name: name);
        ((RectTransform)dim.transform).Stretch();
        root = dim.gameObject;

        var card = UiKit.Panel(dim.transform, UiKit.Theme.panel, UiKit.Theme.panelSprite, "Modal");
        var rt = (RectTransform)card.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        card.VLayout(16, 32);
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
