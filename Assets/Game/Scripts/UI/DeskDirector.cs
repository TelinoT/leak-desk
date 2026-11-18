using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Pacing and tension: last-two-minutes warnings, a ticking final minute, a pulsing ACCUSE button,
/// the case-file sidebar, and gentle one-time hints when the player seems stuck.
/// </summary>
public class DeskDirector : MonoBehaviour
{
    DeskUI ui;
    bool warned2, warned1;
    int lastTick = -1;
    float nextCheck;
    readonly HashSet<string> hintsShown = new();

    UiTheme T => UiKit.Theme;

    void Start() => ui = GetComponent<DeskUI>();

    void Update()
    {
        if (ui == null || ui.Game == null) return;
        var g = ui.Game;
        bool live = g.Phase is GamePhase.Investigating or GamePhase.Accusing;

        // ACCUSE button breathes when it's time to commit.
        float pulse = live && (g.TimeLeft <= 120f || g.Evidence.Count >= 3) ? 1f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.2f)) * 0.05f : 1f;
        ui.AccuseButton.transform.localScale = new Vector3(pulse, pulse, 1);

        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 0.25f;
        ui.UpdateProgress();
        if (!live) return;

        float left = g.TimeLeft;
        if (!warned2 && left <= 120f)
        {
            warned2 = true;
            ui.Toast("<b>Two minutes</b> until the board meeting.", T.warning, "hourglass");
            Sfx.Play("chime", 0.8f);
        }
        if (!warned1 && left <= 60f)
        {
            warned1 = true;
            ui.Toast("<b>One minute.</b> Time to make a call.", T.danger, "hourglass");
            Sfx.Play("crack", 1.6f, 0.6f);
        }
        if (left <= 60f && !g.Overtime)
        {
            int s = Mathf.CeilToInt(left);
            if (s != lastTick)
            {
                lastTick = s;
                Sfx.Play("tick", s % 2 == 0 ? 1f : 0.75f);
                UiFx.Punch(ui.ClockLabel.transform, 0.08f, 0.2f);
            }
        }

        if (g.Phase == GamePhase.Investigating) Hints(g);
    }

    void Hints(LeakDeskGame g)
    {
        float t = g.Elapsed;
        int questions = g.Suspects.Sum(s => s.QuestionsAsked);
        bool presented = g.Suspects.Any(s => s.Log.Any(e => e.Type == ChatEntry.Kind.Evidence));

        if (t > 90 && g.Evidence.Count == 0)
            Hint("flag", "<b>Rosa:</b> Nothing flagged yet? If a log line looks off, flag it.");
        if (t > 200 && questions == 0)
            Hint("talk", "<b>Rosa:</b> You haven't talked to anyone. Ask them where they were at two.");
        if (t > 270 && !presented && g.Evidence.Count > 0)
            Hint("drag", "<b>Rosa:</b> They'll keep lying until you show them something. Drag evidence onto them.");
        if (t > 330 && g.SpotsSearched == 0)
            Hint("desk", "<b>Rosa:</b> Nobody's checked the desks. People throw away the strangest things.");
        if (t > 420 && !g.Jensenbot.Exposed && g.Jensenbot.SummariesGiven > 0)
            Hint("bot", "<b>Rosa:</b> Jensenbot's summary and the raw log don't say the same thing. Check the times.");
        if (t > 480 && g.Evidence.Count < 2)
            Hint("late", "<b>Rosa:</b> Work out how the deck got out, then who was awake to do it.");
    }

    float lastHint = -999f;

    void Hint(string id, string text)
    {
        // At most one hint every 25 seconds, so tips never pile up.
        if (hintsShown.Contains(id) || Time.unscaledTime - lastHint < 25f) return;
        if (!ui.Toast(text, T.secondary, "bulb", DeskUI.ToastPriority.Low)) return; // busy screen: try again later
        hintsShown.Add(id);
        lastHint = Time.unscaledTime;
    }
}
