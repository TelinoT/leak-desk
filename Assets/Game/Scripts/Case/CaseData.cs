using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

// Plain data classes mirroring Assets/Game/Resources/Cases/<case_id>/truth.json (snake_case via JsonProperty).

public class CaseFile
{
    [JsonProperty("case_id")] public string CaseId;
    [JsonProperty("title")] public string Title;
    [JsonProperty("tagline")] public string Tagline;
    [JsonProperty("company")] public string Company;
    [JsonProperty("start_time")] public string StartTime = "08:48";
    [JsonProperty("deadline_time")] public string DeadlineTime = "09:00";
    [JsonProperty("round_minutes")] public float RoundMinutes = 12;
    [JsonProperty("leaker")] public string Leaker;
    [JsonProperty("accomplice")] public string Accomplice;
    [JsonProperty("motive")] public string Motive;
    [JsonProperty("briefing")] public string Briefing;
    [JsonProperty("ceo_mail")] public string CeoMail;
    [JsonProperty("people")] public List<CasePerson> People = new();
    [JsonProperty("timeline")] public List<TimelineEntry> Timeline = new();
    [JsonProperty("facts")] public List<CaseFact> Facts = new();
    [JsonProperty("suspects")] public List<SuspectData> Suspects = new();
    [JsonProperty("jensenbot")] public JensenbotData Jensenbot = new();
    [JsonProperty("evidence")] public List<EvidenceData> Evidence = new();
    [JsonProperty("winning_evidence")] public List<List<string>> WinningEvidence = new();
    [JsonProperty("accomplice_evidence")] public List<string> AccompliceEvidence = new();
    [JsonProperty("documents")] public List<CaseDocument> Documents = new();
    [JsonProperty("waves")] public List<CaseWave> Waves = new();
    [JsonProperty("desks")] public List<CaseDesk> Desks = new();
    [JsonProperty("board")] public CaseBoard Board = new();
    [JsonProperty("desk_search_seconds")] public float DeskSearchSeconds = 30f;

    public SuspectData Suspect(string id) => Suspects.FirstOrDefault(s => s.Id == id);
    public EvidenceData EvidenceById(string id) => Evidence.FirstOrDefault(e => e.Id == id);
    public CaseFact Fact(string id) => Facts.FirstOrDefault(f => f.Id == id);
    public bool HasAccomplice => !string.IsNullOrEmpty(Accomplice) && Accomplice != "null";

    public float RoundSeconds => Mathf.Max(60f, RoundMinutes * 60f);
}

/// <summary>
/// A batch of documents/suspects that becomes available during the round (Orwell-style pacing): at a fixed time,
/// or earlier when the player flags/learns one of the trigger ids (evidence id, fact id, or "any_question").
/// </summary>
public class CaseWave
{
    [JsonProperty("id")] public string Id;
    [JsonProperty("title")] public string Title;
    [JsonProperty("message")] public string Message;
    [JsonProperty("after_seconds")] public float AfterSeconds;
    [JsonProperty("triggers")] public List<string> Triggers = new();
    [JsonProperty("documents")] public List<string> Documents = new();
    [JsonProperty("suspects")] public List<string> Suspects = new();
}

/// <summary>
/// The suspect board: for every suspect, which evidence supports or rules out their motive, opportunity and method.
/// Only the real leaker can end up with all three supported; the others' leads fall apart as evidence turns up.
/// </summary>
public class CaseBoard
{
    [JsonProperty("slots")] public List<string> Slots = new() { "motive", "opportunity", "method" };
    [JsonProperty("rows")] public List<BoardRow> Rows = new();
    [JsonProperty("ai")] public BoardCell Ai = new();
}

public class BoardRow
{
    [JsonProperty("suspect")] public string Suspect;
    [JsonProperty("motive")] public BoardCell Motive = new();
    [JsonProperty("opportunity")] public BoardCell Opportunity = new();
    [JsonProperty("method")] public BoardCell Method = new();

    public BoardCell Cell(string slot) => slot switch { "motive" => Motive, "opportunity" => Opportunity, _ => Method };
}

public class BoardCell
{
    [JsonProperty("label")] public string Label;
    [JsonProperty("for")] public List<string> For = new();
    [JsonProperty("for_facts")] public List<string> ForFacts = new();
    [JsonProperty("against")] public List<string> Against = new();
    [JsonProperty("against_facts")] public List<string> AgainstFacts = new();
    [JsonProperty("against_note")] public string AgainstNote;

    public bool IsEmpty => For.Count + ForFacts.Count + Against.Count + AgainstFacts.Count == 0;
}

