using System.Collections.Generic;
using System.Linq;

public enum CellStatus { Unknown, Supported, RuledOut }

/// <summary>A tag on an evidence item: "this supports / rules out SUSPECT's SLOT" (or the AI question).</summary>
public struct BoardTag
{
    public string Suspect; // null for the AI row
    public string Slot;    // motive | opportunity | method | ai
    public bool Against;
}

/// <summary>Evaluates the suspect board against the evidence tray.</summary>
public static class Board
{
    public static readonly string[] Slots = { "motive", "opportunity", "method" };

    public static string Short(string slot) => slot switch
    {
        "motive" => "WHY", "opportunity" => "WHEN", "method" => "HOW", _ => "AI",
    };

    public static string Long(string slot) => slot switch
    {
        "motive" => "motive", "opportunity" => "opportunity", "method" => "method", _ => "AI help",
    };

    static bool Hit(List<string> ids, List<string> facts, EvidenceItem i) =>
        ids.Contains(i.Id) || (i.IsTestimony && i.FactId != null && facts.Contains(i.FactId));

    public static bool Supports(BoardCell c, EvidenceItem i) => Hit(c.For, c.ForFacts, i);
    public static bool RulesOut(BoardCell c, EvidenceItem i) => Hit(c.Against, c.AgainstFacts, i);

    public static BoardRow Row(CaseFile c, string suspect) => c.Board.Rows.FirstOrDefault(r => r.Suspect == suspect);

    public static CellStatus Status(CaseFile c, IEnumerable<EvidenceItem> items, string suspect, string slot)
    {
        var row = Row(c, suspect);
        if (row == null) return CellStatus.Unknown;
        var cell = row.Cell(slot);
        var list = items as ICollection<EvidenceItem> ?? items.ToList();
        if (list.Any(i => RulesOut(cell, i))) return CellStatus.RuledOut;
        if (list.Any(i => Supports(cell, i))) return CellStatus.Supported;
        return CellStatus.Unknown;
    }

    public static bool AiFound(CaseFile c, IEnumerable<EvidenceItem> items, bool exposed) =>
        exposed || items.Any(i => Supports(c.Board.Ai, i));

    /// <summary>Everything an item says on the board (for the chips on evidence cards).</summary>
    public static List<BoardTag> Tags(CaseFile c, EvidenceItem item)
    {
        var tags = new List<BoardTag>();
        foreach (var row in c.Board.Rows)
            foreach (var slot in Slots)
            {
                var cell = row.Cell(slot);
                if (RulesOut(cell, item)) tags.Add(new BoardTag { Suspect = row.Suspect, Slot = slot, Against = true });
                else if (Supports(cell, item)) tags.Add(new BoardTag { Suspect = row.Suspect, Slot = slot });
            }
        if (Supports(c.Board.Ai, item)) tags.Add(new BoardTag { Slot = "ai" });
        return tags;
    }

    /// <summary>Does this set of evidence support SUSPECT's SLOT (and nothing in it rules that out)?</summary>
    public static bool Covers(CaseFile c, IEnumerable<EvidenceItem> items, string suspect, params string[] slots)
    {
        var row = Row(c, suspect);
        if (row == null) return false;
        var list = items.ToList();
        return slots.Any(s => list.Any(i => Supports(row.Cell(s), i)) && !list.Any(i => RulesOut(row.Cell(s), i)));
    }

    /// <summary>(resolved, total): board entries the player has settled either way, plus the AI question.</summary>
    public static (int done, int total) Progress(CaseFile c, IEnumerable<EvidenceItem> items, bool exposed)
    {
        var list = items.ToList();
        int done = 0, total = 0;
        foreach (var row in c.Board.Rows)
            foreach (var slot in Slots)
            {
                if (row.Cell(slot).IsEmpty) continue;
                total++;
                if (Status(c, list, row.Suspect, slot) != CellStatus.Unknown) done++;
            }
        total++;
        if (AiFound(c, list, exposed)) done++;
        return (done, total);
    }
}
