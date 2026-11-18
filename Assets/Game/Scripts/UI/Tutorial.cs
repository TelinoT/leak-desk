using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Guided first minute. Rosa Vance, Head of Security, walks the player through the real case step by step
/// (open a log, flag a line, question Jules, drag evidence onto him, then a tour of Desks, Jensenbot and ACCUSE).
/// The round clock is paused; a spotlight dims everything except the thing to click. Skippable, and remembered.
/// </summary>
public class Tutorial : MonoBehaviour
{
    const string PrefKey = "leakdesk_tutorial_done";
    const string GuideId = "guide";

    /// <summary>True the first time someone plays (cleared once finished or skipped).</summary>
    public static bool ShouldRun => PlayerPrefs.GetInt(PrefKey, 0) == 0;

    public static void Reset() => PlayerPrefs.DeleteKey(PrefKey);

    public static void MarkSeen()
    {
        PlayerPrefs.SetInt(PrefKey, 1);
        PlayerPrefs.Save();
    }

    class Step
    {
        public string Text;
        public Func<RectTransform> Target;
        public Func<RectTransform> Target2;
        public Func<bool> Done;
        public string Button;
        public bool Block = true;
        public bool CoachTop;
        public Action OnEnter;
    }

    public bool Active { get; private set; }

    DeskUI ui;
    RectTransform root, coach;
    Image[] shades;
    Image outline, outline2;
    TextMeshProUGUI coachText, stepLabel;
    Button nextButton;
    RawImage guideHead;
    List<Step> steps;
    int index;
    bool advancing;

    UiTheme T => UiKit.Theme;
    LeakDeskGame Game => ui.Game;
    SuspectAgent Theo => Game.Agent("theo");

    void Awake() => ui = GetComponent<DeskUI>();

    public void Begin()
    {
        if (root == null) Build();
        steps = Script();
        Game.Paused = true;
        Active = true;
        root.gameObject.SetActive(true);
        root.SetAsLastSibling();
        index = -1;
        Next();
    }

    string GuideVoice => Game.Case.GuideVoice;
    AudioClip guideClip;

    // Rosa waits for a suspect to finish their line before she moves on (so she doesn't talk over them).
    bool SomeoneElseTalking => ui.Interrogation.Voicing || (Voice.Current != null && Voice.Current != guideClip);

    /// <summary>Starts loading Rosa's lines while the player reads the briefing.</summary>
    public void PrefetchVoice() => Voice.Prefetch(GuideVoice, Script().ConvertAll(s => s.Text), 1.08f);

    List<Step> Script() => new()
    {
        new Step
        {
            Text = "Morning. Rosa, I run security here. Before Dex starts shouting, let me show you around. I've stopped the clock for now.",
            Button = "Okay",
        },
        new Step
        {
            Text = "Last night's logs come in here. Start with <b>#keynote</b>, the team channel for the launch.",
            OnEnter = () => ui.ShowTab(DeskUI.Tab.Logs),
            Target = () => ui.Feed.DocCard("doc_slack_keynote"),
            Done = () => ui.CurrentTab == DeskUI.Tab.Logs && ui.Feed.CurrentDocId == "doc_slack_keynote",
        },
        new Step
        {
            Text = "Theo from engineering, taking a shot at the slides. Probably nothing, but you never know. <b>Flag</b> it.",
            Target = () => ui.Feed.LineRow("doc_slack_keynote", 1),
            Done = () => Game.Evidence.Has("e_theo_jab"),
        },
        new Step
        {
            Text = "Whatever you flag ends up in this tray. Same with stuff from desks or anything people admit to.",
            Target = () => ui.Tray.Root,
            Button = "Okay",
        },
        new Step
        {
            Text = "This board is who could have done it: <b>why</b>, <b>when</b> and <b>how</b>. Evidence fills it in, and some of it rules people out. Only one person will check every box.",
            Target = () => ui.BoardRect,
            Button = "Okay",
        },
        new Step
        {
            Text = "Right. Let's go talk to someone. <b>Interrogate</b>.",
            Target = () => ui.NavRect(DeskUI.Tab.Interrogate),
            Done = () => ui.CurrentTab == DeskUI.Tab.Interrogate,
        },
        new Step
        {
            Text = "Let's start with Theo. Ask him something. Click one of those, or just type.",
            OnEnter = () => ui.Interrogation.Open("theo"),
            Target = () => ui.Interrogation.QuickRow,
            Done = () => Theo != null && Theo.QuestionsAsked > 0 && !Theo.Busy,
        },
        new Step
        {
            Text = "Now <b>drag his message</b> from your tray onto him. Keep an eye on his stress.",
            OnEnter = () => ui.Interrogation.Open("theo"),
            Target = () => ui.Tray.FirstCard,
            Target2 = () => ui.Interrogation.StageRect,
            Block = false,
            CoachTop = true,
            Done = () => Theo != null && !Theo.Busy && Theo.Log.Any(e => e.Type == ChatEntry.Kind.Evidence),
        },
        new Step
        {
            Text = "You can go through people's <b>desks</b> too. Each one takes half a minute, so don't search everything.",
            Target = () => ui.NavRect(DeskUI.Tab.Desks),
            Button = "Next",
        },
        new Step
        {
            Text = "That's <b>Jensenbot</b>. It'll summarize logs for you. I'd still read them myself.",
            Target = () => ui.NavRect(DeskUI.Tab.Jensenbot),
            Button = "Next",
        },
        new Step
        {
            Text = "When you know who it was, hit <b>Accuse</b>. One name, up to three pieces of evidence, and why they did it. Board's at nine.",
            Target = () => (RectTransform)ui.AccuseButton.transform,
            Button = "Start the clock",
        },
    };

