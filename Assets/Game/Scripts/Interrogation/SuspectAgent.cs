using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class SuspectReply
{
    public string Reply;
    public string Emotion = "calm";
    public int StressDelta;
    public List<string> Revealed = new();
    public bool IsFallback;
}

/// <summary>One chat log entry, kept so the interrogation view can be rebuilt when switching suspects.</summary>
public class ChatEntry
{
    public enum Kind { Player, Suspect, Evidence, System }
    public Kind Type;
    public string Text;
    public string Emotion;
    public string EvidenceSource;
}

/// <summary>
/// LLM-driven suspect. Builds its system prompt from truth.json, keeps its own chat history and stress meter,
/// and parses the JSON reply format {"reply","emotion","stress_delta","revealed"} defensively.
/// </summary>
public class SuspectAgent
{
    const int MaxHistory = 40;
    static readonly string[] FallbackLines =
    {
        "*stares at you for a long moment* ...Can you repeat the question?",
        "*rubs temples* Sorry. My head is all over the place this morning.",
        "*checks phone, then looks back at you* What was that?",
    };

    public readonly SuspectData Data;
    readonly CaseFile caseFile;
    readonly List<LlmMessage> history = new();
    readonly HashSet<string> shown = new();
    readonly HashSet<string> revealed = new();

    public List<ChatEntry> Log { get; } = new();
    public int Stress { get; private set; }
    public string Emotion { get; private set; } = "calm";
    public bool Broken { get; private set; }
    public bool Busy { get; private set; }
    public bool Met { get; private set; }
    public int QuestionsAsked { get; private set; }

    /// <summary>Raised after every reply (stress/emotion may have changed).</summary>
    public event Action<SuspectAgent> Changed;

    /// <summary>Raised when the suspect states a fact for the first time.</summary>
    public event Action<SuspectAgent, CaseFact> FactRevealed;

    public SuspectAgent(CaseFile caseFile, SuspectData data)
    {
        this.caseFile = caseFile;
        Data = data;
    }

    public void Meet()
    {
        if (Met) return;
        Met = true;
        Log.Add(new ChatEntry { Type = ChatEntry.Kind.System, Text = $"{Data.Name} sits down across from you." });
        if (!string.IsNullOrEmpty(Data.OpeningLine))
            Log.Add(new ChatEntry { Type = ChatEntry.Kind.Suspect, Text = Data.OpeningLine, Emotion = "calm" });
    }

    public void Ask(string question, Action<SuspectReply> done)
    {
        Log.Add(new ChatEntry { Type = ChatEntry.Kind.Player, Text = question });
        QuestionsAsked++;
        Send(question, done);
    }

    public void Present(EvidenceItem evidence, Action<SuspectReply> done)
    {
        Log.Add(new ChatEntry { Type = ChatEntry.Kind.Evidence, Text = evidence.Content, EvidenceSource = evidence.Source });
        bool breaking = Data.BreaksIfShown.Contains(evidence.Id);
        bool first = shown.Add(evidence.Id);

        string content = $"[The analyst places a piece of evidence on the table. {evidence.PromptText}] Explain this.";
        Send(content, done, adjust: reply =>
        {
            // The truth file decides what breaks a suspect; don't leave that to the model alone.
            if (breaking && first && !reply.IsFallback)
            {
                reply.StressDelta = Mathf.Max(reply.StressDelta, 30);
                Broken = true;
            }
        });
    }

    void Send(string userContent, Action<SuspectReply> done, Action<SuspectReply> adjust = null)
    {
        Busy = true;
        history.Add(LlmMessage.User(userContent));
        TrimHistory();
        string system = BuildSystemPrompt();

        Request(system, retriesLeft: 1, reply =>
        {
            Busy = false;
            // Keep the model's own JSON in history so it sticks to the format; fallbacks get a synthetic one.
            history.Add(LlmMessage.Assistant(string.IsNullOrEmpty(reply.raw) || reply.IsFallback
                ? $"{{\"reply\": \"{Escape(reply.Reply)}\", \"emotion\": \"{Emotion}\", \"stress_delta\": 0, \"revealed\": []}}"
                : reply.raw));

            adjust?.Invoke(reply);
            Apply(reply);
            done?.Invoke(reply);
        });
    }

