using System.Collections.Generic;
using System.Linq;

/// <summary>Evaluates which case leads the player has covered with what's in the evidence tray.</summary>
public static class Leads
{
    public static bool Matches(LeadPart part, EvidenceItem item) =>
        part.Evidence.Contains(item.Id) ||
        (item.IsTestimony && item.FactId != null && part.Facts.Contains(item.FactId));

    public static bool PartFound(LeadPart part, EvidenceInventory inv) => inv.Items.Any(i => Matches(part, i));

    public static int PartsFound(CaseLead lead, EvidenceInventory inv, bool jensenbotExposed)
    {
        if (lead.CountsJensenbotExposed && jensenbotExposed) return lead.Parts.Count;
        return lead.Parts.Count(p => PartFound(p, inv));
    }

    public static bool Found(CaseLead lead, EvidenceInventory inv, bool jensenbotExposed) =>
        PartsFound(lead, inv, jensenbotExposed) >= lead.Parts.Count;

    /// <summary>The leads a single evidence item helps with (for the chips on evidence cards).</summary>
    public static IEnumerable<CaseLead> For(CaseFile c, EvidenceItem item) =>
        c.Leads.Where(l => l.Parts.Any(p => Matches(p, item)));
}
