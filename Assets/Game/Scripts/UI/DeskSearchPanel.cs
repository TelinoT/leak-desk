using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Desks app: search a suspect's monitor, drawer and trash bin for physical clues. Every search costs
/// clock time, so the player has to choose where to look.
/// </summary>
public class DeskSearchPanel
{
    readonly DeskUI ui;
    readonly GameObject root;
    readonly Dictionary<string, Image> deskCards = new();
    readonly Dictionary<string, RawImage> headshots = new();
    readonly Dictionary<string, TextMeshProUGUI> counters = new();
    readonly RectTransform spotsRow;
    readonly TextMeshProUGUI title, subtitle;
    readonly RawImage titleHead;
    CaseDesk current;

    UiTheme T => UiKit.Theme;
    LeakDeskGame Game => ui.Game;

    public DeskSearchPanel(DeskUI ui, Transform parent)
    {
        this.ui = ui;
        var rt = UiKit.Rect("DeskSearchPanel", parent).Stretch();
        root = rt.gameObject;
        rt.HLayout(16);
        rt.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;

        // Desk list
        var list = UiKit.Panel(rt, T.panel, T.panelSprite, "DeskList");
        list.Size(prefW: 300, flexH: 1);
        list.VLayout(12, 16);
        UiKit.Heading(list.transform, "Desks", 28);
        UiKit.Text(list.transform, $"Each search takes <b>{Mathf.RoundToInt(Game.Case.DeskSearchSeconds)}s</b> off the clock.", T.smallSize, T.muted);
        foreach (var desk in Game.Case.Desks) AddDeskCard(list.transform, desk);
        UiKit.Spacer(list.transform, 0, 1);

        // Desk view
        var view = UiKit.Panel(rt, T.panel, T.panelSprite, "DeskView");
        view.Size(flexW: 1, flexH: 1);
        view.VLayout(18, 26);

        var header = UiKit.Rect("Header", view.transform);
        header.HLayout(18);
        var av = UiKit.Panel(header, T.panelAlt, UiKit.AvatarSprite, "Avatar");
        av.Size(prefW: 96, prefH: 96, minW: 96);
        av.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        titleHead = UiKit.Rect("Head", av.transform).Stretch().gameObject.AddComponent<RawImage>();
        titleHead.raycastTarget = false;
        var col = UiKit.Rect("Col", header);
        col.VLayout(4);
        col.Size(flexW: 1);
        title = UiKit.Heading(col, "", 36);
        subtitle = UiKit.Text(col, "", T.bodySize - 2, T.muted);

        spotsRow = UiKit.Rect("Spots", view.transform);
        spotsRow.HLayout(18, 0, TextAnchor.UpperLeft, expandWidth: true);
        spotsRow.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;
        spotsRow.Size(flexH: 1);

        var portraits = SuspectPortraits.Instance;
        if (portraits != null)
        {
            if (portraits.IsReady) OnPortraitsReady();
            else portraits.Ready += OnPortraitsReady;
        }

        Game.WaveUnlocked += _ => RefreshCards();
        if (Game.Case.Desks.Count > 0) Select(Game.Case.Desks[0]);
    }

    public void SetVisible(bool v)
    {
        root.SetActive(v);
        if (v) RefreshCards();
    }

