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

    /// <summary>Raw match, ignoring links. Use the overload with <c>known</c> for anything the player sees.</summary>
    public static bool Supports(BoardCell c, EvidenceItem i) => Hit(c.For, c.ForFacts, i);

    /// <summary>Supports, and if the clue doesn't name the suspect itself, the player also holds a clue that links it.</summary>
    public static bool Supports(BoardCell c, EvidenceItem i, ICollection<EvidenceItem> known) =>
        Supports(c, i) && Linked(c, i, known);

    static bool Linked(BoardCell c, EvidenceItem i, ICollection<EvidenceItem> known)
    {
        if (c.Needs == null || !c.Needs.TryGetValue(i.Id, out var links) || links.Count == 0) return true;
        return known.Any(k => links.Contains(k.Id) || (k.FactId != null && links.Contains(k.FactId)));
    }
    public static bool RulesOut(BoardCell c, EvidenceItem i) => Hit(c.Against, c.AgainstFacts, i);

    /// <summary>Does the cell have anything that puts the suspect in the frame (as opposed to a pure alibi)?</summary>
    static bool HasClaim(BoardCell c) => c.For.Count + c.ForFacts.Count > 0;

    /// <summary>
    /// Counter-evidence only counts once the player knows the claim it contradicts: the article footer says
    /// nothing about Mara until you've seen her send a .key file home. Pure alibis (no claim) count right away.
    /// </summary>
    public static bool RulesOut(BoardCell c, EvidenceItem i, ICollection<EvidenceItem> known) =>
        RulesOut(c, i) && (!HasClaim(c) || known.Any(k => Supports(c, k, known)));

    public static BoardRow Row(CaseFile c, string suspect) => c.Board.Rows.FirstOrDefault(r => r.Suspect == suspect);

    public static CellStatus Status(CaseFile c, IEnumerable<EvidenceItem> items, string suspect, string slot)
    {
        var row = Row(c, suspect);
        if (row == null) return CellStatus.Unknown;
        var cell = row.Cell(slot);
        var list = items as ICollection<EvidenceItem> ?? items.ToList();
        if (list.Any(i => RulesOut(cell, i, list))) return CellStatus.RuledOut;
        if (list.Any(i => Supports(cell, i, list))) return CellStatus.Supported;
        return CellStatus.Unknown;
    }

    public static bool AiFound(CaseFile c, IEnumerable<EvidenceItem> items, bool exposed) =>
        exposed || items.Any(i => Supports(c.Board.Ai, i, items as ICollection<EvidenceItem> ?? items.ToList()));

    /// <summary>Everything an item says on the board (for the chips on evidence cards).</summary>
    public static List<BoardTag> Tags(CaseFile c, EvidenceItem item, IEnumerable<EvidenceItem> known)
    {
        var tags = new List<BoardTag>();
        var list = known as ICollection<EvidenceItem> ?? known.ToList();
        foreach (var row in c.Board.Rows)
            foreach (var slot in Slots)
            {
                var cell = row.Cell(slot);
                if (RulesOut(cell, item, list)) tags.Add(new BoardTag { Suspect = row.Suspect, Slot = slot, Against = true });
                else if (Supports(cell, item, list)) tags.Add(new BoardTag { Suspect = row.Suspect, Slot = slot });
            }
        if (Supports(c.Board.Ai, item, list)) tags.Add(new BoardTag { Slot = "ai" });
        return tags;
    }

    /// <summary>Does this set of evidence support SUSPECT's SLOT (and nothing in it rules that out)?</summary>
    /// <param name="known">Everything the player has found (for links and contradictions); defaults to <paramref name="items"/>.</param>
    public static bool Covers(CaseFile c, IEnumerable<EvidenceItem> items, string suspect, IEnumerable<EvidenceItem> known, params string[] slots)
    {
        var row = Row(c, suspect);
        if (row == null) return false;
        var list = items.ToList();
        var all = known?.ToList() ?? list;
        foreach (var i in list) if (!all.Contains(i)) all.Add(i);
        return slots.Any(s => list.Any(i => Supports(row.Cell(s), i, all)) && !list.Any(i => RulesOut(row.Cell(s), i, all)));
    }

    /// <summary>The first attached item that supports the slot (links resolved against <paramref name="known"/>).</summary>
    public static EvidenceItem FirstSupport(BoardCell cell, IEnumerable<EvidenceItem> items, IEnumerable<EvidenceItem> known)
    {
        var all = known.ToList();
        return items.FirstOrDefault(i => Supports(cell, i, all));
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
