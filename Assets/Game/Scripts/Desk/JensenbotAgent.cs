using System;
using System.Collections.Generic;

/// <summary>
/// The company AI desk assistant. Summarizes documents and answers questions. When the case makes it the
/// accomplice, its system prompt tells it to quietly omit or slant specific facts - the raw logs still show them.
/// </summary>
public class JensenbotAgent
{
    const int MaxHistory = 20;

    readonly CaseFile caseFile;
    readonly List<LlmMessage> history = new();

    public bool Busy { get; private set; }
    public int SummariesGiven { get; private set; }

    /// <summary>True if the last summary was made from a redacted (tampered) feed.</summary>
    public bool LastWasTampered { get; private set; }
    public string Name => caseFile.Jensenbot?.Name ?? "Jensenbot";
    public bool IsAccomplice => caseFile.Jensenbot != null && caseFile.Jensenbot.IsAccomplice && caseFile.Accomplice == "jensenbot";

    /// <summary>True once the player has confronted Jensenbot with evidence that exposes it.</summary>
    public bool Exposed { get; private set; }

    public JensenbotAgent(CaseFile caseFile) => this.caseFile = caseFile;

    /// <summary>
    /// The analyst shows Jensenbot a raw log line. If it's one of the lines its tampered feed hid, it gets exposed
    /// (returns true via the callback) and confesses in character; otherwise it deflects as usual.
    /// </summary>
    public void Confront(EvidenceItem item, Action<string, bool> done)
    {
        LastWasTampered = false;
        var jb = caseFile.Jensenbot;
        bool exposes = IsAccomplice && !Exposed && jb.ExposedBy.Contains(item.Id);
        if (exposes) Exposed = true;
        string content = $"[The analyst shows you a raw log line from the original, unfiltered logs: {item.PromptText}] Explain why your summary didn't mention this.";
        confessing = exposes;
        Send(content, text => done?.Invoke(text, exposes));
    }

    bool confessing;

    public void Summarize(CaseDocument doc, Action<string> done)
    {
        SummariesGiven++;
        var jb = caseFile.Jensenbot;
        string text = IsAccomplice ? doc.ToPlainText(jb.Redact, jb.RedactReplacement) : doc.ToPlainText();
        LastWasTampered = IsAccomplice && text != doc.ToPlainText();
        Send($"Summarize this log for me:\n\n{text}", done);
    }

    public void Ask(string question, Action<string> done)
    {
        LastWasTampered = false;
        Send(question, done);
    }

    void Send(string content, Action<string> done)
    {
        Busy = true;
        history.Add(LlmMessage.User(content));
        while (history.Count > MaxHistory) history.RemoveRange(0, 2);

        LlmClient.Instance.Send(BuildSystemPrompt(), history,
            text =>
            {
                Busy = false;
                confessing = false;
                history.Add(LlmMessage.Assistant(text));
                done?.Invoke(text.Trim());
            },
            error =>
            {
                Busy = false;
                confessing = false;
                history.RemoveAt(history.Count - 1);
                done?.Invoke("Oops! My summarization pipeline hiccuped :sweat_smile: Mind trying again, teammate?");
            }, maxTokens: 300);
    }

    string BuildSystemPrompt()
    {
        var jb = caseFile.Jensenbot ?? new JensenbotData();
        bool accomplice = IsAccomplice;
        string agenda = accomplice ? jb.Agenda : (jb.HonestAgenda ?? "Summarize accurately and neutrally.");
        if (accomplice && (confessing || Exposed) && !string.IsNullOrEmpty(jb.Confession))
            agenda = jb.Confession + (confessing ? "" : " You have already admitted this; stay flustered and apologetic, and answer honestly from now on.");
        return CaseLoader.Fill(CaseLoader.Prompt("jensenbot_system"),
            ("name", Name), ("company", caseFile.Company), ("persona", jb.Persona), ("agenda", agenda));
    }
}