    void AddDeskCard(Transform parent, CaseDesk desk)
    {
        var s = Game.Case.Suspect(desk.Suspect);
        var card = UiKit.Panel(parent, T.panelAlt, T.cardSprite, "Desk: " + desk.Suspect);
        card.HLayout(12, 12);
        card.gameObject.AddComponent<Button>().onClick.AddListener(() => { Sfx.Play("click"); Select(desk); });
        card.gameObject.AddComponent<ButtonPop>();
        card.gameObject.AddComponent<CanvasGroup>();

        var av = UiKit.Panel(card.transform, s.Color, UiKit.AvatarSprite, "Avatar");
        av.Size(prefW: 64, prefH: 64, minW: 64);
        av.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        var ini = UiKit.Text(av.transform, InterrogationPanel.Initials(s.Name), 24, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        ((RectTransform)ini.transform).Stretch();
        var head = UiKit.Rect("Head", av.transform).Stretch().gameObject.AddComponent<RawImage>();
        head.raycastTarget = false;
        head.enabled = false;
        headshots[desk.Suspect] = head;

        var col = UiKit.Rect("Col", card.transform);
        col.VLayout(2);
        col.Size(flexW: 1);
        UiKit.Text(col, $"<b>{s.FirstName}'s desk</b>", T.bodySize - 1, T.text);
        counters[desk.Suspect] = UiKit.Text(col, "", T.smallSize - 2, T.muted);
        deskCards[desk.Suspect] = card;
    }

    void OnPortraitsReady()
    {
        foreach (var kv in headshots)
        {
            var tex = SuspectPortraits.Instance.Headshot(kv.Key);
            kv.Value.texture = tex;
            kv.Value.enabled = tex != null;
        }
        if (current != null) titleHead.texture = SuspectPortraits.Instance.Headshot(current.Suspect);
    }

    void RefreshCards()
    {
        foreach (var desk in Game.Case.Desks)
        {
            if (!deskCards.TryGetValue(desk.Suspect, out var card)) continue;
            bool unlocked = Game.IsSuspectUnlocked(desk.Suspect);
            card.GetComponent<CanvasGroup>().alpha = unlocked ? 1f : 0.45f;
            int searched = desk.Spots.FindAll(sp => Game.IsSpotSearched(desk.Suspect, sp.Id)).Count;
            counters[desk.Suspect].text = !unlocked ? "Not accessible yet" : searched == desk.Spots.Count ? "Fully searched" : $"{searched}/{desk.Spots.Count} searched";
            card.color = current == desk ? Color.Lerp(T.panelAlt, Game.Case.Suspect(desk.Suspect).Color, 0.45f) : T.panelAlt;
        }
    }

    void Select(CaseDesk desk)
    {
        if (!Game.IsSuspectUnlocked(desk.Suspect))
        {
            ui.Toast("That desk isn't accessible yet. Keep digging.", T.warning);
            return;
        }
        current = desk;
        var s = Game.Case.Suspect(desk.Suspect);
        title.text = $"{s.Name}'s desk";
        subtitle.text = $"{s.Role}. Pick where to look: each search costs {Mathf.RoundToInt(Game.Case.DeskSearchSeconds)} seconds.";
        if (SuspectPortraits.Instance != null) titleHead.texture = SuspectPortraits.Instance.Headshot(desk.Suspect);
        titleHead.enabled = titleHead.texture != null;
        RebuildSpots();
        RefreshCards();
    }

    void RebuildSpots()
    {
        UiKit.Clear(spotsRow);
        foreach (var spot in current.Spots) BuildSpot(spot);
    }

    void BuildSpot(DeskSpot spot)
    {
        bool searched = Game.IsSpotSearched(current.Suspect, spot.Id);
        var tile = UiKit.Panel(spotsRow, searched ? T.card : T.panelAlt, T.cardSprite, "Spot: " + spot.Id);
        tile.VLayout(14, 22, TextAnchor.UpperCenter);
        tile.Size(prefW: 10, flexW: 1, flexH: 1, minW: 10);

        var iconBg = UiKit.Panel(tile.transform, searched ? new Color(0, 0, 0, 0.06f) : new Color(1, 1, 1, 0.08f), UiKit.AvatarSprite, "IconBg");
        iconBg.Size(prefW: 150, prefH: 150, minW: 150);
        var iconHolder = UiKit.Rect("IconHolder", iconBg.transform).Stretch(35, 35, 35, 35);
        var icon = UiKit.Icon(iconHolder, spot.Icon, 80, searched ? T.cardText : T.text);
        ((RectTransform)icon.transform).Stretch();
        var center = UiKit.Rect("Center", tile.transform);
        center.HLayout(0, 0, TextAnchor.MiddleCenter);
        iconBg.transform.SetParent(center, false);

        UiKit.Heading(tile.transform, spot.Name, 30, searched ? T.cardText : T.text).alignment = TextAlignmentOptions.Center;

        if (!searched)
        {
            UiKit.Text(tile.transform, "What's in here?", T.bodySize - 2, T.muted, TextAlignmentOptions.Center);
            UiKit.Spacer(tile.transform, 0, 1);
            var b = UiKit.Button(tile.transform, $"Search · {Mathf.RoundToInt(Game.Case.DeskSearchSeconds)}s", () => Search(spot, tile), T.warning, T.cardText, 60, 21);
            b.interactable = Game.Phase == GamePhase.Investigating;
        }
        else
        {
            UiKit.Text(tile.transform, spot.Found, T.bodySize - 1, T.cardText, TextAlignmentOptions.Center);
            if (!string.IsNullOrEmpty(spot.EvidenceId))
            {
                var ev = Game.Case.EvidenceById(spot.EvidenceId);
                var note = UiKit.Card(tile.transform, new Color(1f, 0.95f, 0.70f), 16, 6, "Found");
                note.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(-2.5f, 2.5f));
                var row = UiKit.Rect("Row", note.transform);
                row.HLayout(8);
                UiKit.Icon(row, "find", 22, T.cardText);
                UiKit.Text(row, $"<b>{ev?.Title}</b>", T.smallSize, T.cardText).Size(flexW: 1);
                UiKit.Text(note.transform, ev?.Content, T.smallSize - 1, T.cardText);
                var chip = UiKit.Chip(note.transform, T.accent, 210, 30, "Added");
                UiKit.Text(chip.transform, "ADDED TO EVIDENCE", 13, T.accentText, TextAlignmentOptions.Center, FontStyles.Bold);
            }
            UiKit.Spacer(tile.transform, 0, 1);
        }
    }

