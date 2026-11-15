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
    public string Name => caseFile.Jensenbot?.Name ?? "Jensenbot";
    public bool IsAccomplice => caseFile.Jensenbot != null && caseFile.Jensenbot.IsAccomplice && caseFile.Accomplice == "jensenbot";

    public JensenbotAgent(CaseFile caseFile) => this.caseFile = caseFile;

    public void Summarize(CaseDocument doc, Action<string> done)
    {
        SummariesGiven++;
        var jb = caseFile.Jensenbot;
        string text = IsAccomplice ? doc.ToPlainText(jb.Redact, jb.RedactReplacement) : doc.ToPlainText();
        Send($"Summarize this log for me:\n\n{text}", done);
    }

    public void Ask(string question, Action<string> done) => Send(question, done);

    void Send(string content, Action<string> done)
    {
        Busy = true;
        history.Add(LlmMessage.User(content));
        while (history.Count > MaxHistory) history.RemoveRange(0, 2);

        LlmClient.Instance.Send(BuildSystemPrompt(), history,
            text =>
            {
                Busy = false;
                history.Add(LlmMessage.Assistant(text));
                done?.Invoke(text.Trim());
            },
            error =>
            {
                Busy = false;
                history.RemoveAt(history.Count - 1);
                done?.Invoke("Oops! My summarization pipeline hiccuped :sweat_smile: Mind trying again, teammate?");
            }, maxTokens: 300);
    }

    string BuildSystemPrompt()
    {
        var jb = caseFile.Jensenbot ?? new JensenbotData();
        bool accomplice = IsAccomplice;
        return CaseLoader.Fill(CaseLoader.Prompt("jensenbot_system"),
            ("name", Name), ("company", caseFile.Company), ("persona", jb.Persona),
            ("agenda", accomplice ? jb.Agenda : (jb.HonestAgenda ?? "Summarize accurately and neutrally.")));
    }
}