    void Request(string system, int retriesLeft, Action<RawReply> done)
    {
        LlmClient.Instance.Send(system, history, text =>
            {
                var parsed = Parse(text);
                if (parsed != null) { done(parsed); return; }

                if (retriesLeft > 0)
                {
                    Debug.LogWarning($"[SuspectAgent:{Data.Id}] Unparseable reply, retrying once:\n{text}");
                    Request(system, retriesLeft - 1, done);
                    return;
                }

                // Last resort: plain prose without braces is still usable dialogue.
                bool prose = !string.IsNullOrWhiteSpace(text) && !text.Contains("{") && text.Length < 600;
                done(prose ? new RawReply { Reply = text.Trim(), Emotion = Emotion, StressDelta = 2 } : Fallback());
            },
            error =>
            {
                Debug.LogWarning($"[SuspectAgent:{Data.Id}] LLM error: {error}");
                done(Fallback());
            });
    }

    RawReply Fallback() => new()
    {
        Reply = FallbackLines[UnityEngine.Random.Range(0, FallbackLines.Length)],
        Emotion = Emotion,
        StressDelta = 0,
        IsFallback = true,
    };

    void Apply(SuspectReply reply)
    {
        Stress = Mathf.Clamp(Stress + Mathf.Clamp(reply.StressDelta, -10, 40), 0, 100);
        if (Stress >= 80) Broken = true;
        Emotion = reply.Emotion;
        Log.Add(new ChatEntry { Type = ChatEntry.Kind.Suspect, Text = reply.Reply, Emotion = reply.Emotion });

        foreach (var factId in reply.Revealed)
        {
            var fact = caseFile.Fact(factId);
            // Only accept facts this suspect actually knows (the model may hallucinate ids).
            if (fact == null || !fact.KnownBy.Contains(Data.Id) || !revealed.Add(factId)) continue;
            FactRevealed?.Invoke(this, fact);
        }

        Changed?.Invoke(this);
    }

    class RawReply : SuspectReply
    {
        public string raw;
    }

    static RawReply Parse(string text)
    {
        var obj = LlmJson.TryParseObject(text);
        if (obj == null) return null;
        string reply = obj["reply"]?.ToString();
        if (string.IsNullOrWhiteSpace(reply)) return null;

        var r = new RawReply { Reply = reply.Trim(), raw = text };
        string emotion = obj["emotion"]?.ToString()?.ToLowerInvariant();
        r.Emotion = emotion is "calm" or "nervous" or "angry" or "evasive" ? emotion : "calm";
        r.StressDelta = obj["stress_delta"]?.Type == JTokenType.Integer || obj["stress_delta"]?.Type == JTokenType.Float
            ? Mathf.RoundToInt(obj["stress_delta"].Value<float>())
            : 0;
        if (obj["revealed"] is JArray arr)
            r.Revealed = arr.Select(t => t.ToString()).Where(s => !string.IsNullOrEmpty(s)).ToList();
        return r;
    }

    void TrimHistory()
    {
        // Keep the conversation under the Worker's message cap; drop the oldest exchange (must start with user).
        while (history.Count > MaxHistory) history.RemoveRange(0, 2);
    }

    string BuildSystemPrompt()
    {
        var known = caseFile.Facts.Where(f => f.KnownBy.Contains(Data.Id)).ToList();
        var knowledge = new StringBuilder();
        foreach (var f in known) knowledge.AppendLine($"- [{f.Id}] {f.Text}");
        if (known.Count == 0) knowledge.AppendLine("- (nothing special)");

        var people = new StringBuilder();
        foreach (var s in caseFile.Suspects.Where(s => s.Id != Data.Id))
            people.AppendLine($"- {s.Name}, {s.Role}");
        foreach (var p in caseFile.People)
            people.AppendLine($"- {p.Name}: {p.Role}");

        string breaks = Data.BreaksIfShown.Count == 0
            ? "none (you have nothing to hide about the leak)"
            : string.Join("; ", Data.BreaksIfShown.Select(id => caseFile.EvidenceById(id)?.Content ?? id));

        string shownText = shown.Count == 0
            ? "none yet"
            : string.Join("; ", shown.Select(id => caseFile.EvidenceById(id)?.Content ?? id));

        return CaseLoader.Fill(CaseLoader.Prompt("suspect_system"),
            ("name", Data.Name), ("role", Data.Role), ("company", caseFile.Company), ("briefing", caseFile.Briefing),
            ("personality", Data.Personality), ("speech_style", Data.SpeechStyle), ("age", Data.Age.ToString()),
            ("public_story", Data.PublicStory), ("secret", Data.Secret),
            ("lies", string.Join("\n", Data.Lies.Select(l => "- " + l))),
            ("protects", Data.Protects ?? "nothing in particular"),
            ("knowledge", knowledge.ToString()), ("people", people.ToString()),
            ("breaks", breaks), ("on_break", Data.OnBreak ?? ""),
            ("stress", Stress.ToString()), ("broken", Broken ? "yes - you no longer deny what the evidence shows" : "no"),
            ("shown", shownText));
    }

    static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
}
