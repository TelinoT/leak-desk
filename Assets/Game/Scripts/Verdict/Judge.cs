using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class Accusation
{
    public string SuspectId;
    public List<EvidenceItem> Evidence = new();
    public string Motive = "";
    public bool NamesJensenbot;
    public bool TimedOut;
    public int LeadsFound;
    public int LeadsTotal;
    public float TimeLeft01;
    /// <summary>The player cracked a suspect or exposed Jensenbot (needed for S and A).</summary>
    public bool UsedAi;
    /// <summary>Everything the player found (not just what they attached): resolves links between clues.</summary>
    public List<EvidenceItem> Known = new();
}

public class Verdict
{
    public string Result = "wrong"; // convicted | insufficient | wrong
    public int Score;
    public string BoardComment;
    public PreCheck Check;
    public bool Offline;
    public int MotivePoints;
    public List<ScoreLine> Breakdown = new();
    public string Grade = "D";
    public string Rank = "";
    /// <summary>Why the grade was capped, if it was.</summary>
    public string GradeNote;
}

public class ScoreLine
{
    public string Label;
    public string Detail;
    public int Points;
    public int Max;
    public bool Ok => Points > 0;
}

/// <summary>Deterministic part of the judgment, computed from truth.json. The LLM board gets it as ground truth.</summary>
public class PreCheck
{
    public bool CorrectSuspect;
    public List<string> MatchedCombo; // null if no winning combination is covered
    public bool AccompliceCorrect;    // named Jensenbot and it really is the accomplice
    public bool AccompliceWrong;      // named Jensenbot but it isn't
    public bool AccompliceEvidence;   // attached evidence about the accomplice
    public int RelevantCount;
    public int IrrelevantCount;

    public bool Proven => CorrectSuspect && MatchedCombo != null;
}

/// <summary>Runs the board verdict: code pre-check + one LLM call, with an offline fallback.</summary>
public static class Judge
{
    public static PreCheck Check(CaseFile c, Accusation a)
    {
        var p = new PreCheck { CorrectSuspect = a.SuspectId == c.Leaker };

        // An item "covers" a case evidence id if it is that evidence, or a statement about one of its facts.
        bool Covers(string evidenceId) => a.Evidence.Any(item =>
            item.Id == evidenceId ||
            (item.IsTestimony && c.EvidenceById(evidenceId)?.RelevantTo.Contains(item.FactId) == true));

        if (p.CorrectSuspect)
        {
            p.MatchedCombo = c.WinningEvidence.FirstOrDefault(combo => combo.All(Covers));
            // Same rule the accusation screen's "case strength" shows: HOW it got out + WHY or WHEN.
            if (p.MatchedCombo == null)
            {
                var row = Board.Row(c, a.SuspectId);
                var known = a.Known.Count > 0 ? a.Known : a.Evidence;
                if (row != null && Board.Covers(c, a.Evidence, a.SuspectId, known, "method") && Board.Covers(c, a.Evidence, a.SuspectId, known, "motive", "opportunity"))
                {
                    var how = Board.FirstSupport(row.Method, a.Evidence, known);
                    var why = Board.FirstSupport(row.Motive, a.Evidence, known) ?? Board.FirstSupport(row.Opportunity, a.Evidence, known);
                    p.MatchedCombo = new List<string> { how.Id, why.Id };
                }
            }
        }

        bool jbIsAccomplice = c.HasAccomplice && c.Accomplice == "jensenbot";
        p.AccompliceCorrect = a.NamesJensenbot && jbIsAccomplice;
        p.AccompliceWrong = a.NamesJensenbot && !jbIsAccomplice;
        p.AccompliceEvidence = c.AccompliceEvidence.Any(Covers);

        var leakerFacts = c.Facts.Where(f => f.Proves == "leaker").Select(f => f.Id).ToHashSet();
        foreach (var item in a.Evidence)
        {
            if (item.RelevantFacts.Any(leakerFacts.Contains)) p.RelevantCount++;
            else p.IrrelevantCount++;
        }
        return p;
    }

    public static void Run(CaseFile c, Accusation a, Action<Verdict> done) => Run(c, a, done, attempt: 0);