    // ---------- building ----------

    void Build()
    {
        root = UiKit.Rect("Tutorial", ui.OverlayRoot).Stretch();

        shades = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            shades[i] = UiKit.Panel(root, new Color(0.02f, 0.01f, 0.07f, 0.72f), name: "Shade");
            var rt = (RectTransform)shades[i].transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = Vector2.zero;
        }

        outline = MakeOutline("Outline");
        outline2 = MakeOutline("Outline2");

        var card = UiKit.Panel(root, T.panel, T.panelSprite, "Coach");
        coach = (RectTransform)card.transform;
        coach.anchorMin = coach.anchorMax = new Vector2(0.5f, 0.5f);
        coach.sizeDelta = new Vector2(860, 210);
        var backing = UiKit.Panel(card.transform, T.panel, T.panelSprite, "Backing");
        ((RectTransform)backing.transform).Stretch();
        card.HLayout(20, 22, TextAnchor.UpperLeft);

        var av = UiKit.Panel(card.transform, SourceStyle.Color("calendar"), UiKit.AvatarSprite, "Avatar");
        av.Size(prefW: 110, prefH: 110, minW: 110);
        av.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        guideHead = UiKit.Rect("Head", av.transform).Stretch().gameObject.AddComponent<RawImage>();
        guideHead.raycastTarget = false;
        guideHead.enabled = false;

        var col = UiKit.Rect("Col", card.transform);
        col.VLayout(8);
        col.Size(flexW: 1);
        UiKit.Text(col, $"<color=#{UiKit.Hex(T.accent)}><b>ROSA VANCE</b></color>  ·  Head of Security", T.smallSize, T.muted);
        coachText = UiKit.Text(col, "", T.bodySize + 1, T.text);
        coachText.Size(flexH: 1);
        var row = UiKit.Rect("Row", col);
        row.HLayout(12);
        stepLabel = UiKit.Text(row, "", T.smallSize - 1, T.muted);
        stepLabel.Size(flexW: 1);
        var skip = UiKit.Button(row, "Skip tutorial", Finish, new Color(1, 1, 1, 0.08f), T.muted, 44, 16);
        skip.Size(prefW: 170, prefH: 44);
        nextButton = UiKit.Button(row, "Next", () => Advance(), T.accent, T.accentText, 44, 18);
        nextButton.Size(prefW: 210, prefH: 44);