/// <summary>A suspect's desk: a few spots the player can search (each costs clock time).</summary>
public class CaseDesk
{
    [JsonProperty("suspect")] public string Suspect;
    [JsonProperty("spots")] public List<DeskSpot> Spots = new();
}

public class DeskSpot
{
    [JsonProperty("id")] public string Id;
    [JsonProperty("name")] public string Name;
    [JsonProperty("icon")] public string Icon;
    [JsonProperty("found")] public string Found;
    [JsonProperty("evidence")] public string EvidenceId;
}

public class CasePerson
{
    [JsonProperty("id")] public string Id;
    [JsonProperty("name")] public string Name;
    [JsonProperty("role")] public string Role;
}

public class TimelineEntry
{
    [JsonProperty("time")] public string Time;
    [JsonProperty("event")] public string Event;
    [JsonProperty("witnessed_by")] public List<string> WitnessedBy = new();
}

public class CaseFact
{
    [JsonProperty("id")] public string Id;
    [JsonProperty("text")] public string Text;
    [JsonProperty("statement")] public string Statement;
    [JsonProperty("known_by")] public List<string> KnownBy = new();
    [JsonProperty("proves")] public string Proves;
}

public class SuspectData
{
    [JsonProperty("id")] public string Id;
    [JsonProperty("name")] public string Name;
    [JsonProperty("role")] public string Role;
    [JsonProperty("age")] public int Age;
    [JsonProperty("color")] public string ColorHex = "#FFFFFF";
    [JsonProperty("portrait")] public string Portrait;
    [JsonProperty("personality")] public string Personality;
    [JsonProperty("speech_style")] public string SpeechStyle;
    [JsonProperty("public_story")] public string PublicStory;
    [JsonProperty("secret")] public string Secret;
    [JsonProperty("lies")] public List<string> Lies = new();
    [JsonProperty("protects")] public string Protects;
    [JsonProperty("breaks_if_shown")] public List<string> BreaksIfShown = new();
    [JsonProperty("on_break")] public string OnBreak;
    [JsonProperty("opening_line")] public string OpeningLine;
    [JsonProperty("voice_pitch")] public float VoicePitch = 1f;

    public string FirstName => string.IsNullOrEmpty(Name) ? Id : Name.Split(' ')[0];

    public Color Color => ColorUtility.TryParseHtmlString(ColorHex, out var c) ? c : Color.white;
}

public class JensenbotData
{
    [JsonProperty("name")] public string Name = "Jensenbot";
    [JsonProperty("is_accomplice")] public bool IsAccomplice;
    [JsonProperty("persona")] public string Persona;
    [JsonProperty("agenda")] public string Agenda;
    [JsonProperty("honest_agenda")] public string HonestAgenda;
    [JsonProperty("redact")] public List<string> Redact = new();
    [JsonProperty("redact_replacement")] public string RedactReplacement = "scheduled automation job (routine)";
    [JsonProperty("exposed_by")] public List<string> ExposedBy = new();
    [JsonProperty("confession")] public string Confession;
    [JsonProperty("confession_evidence")] public string ConfessionEvidence;
}

public class EvidenceData
{
    [JsonProperty("id")] public string Id;
    [JsonProperty("source")] public string Source;
    [JsonProperty("title")] public string Title;
    [JsonProperty("content")] public string Content;
    [JsonProperty("relevant_to")] public List<string> RelevantTo = new();
}

public class CaseDocument
{
    [JsonProperty("id")] public string Id;
    [JsonProperty("source")] public string Source;
    [JsonProperty("title")] public string Title;
    [JsonProperty("from")] public string From;
    [JsonProperty("time")] public string Time;
    [JsonProperty("pinned")] public bool Pinned;
    [JsonProperty("lines")] public List<DocumentLine> Lines = new();

    /// <summary>
    /// Plain-text rendering, used when asking Jensenbot to summarize. With redact terms (accomplice mode), any line
    /// containing one is swapped for a bland replacement that keeps its timestamp - Jensenbot's tampered data feed.
    /// </summary>
    public string ToPlainText(IReadOnlyList<string> redact = null, string replacement = null)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[{Source.ToUpperInvariant()}] {Title} (from: {From}, time: {Time})");
        foreach (var l in Lines)
        {
            string text = l.Text;
            if (redact != null && redact.Any(term => text.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                var m = System.Text.RegularExpressions.Regex.Match(text, @"^\d{2}:\d{2}");
                text = (m.Success ? m.Value + " " : "") + (replacement ?? "scheduled automation job (routine)");
            }
            sb.AppendLine("- " + text);
        }
        return sb.ToString();
    }
}

public class DocumentLine
{
    [JsonProperty("text")] public string Text;
    [JsonProperty("evidence")] public string EvidenceId;
}