    static void Run(CaseFile c, Accusation a, Action<Verdict> done, int attempt)
    {
        var check = Check(c, a);
        var config = LlmConfig.Load();

        string system = CaseLoader.Fill(CaseLoader.Prompt("judge_system"),
            ("company", c.Company), ("title", c.Title),
            ("leaker_name", c.Suspect(c.Leaker)?.Name ?? c.Leaker),
            ("accomplice", c.HasAccomplice ? "Jensenbot, the company AI assistant" : "none"),
            ("motive", c.Motive),
            ("timeline", string.Join("\n", c.Timeline.Select(t => $"- {t.Time}: {t.Event}"))),
            ("winning", string.Join("\n", c.WinningEvidence.Select(combo =>
                "- " + string.Join(" + ", combo.Select(id => c.EvidenceById(id)?.Title ?? id))))),
            ("precheck", Describe(check)));

        var suspect = c.Suspect(a.SuspectId);
        var sb = new StringBuilder();
        sb.AppendLine(a.TimedOut ? "(The analyst ran out of time and was dragged into the board room.)" : "The analyst presents the case.");
        sb.AppendLine($"Accused: {(suspect != null ? $"{suspect.Name} ({suspect.Role})" : "NOBODY - the analyst did not name anyone")}");
        sb.AppendLine($"Jensenbot named as accomplice: {(a.NamesJensenbot ? "yes" : "no")}");
        sb.AppendLine($"Motive: {(string.IsNullOrWhiteSpace(a.Motive) ? "(none given)" : a.Motive.Trim())}");
        sb.AppendLine("Evidence attached:");
        if (a.Evidence.Count == 0) sb.AppendLine("- (none)");
        foreach (var e in a.Evidence) sb.AppendLine("- " + e.PromptText);

        var messages = new List<LlmMessage> { LlmMessage.User(sb.ToString()) };
        LlmClient.Instance.Send(system, messages,
            text =>
            {
                var obj = LlmJson.TryParseObject(text);
                if (obj == null)
                {
                    UnityEngine.Debug.LogWarning($"[Judge] unparseable reply (attempt {attempt + 1}):\n{text}");
                    if (attempt == 0) Run(c, a, done, 1); else done(Offline(c, a, check));
                    return;
                }
                var v = new Verdict
                {
                    Result = obj["verdict"]?.ToString()?.ToLowerInvariant() ?? "wrong",
                    MotivePoints = (int)(obj["motive_points"]?.ToObject<float>() ?? ScoreCard.GuessMotive(a.Motive)),
                    BoardComment = obj["board_comment"]?.ToString() ?? "",
                    Check = check,
                };
                v = Enforce(v, check);
                ScoreCard.Fill(c, a, v);
                done(v);
            },
            err =>
            {
                UnityEngine.Debug.LogWarning($"[Judge] call failed (attempt {attempt + 1}): {err}");
                if (attempt == 0) Run(c, a, done, 1); else done(Offline(c, a, check));
            },
            // Second try goes to the dialogue model: if the judge model is having a bad minute, a fast model still beats a canned line.
            maxTokens: 400, model: attempt > 0 || config == null ? config?.model : config.judgeModel);
    }

    /// <summary>The code pre-check is authoritative about the verdict class; the LLM only flavors score + comment.</summary>
    static Verdict Enforce(Verdict v, PreCheck p)
    {
        string expected = !p.CorrectSuspect ? "wrong" : p.Proven ? "convicted" : "insufficient";
        if (v.Result != expected) v.Result = expected;
        return v;
    }

    static Verdict Offline(CaseFile c, Accusation a, PreCheck p)
    {
        var v = OfflineComment(c, a, p);
        v.MotivePoints = ScoreCard.GuessMotive(a.Motive);
        ScoreCard.Fill(c, a, v);
        return v;
    }

    static Verdict OfflineComment(CaseFile c, Accusation a, PreCheck p)
    {
        var v = new Verdict { Check = p, Offline = true };
        if (!p.CorrectSuspect)
        {
            v.Result = "wrong";
            v.Score = 10;
            v.BoardComment = "The board stares at you in silence. That is not who did it, and the real leaker is drinking coffee down the hall.";
        }
        else if (!p.Proven)
        {
            v.Result = "insufficient";
            v.Score = 40;
            v.BoardComment = "Right instinct, flimsy file. Legal says we can't act on a hunch. Bring the logs next time.";
        }
        else
        {
            v.Result = "convicted";
            v.Score = 75 + (p.AccompliceCorrect ? 15 : 0) + (string.IsNullOrWhiteSpace(a.Motive) ? 0 : 5);
            v.BoardComment = p.AccompliceCorrect
                ? "Airtight. And you caught the AI helping. Jensenbot is being 'reassigned' to the cafeteria menu."
                : "Airtight case. The board is satisfied, though some of us still wonder who pressed the export button.";
        }
        v.Score = Mathf.Clamp(v.Score, 0, 100);
        return v;
    }

    static string Describe(PreCheck p)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"- Accused the correct leaker: {(p.CorrectSuspect ? "YES" : "NO")}");
        sb.AppendLine($"- Attached evidence covers a winning combination: {(p.MatchedCombo != null ? "YES (" + string.Join(" + ", p.MatchedCombo) + ")" : "NO")}");
        sb.AppendLine($"- Named Jensenbot as accomplice: {(p.AccompliceCorrect ? "YES, correctly" : p.AccompliceWrong ? "YES, but wrongly" : "NO")}");
        sb.AppendLine($"- Attached evidence about Jensenbot's role: {(p.AccompliceEvidence ? "YES" : "NO")}");
        sb.AppendLine($"- Relevant evidence items: {p.RelevantCount}, irrelevant: {p.IrrelevantCount}");
        return sb.ToString();
    }
}
