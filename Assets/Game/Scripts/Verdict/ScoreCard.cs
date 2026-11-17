using System.Linq;
using UnityEngine;

/// <summary>
/// Turns an accusation into a transparent points breakdown (max 100). Everything is computed in code from the
/// case file; only the motive points come from the LLM board (with an offline keyword fallback).
/// </summary>
public static class ScoreCard
{
    public const int SuspectPts = 30, ProofPts = 25, MotivePts = 10, AccomplicePts = 15, LeadsPts = 10, TimePts = 10;

    public static void Fill(CaseFile c, Accusation a, Verdict v)
    {
        var p = v.Check;
        var s = c.Suspect(a.SuspectId);
        bool right = p.CorrectSuspect;
        v.Breakdown.Clear();

        v.Breakdown.Add(new ScoreLine
        {
            Label = "Right person", Max = SuspectPts, Points = right ? SuspectPts : 0,
            Detail = s == null ? "You didn't name anyone." : right ? $"It really was {s.FirstName}." : $"It wasn't {s.FirstName}.",
        });
        v.Breakdown.Add(new ScoreLine
        {
            Label = "Proof that holds up", Max = ProofPts, Points = right && p.Proven ? ProofPts : 0,
            Detail = !right ? "Proof for the wrong person doesn't count." :
                     p.Proven ? "Your evidence shows how it got out and ties it to them." : "Your evidence didn't show how it got out, or why or when.",
        });
        int motive = right ? Mathf.Clamp(v.MotivePoints, 0, MotivePts) : 0;
        v.Breakdown.Add(new ScoreLine
        {
            Label = "Motive", Max = MotivePts, Points = motive,
            Detail = string.IsNullOrWhiteSpace(a.Motive) ? "You didn't say why." :
                     motive >= 8 ? "That's exactly why." : motive > 0 ? "Partly right." : "That's not why.",
        });
        int acc = p.AccompliceCorrect ? AccomplicePts : 0;
        v.Breakdown.Add(new ScoreLine
        {
            Label = "The AI accomplice", Max = AccomplicePts, Points = acc,
            Detail = p.AccompliceCorrect ? "You caught Jensenbot." : c.HasAccomplice ? "Someone (something) helped, and you missed it." : "No accomplice this time.",
        });
        int leads = a.LeadsTotal == 0 ? 0 : Mathf.RoundToInt(LeadsPts * (float)a.LeadsFound / a.LeadsTotal);
        v.Breakdown.Add(new ScoreLine
        {
            Label = "Leads uncovered", Max = LeadsPts, Points = leads,
            Detail = $"{a.LeadsFound} of {a.LeadsTotal} leads.",
        });
        int time = right && p.Proven ? Mathf.RoundToInt(TimePts * Mathf.Clamp01(a.TimeLeft01 / 0.5f)) : 0;
        v.Breakdown.Add(new ScoreLine
        {
            Label = "Time to spare", Max = TimePts, Points = time,
            Detail = !(right && p.Proven) ? "Only counts when the case is closed." : a.TimedOut ? "Dragged in at nine." :
                     $"{Mathf.RoundToInt(a.TimeLeft01 * 12f * 60f) / 60}:{Mathf.RoundToInt(a.TimeLeft01 * 12f * 60f) % 60:00} left on the clock.",
        });

        v.Score = Mathf.Clamp(v.Breakdown.Sum(l => l.Points), 0, 100);
        (v.Grade, v.Rank) = v.Score switch
        {
            >= 90 => ("S", "Leak Hunter"),
            >= 75 => ("A", "Senior Analyst"),
            >= 60 => ("B", "Analyst"),
            >= 40 => ("C", "Intern with Potential"),
            _ => ("D", "Badge Revoked"),
        };
    }

    /// <summary>Offline fallback: does the motive mention the real reason?</summary>
    public static int GuessMotive(string motive)
    {
        if (string.IsNullOrWhiteSpace(motive)) return 0;
        string m = motive.ToLowerInvariant();
        int hits = new[] { "cut", "demo", "revenge", "spite", "keynote", "dex", "angry", "credit" }.Count(m.Contains);
        return hits >= 2 ? 10 : hits == 1 ? 6 : 2;
    }
}
