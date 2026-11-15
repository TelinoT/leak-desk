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
    [JsonProperty("people")] public List<CasePerson> People = new();
    [JsonProperty("timeline")] public List<TimelineEntry> Timeline = new();
    [JsonProperty("facts")] public List<CaseFact> Facts = new();
    [JsonProperty("suspects")] public List<SuspectData> Suspects = new();
    [JsonProperty("jensenbot")] public JensenbotData Jensenbot = new();
    [JsonProperty("evidence")] public List<EvidenceData> Evidence = new();
    [JsonProperty("winning_evidence")] public List<List<string>> WinningEvidence = new();
    [JsonProperty("accomplice_evidence")] public List<string> AccompliceEvidence = new();
    [JsonProperty("documents")] public List<CaseDocument> Documents = new();

    public SuspectData Suspect(string id) => Suspects.FirstOrDefault(s => s.Id == id);
    public EvidenceData EvidenceById(string id) => Evidence.FirstOrDefault(e => e.Id == id);
    public CaseFact Fact(string id) => Facts.FirstOrDefault(f => f.Id == id);
    public bool HasAccomplice => !string.IsNullOrEmpty(Accomplice) && Accomplice != "null";

    public float RoundSeconds => Mathf.Max(60f, RoundMinutes * 60f);
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

    /// <summary>Plain-text rendering, used when asking Jensenbot to summarize.</summary>
    public string ToPlainText()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[{Source.ToUpperInvariant()}] {Title} (from: {From}, time: {Time})");
        foreach (var l in Lines) sb.AppendLine("- " + l.Text);
        return sb.ToString();
    }
}

public class DocumentLine
{
    [JsonProperty("text")] public string Text;
    [JsonProperty("evidence")] public string EvidenceId;
}