        backing.transform.SetAsFirstSibling();
        backing.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        root.gameObject.SetActive(false);
    }

    Image MakeOutline(string name)
    {
        var img = UiKit.Panel(root, T.warning, T.outlineSprite != null ? T.outlineSprite : T.panelSprite, name);
        img.raycastTarget = false;
        if (T.outlineSprite == null) img.color = new Color(1f, 0.8f, 0.2f, 0.25f);
        var rt = (RectTransform)img.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        return img;
    }

    // ---------- flow ----------

    void Next()
    {
        index++;
        if (index >= steps.Count) { Finish(); return; }
        var s = steps[index];
        s.OnEnter?.Invoke();
        coachText.text = s.Text;
        guideClip = Voice.Cached(GuideVoice, s.Text);
        Voice.TypeAlong(coachText, guideClip, 120f, 1.25f);
        stepLabel.text = $"{index + 1} / {steps.Count}";
        nextButton.gameObject.SetActive(!string.IsNullOrEmpty(s.Button));
        if (!string.IsNullOrEmpty(s.Button)) nextButton.SetLabel(s.Button);
        UiFx.Punch(coach, 0.04f, 0.25f);
        Sfx.Play("chime", 1.2f, 0.5f);
        advancing = false;

        if (guideHead != null && !guideHead.enabled && SuspectPortraits.Instance != null)
        {
            var tex = SuspectPortraits.Instance.Headshot(GuideId);
            guideHead.texture = tex;
            guideHead.enabled = tex != null;
        }
    }

    void Advance()
    {
        if (advancing) return;
        Sfx.Play("click");
        Next();
    }

    void Finish()
    {
        if (!Active) return;
        Active = false;
        PlayerPrefs.SetInt(PrefKey, 1);
        PlayerPrefs.Save();
        root.gameObject.SetActive(false);
        Game.Paused = false;
        ui.Toast("Clock's running. Board meets at 09:00.", T.accent, "hourglass");
        Sfx.Play("unlock");
    }

    void Update()
    {
        if (!Active || steps == null || index < 0 || index >= steps.Count) return;
        var s = steps[index];

        var target = s.Target?.Invoke();
        var target2 = s.Target2?.Invoke();
        Rect? hole = target != null && target.gameObject.activeInHierarchy ? LocalRect(target, 10f) : null;
        Rect? hole2 = target2 != null && target2.gameObject.activeInHierarchy ? LocalRect(target2, 10f) : null;

        PlaceShades(hole, s.Block);
        PlaceOutline(outline, hole);
        PlaceOutline(outline2, hole2);
        PlaceCoach(hole, s.CoachTop);
        root.SetAsLastSibling();

        if (s.Done != null && !advancing && !SomeoneElseTalking && s.Done())
        {
            advancing = true;
            StartCoroutine(AdvanceSoon());
        }
    }

    IEnumerator AdvanceSoon()
    {
        Sfx.Play("unlock", 1.1f, 0.6f);
        yield return new WaitForSecondsRealtime(0.6f);
        if (Active) Next();
    }

    // ---------- spotlight geometry ----------

    Rect LocalRect(RectTransform t, float pad)
    {
        var corners = new Vector3[4];
        t.GetWorldCorners(corners);
        Vector2 min = root.InverseTransformPoint(corners[0]);
        Vector2 max = root.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect(min.x - pad, min.y - pad, max.x + pad, max.y + pad);
    }

    void PlaceShades(Rect? hole, bool block)
    {
        var r = root.rect;
        foreach (var sh in shades)
        {
            sh.raycastTarget = block;
            sh.color = new Color(0.02f, 0.01f, 0.07f, block ? 0.9f : 0.45f); // linear color space: needs high alpha to read as dark
        }
        if (hole == null)
        {
            Set(shades[0], r.xMin, r.yMin, r.width, r.height);
            for (int i = 1; i < 4; i++) Set(shades[i], 0, 0, 0, 0);
            return;
        }
        var h = hole.Value;
        Set(shades[0], r.xMin, r.yMin, h.xMin - r.xMin, r.height);           // left
        Set(shades[1], h.xMax, r.yMin, r.xMax - h.xMax, r.height);           // right
        Set(shades[2], h.xMin, h.yMax, h.width, r.yMax - h.yMax);            // top
        Set(shades[3], h.xMin, r.yMin, h.width, h.yMin - r.yMin);            // bottom
    }

    static void Set(Image img, float x, float y, float w, float h)
    {
        var rt = (RectTransform)img.transform;
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(Mathf.Max(0, w), Mathf.Max(0, h));
    }

    void PlaceOutline(Image o, Rect? hole)
    {
        o.gameObject.SetActive(hole != null);
        if (hole == null) return;
        var h = hole.Value;
        var rt = (RectTransform)o.transform;
        float pulse = 1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.015f;
        rt.anchoredPosition = h.center;
        rt.sizeDelta = new Vector2(h.width + 8, h.height + 8);
        rt.localScale = new Vector3(pulse, pulse, 1);
    }

    void PlaceCoach(Rect? hole, bool forceTop)
    {
        float halfH = root.rect.height * 0.5f;
        // Sit opposite the spotlight so it never covers what you need to click.
        bool targetHigh = !forceTop && (hole == null || hole.Value.center.y > 0);
        float y = targetHigh ? -halfH + 150f : halfH - 160f;
        if (hole == null) y = -60f;
        coach.anchoredPosition = Vector2.Lerp(coach.anchoredPosition, new Vector2(0, y), 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
    }
}