    void Search(DeskSpot spot, Image tile)
    {
        if (Game.Phase != GamePhase.Investigating) return;
        ui.StartCoroutine(SearchRoutine(spot, tile));
    }

    IEnumerator SearchRoutine(DeskSpot spot, Image tile)
    {
        Sfx.Play("flag", 0.8f);
        // Flip the tile closed...
        var t = tile.transform;
        for (float time = 0; time < 0.15f; time += Time.unscaledDeltaTime)
        {
            t.localScale = new Vector3(1 - time / 0.15f, 1, 1);
            yield return null;
        }
        var item = Game.SearchSpot(current.Suspect, spot);
        ui.Toast(item != null ? $"Found: {item.Title}  <color=#{UiKit.Hex(T.muted)}>(-{Mathf.RoundToInt(Game.Case.DeskSearchSeconds)}s)</color>"
                              : $"Nothing useful in the {spot.Name.ToLowerInvariant()}  <color=#{UiKit.Hex(T.muted)}>(-{Mathf.RoundToInt(Game.Case.DeskSearchSeconds)}s)</color>",
            item != null ? SourceStyle.Color("desk") : T.muted);
        if (item != null) Sfx.Play("chime");
        RebuildSpots();
        RefreshCards();
        // ...and open the rebuilt one.
        int index = current.Spots.IndexOf(spot);
        if (index >= 0 && index < spotsRow.childCount)
        {
            var nt = spotsRow.GetChild(index);
            for (float time = 0; time < 0.18f; time += Time.unscaledDeltaTime)
            {
                nt.localScale = new Vector3(time / 0.18f, 1, 1);
                yield return null;
            }
            nt.localScale = Vector3.one;
            if (item != null) UiFx.Punch(nt, 0.06f, 0.35f);
        }
    }
}
