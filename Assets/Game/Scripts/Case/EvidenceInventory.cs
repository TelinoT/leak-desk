using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>An item in the player's evidence tray: a flagged log line, a note, or a suspect statement.</summary>
public class EvidenceItem
{
    public string Id;          // case evidence id (e_*), testimony id (t_<fact>) or note id (n_*)
    public string Source;      // email, slack, badge, transfer, vpn, audit, ticket, calendar, news, facilities, testimony, note
    public string Title;
    public string Content;
    public string FactId;      // for testimony
    public string FromSuspect; // for testimony
    public List<string> RelevantFacts = new();

    public bool IsTestimony => Source == "testimony";

    /// <summary>Short text used in prompts and the board's pre-check.</summary>
    public string PromptText => $"{SourceStyle.Label(Source)}: \"{Content}\"";
}

/// <summary>Holds everything the player has flagged or learned. Raises events for the UI.</summary>
public class EvidenceInventory
{
    readonly CaseFile caseFile;
    readonly List<EvidenceItem> items = new();

    public event Action<EvidenceItem> Added;

    public IReadOnlyList<EvidenceItem> Items => items;
    public int Count => items.Count;

    public EvidenceInventory(CaseFile caseFile) => this.caseFile = caseFile;

    public bool Has(string id) => items.Any(i => i.Id == id);
    public EvidenceItem Get(string id) => items.FirstOrDefault(i => i.Id == id);

    /// <summary>Flags a document line. Lines tied to case evidence become that evidence; others become notes.</summary>
    public EvidenceItem FlagLine(CaseDocument doc, int lineIndex)
    {
        var line = doc.Lines[lineIndex];
        if (!string.IsNullOrEmpty(line.EvidenceId))
        {
            var ev = caseFile.EvidenceById(line.EvidenceId);
            if (ev != null)
                return Add(new EvidenceItem
                {
                    Id = ev.Id, Source = ev.Source, Title = ev.Title, Content = ev.Content,
                    RelevantFacts = new List<string>(ev.RelevantTo),
                });
        }

        return Add(new EvidenceItem
        {
            Id = $"n_{doc.Id}_{lineIndex}", Source = doc.Source, Title = doc.Title, Content = $"{doc.Title}: {line.Text}",
        });
    }

    /// <summary>Adds a case evidence item directly (desk searches).</summary>
    public EvidenceItem AddCaseEvidence(string evidenceId)
    {
        var ev = caseFile.EvidenceById(evidenceId);
        if (ev == null) return null;
        return Add(new EvidenceItem
        {
            Id = ev.Id, Source = ev.Source, Title = ev.Title, Content = ev.Content,
            RelevantFacts = new List<string>(ev.RelevantTo),
        });
    }

    public EvidenceItem AddTestimony(SuspectData suspect, CaseFact fact)
    {
        if (fact == null) return null;
        return Add(new EvidenceItem
        {
            Id = "t_" + fact.Id, Source = "testimony", Title = $"{suspect.FirstName}'s statement",
            Content = $"{suspect.Name}: {fact.Statement ?? fact.Text}", FactId = fact.Id, FromSuspect = suspect.Id,
            RelevantFacts = new List<string> { fact.Id },
        });
    }

    EvidenceItem Add(EvidenceItem item)
    {
        var existing = Get(item.Id);
        if (existing != null) return existing;
        items.Add(item);
        Added?.Invoke(item);
        return item;
    }

    public bool IsLineFlagged(CaseDocument doc, int lineIndex)
    {
        var line = doc.Lines[lineIndex];
        return !string.IsNullOrEmpty(line.EvidenceId) ? Has(line.EvidenceId) : Has($"n_{doc.Id}_{lineIndex}");
    }
}
