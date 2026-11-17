using System.Collections.Generic;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Board deliberation spinner, then the verdict: stamp, score, board comment, and what really happened.</summary>
public class VerdictOverlay
{
    readonly DeskUI ui;
    readonly GameObject root;
    readonly Transform modal;
    readonly RectTransform body;

    UiTheme T => UiKit.Theme;

    public VerdictOverlay(DeskUI ui, Transform overlayRoot)
    {
        this.ui = ui;
        var card = DeskUI.Modal(overlayRoot, "Verdict", 1560, 980, out root);
        modal = card.transform;
        var scroll = UiKit.ScrollList(card.transform, out body, 16, 4);
        scroll.Size(flexH: 1);
        root.SetActive(false);
    }

    public void ShowDeliberating(Accusation a)
    {
        UiKit.Clear(body);
        root.SetActive(true);
        root.transform.SetAsLastSibling();
        UiKit.Heading(body, "09:00 - Board room", 38);
        var gavelRow = UiKit.Rect("GavelRow", body);
        gavelRow.HLayout(0, 0, TextAnchor.MiddleCenter);
        gavelRow.Size(prefH: 260);
        var gavelBg = UiKit.Panel(gavelRow, new Color(1, 1, 1, 0.06f), UiKit.AvatarSprite, "GavelBg");
        gavelBg.Size(prefW: 220, prefH: 220, minW: 220);
        var gavel = UiKit.Icon(UiKit.Rect("H", gavelBg.transform).Stretch(45, 45, 45, 45), "mallet", 130, T.warning);
        ((RectTransform)gavel.transform).Stretch();
        var t = UiKit.Text(body, "The board is deliberating", 30, T.text, TextAlignmentOptions.Center, FontStyles.Bold);
        t.Size(prefH: 120);
        ui.StartCoroutine(Dots(t));
        ui.StartCoroutine(Gavel((RectTransform)gavel.transform.parent));

        // Recap of what the player just put on the table.
        var c = ui.Game.Case;
        var s = c.Suspect(a.SuspectId);
        var recap = UiKit.Card(body, new Color(1, 1, 1, 0.06f), 22, 10, "Recap");
        UiKit.Text(recap.transform, "YOUR CASE", T.smallSize, T.accent, style: FontStyles.Bold);
        var who = UiKit.Rect("Who", recap.transform);
        who.HLayout(16);
        var av = UiKit.Panel(who, s != null ? s.Color : T.panelAlt, UiKit.AvatarSprite, "Avatar");
        av.Size(prefW: 84, prefH: 84, minW: 84);
        av.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        var tex = s != null && SuspectPortraits.Instance != null ? SuspectPortraits.Instance.Headshot(s.Id) : null;
        if (tex != null)
        {
            var raw = UiKit.Rect("Head", av.transform).Stretch().gameObject.AddComponent<RawImage>();
            raw.texture = tex;
        }
        var col = UiKit.Rect("Col", who);
        col.VLayout(4);
        col.Size(flexW: 1);
        UiKit.Heading(col, s != null ? $"You accuse {s.Name}" : "You accuse nobody", 30);
        UiKit.Text(col, string.IsNullOrWhiteSpace(a.Motive) ? "<i>No motive given.</i>" : $"<i>\"{a.Motive.Trim()}\"</i>", T.bodySize - 2, T.muted);
        if (a.NamesJensenbot) UiKit.Text(col, "...and you name <b>Jensenbot</b> as the accomplice.", T.bodySize - 2, T.danger);
        var evRow = UiKit.Rect("Evidence", recap.transform);
        evRow.HLayout(10);
        if (a.Evidence.Count == 0) UiKit.Text(evRow, "No evidence attached.", T.smallSize, T.muted);
        foreach (var e in a.Evidence)
        {
            var chip = UiKit.Chip(evRow, SourceStyle.Color(e.Source), 300, 36, "Ev");
            chip.GetComponent<HorizontalLayoutGroup>().spacing = 6;
            UiKit.Icon(chip.transform, SourceStyle.Icon(e.Source), 20, T.cardText);
            var et = UiKit.Text(chip.transform, e.Title, 15, T.cardText, TextAlignmentOptions.Left, FontStyles.Bold);
            et.textWrappingMode = TextWrappingModes.NoWrap;
            et.overflowMode = TextOverflowModes.Ellipsis;
        }
        ui.StartCoroutine(DeskUI.PopModal(modal));
    }

    IEnumerator Dots(TextMeshProUGUI t)
    {
        int i = 0;
        while (t != null && ui.Game.Phase == GamePhase.Deliberating)
        {
            t.text = "The board is deliberating" + new string('.', i++ % 4) + "\n<size=60%><color=#" + UiKit.Hex(T.muted) +
                     ">The CEO adjusts his leather jacket.</color></size>";
            yield return new WaitForSecondsRealtime(0.4f);
        }
    }

    public void ShowResult(Accusation a, Verdict v)
    {
        UiKit.Clear(body);
        var game = ui.Game;
        var c = game.Case;

        Color stampColor = v.Result switch { "convicted" => T.accent, "insufficient" => T.warning, _ => T.danger };
        string stamp = v.Result switch { "convicted" => "CASE CLOSED", "insufficient" => "NOT ENOUGH PROOF", _ => "WRONG PERSON" };
        var accused = c.Suspect(a.SuspectId);

        // ---------- header: stamp + who you accused | grade badge + total ----------
        var top = UiKit.Rect("Top", body);
        top.HLayout(28, 0, TextAnchor.MiddleLeft);

        var left = UiKit.Rect("Left", top);
        left.VLayout(14);
        left.Size(flexW: 1);
        var stampBg = UiKit.Panel(left, stampColor, T.buttonSprite, "Stamp");
        stampBg.HLayout(0, 14, TextAnchor.MiddleCenter);
        stampBg.Size(prefW: 560, prefH: 104);
        var stampText = UiKit.Text(stampBg.transform, stamp, 50, v.Result == "wrong" ? Color.white : T.accentText, TextAlignmentOptions.Center, FontStyles.Bold);
        if (T.headingFont != null) stampText.font = T.headingFont;
        stampBg.transform.localRotation = Quaternion.Euler(0, 0, 2.5f);
        UiKit.Text(left, $"You accused <b>{(accused != null ? accused.Name : "nobody")}</b>" +
                         (a.NamesJensenbot ? " and named <b>Jensenbot</b> as the accomplice" : "") + ".", T.bodySize, T.muted);

        var badgeCol = UiKit.Rect("Badge", top);
        badgeCol.HLayout(18, 0, TextAnchor.MiddleRight);
        var scoreCol = UiKit.Rect("ScoreCol", badgeCol);
        scoreCol.VLayout(0, 0, TextAnchor.MiddleRight);
        scoreCol.Size(prefW: 260);
        UiKit.Text(scoreCol, "SCORE", T.smallSize, T.muted, TextAlignmentOptions.Right, FontStyles.Bold);
        var total = UiKit.Text(scoreCol, "0", 88, T.text, TextAlignmentOptions.Right, FontStyles.Bold);
        if (T.headingFont != null) total.font = T.headingFont;
        var rank = UiKit.Text(scoreCol, "", T.bodySize, GradeColor(v.Grade), TextAlignmentOptions.Right, FontStyles.Bold);
        var gradeBg = UiKit.Panel(badgeCol, GradeColor(v.Grade), UiKit.AvatarSprite, "Grade");
        gradeBg.Size(prefW: 170, prefH: 170, minW: 170);
        var gradeText = UiKit.Text(gradeBg.transform, v.Grade, 110, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        if (T.headingFont != null) gradeText.font = T.headingFont;
        ((RectTransform)gradeText.transform).Stretch();
        var gradeGroup = gradeBg.gameObject.AddComponent<CanvasGroup>();
        gradeGroup.alpha = 0;

        // ---------- two columns: breakdown | board + truth ----------
        var cols = UiKit.Rect("Cols", body);
        cols.HLayout(20, 0, TextAnchor.UpperLeft);
        cols.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;

        var breakdown = UiKit.Card(cols, new Color(0, 0, 0, 0.25f), 22, 8, "Breakdown");
        breakdown.Size(prefW: 640, flexW: 0);
        UiKit.Text(breakdown.transform, "HOW YOU SCORED", T.smallSize, T.accent, style: FontStyles.Bold);
        var rows = new List<(CanvasGroup group, ScoreLine line)>();
        foreach (var line in v.Breakdown)
        {
            var row = UiKit.Panel(breakdown.transform, new Color(1, 1, 1, line.Ok ? 0.07f : 0.03f), T.cardSprite, "Row");
            row.HLayout(14, 12);
            UiKit.Icon(row.transform, line.Ok ? "mark_check" : "mark_x", 30, line.Ok ? T.accent : new Color(1, 1, 1, 0.3f));
            var lc = UiKit.Rect("Col", row.transform);
            lc.VLayout(0);
            lc.Size(flexW: 1);
            UiKit.Text(lc, $"<b>{line.Label}</b>", T.bodySize - 1, line.Ok ? T.text : T.muted);
            UiKit.Text(lc, line.Detail, T.smallSize - 1, T.muted);
            var pts = UiKit.Text(row.transform, $"<b>{(line.Points > 0 ? "+" : "")}{line.Points}</b><size=60%><color=#{UiKit.Hex(T.muted)}> / {line.Max}</color></size>",
                34, line.Ok ? T.accent : new Color(1, 1, 1, 0.35f), TextAlignmentOptions.Right);
            pts.Size(prefW: 120);
            pts.textWrappingMode = TextWrappingModes.NoWrap;
            var g = row.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0;
            rows.Add((g, line));
        }

        var right = UiKit.Rect("Right", cols);
        right.VLayout(16);
        right.Size(flexW: 1);
        var quote = UiKit.Card(right, T.card, 22, 8, "BoardComment");
        UiKit.Text(quote.transform, "THE BOARD CHAIR", T.smallSize, new Color(T.cardText.r, T.cardText.g, T.cardText.b, 0.6f), style: FontStyles.Bold);
        UiKit.Text(quote.transform, $"\"{v.BoardComment}\"", T.bodySize + 2, T.cardText, style: FontStyles.Italic);

        var truth = UiKit.Card(right, new Color(1, 1, 1, 0.07f), 22, 8, "Truth");
        UiKit.Text(truth.transform, "WHAT REALLY HAPPENED", T.smallSize, T.accent, style: FontStyles.Bold);
        var leaker = c.Suspect(c.Leaker);
        UiKit.Text(truth.transform, $"<b>{leaker?.Name}</b> leaked it. {c.Motive}", T.bodySize - 2, T.text);
        if (c.HasAccomplice)
            UiKit.Text(truth.transform, "<b>Jensenbot</b> helped: it gave itself external-sharing rights on Monday, offered Jules the share link, and left it out of its summaries.",
                T.bodySize - 2, T.text);

        var p = v.Check;
        if (p != null && !p.Proven)
        {
            var best = c.WinningEvidence.OrderByDescending(combo => combo.Count(id => game.Evidence.Has(id))).FirstOrDefault();
            if (best != null)
            {
                var miss = UiKit.Card(right, new Color(1, 1, 1, 0.07f), 22, 8, "Missed");
                UiKit.Text(miss.transform, "WHAT WOULD HAVE CLOSED IT", T.smallSize, T.warning, style: FontStyles.Bold);
                foreach (var id in best)
                {
                    var ev = c.EvidenceById(id);
                    bool had = game.Evidence.Has(id);
                    var row = UiKit.Rect("Row", miss.transform);
                    row.HLayout(12);
                    UiKit.Icon(row, had ? "mark_check" : "mark_x", 24, had ? T.accent : T.danger);
                    UiKit.Text(row, $"<b>{ev?.Title}</b>  <color=#{UiKit.Hex(T.muted)}>({SourceStyle.Label(ev?.Source)}, {(had ? "you had it" : "never found")})</color>",
                        T.bodySize - 3, T.text).Size(flexW: 1);
                }
            }
        }

        // ---------- footer ----------
        int questions = game.Suspects.Sum(s => s.QuestionsAsked);
        int minutes = Mathf.FloorToInt(game.Elapsed / 60f), seconds = Mathf.FloorToInt(game.Elapsed % 60f);
        UiKit.Text(body, $"Time used {minutes}:{seconds:00}  ·  Questions asked {questions}  ·  Evidence found {game.Evidence.Count}  ·  " +
                         $"Leads {a.LeadsFound}/{a.LeadsTotal}" + (v.Offline ? "  ·  (offline verdict)" : ""),
            T.smallSize, T.muted, TextAlignmentOptions.Center);
        var again = UiKit.Button(body, "Play again", game.Restart, T.accent, T.accentText, 64, 26);
        again.Size(prefH: 64);

        ui.StartCoroutine(DeskUI.PopModal(stampBg.transform));
        CameraDrift.PushIn();
        Sfx.Play("stamp");
        ui.StartCoroutine(RevealScore(rows, total, rank, gradeBg, gradeGroup, v));
    }

    /// <summary>Rows tick in one by one, the total counts along, then the grade badge slams down.</summary>
    IEnumerator RevealScore(List<(CanvasGroup group, ScoreLine line)> rows, TextMeshProUGUI total, TextMeshProUGUI rank,
        Image gradeBg, CanvasGroup gradeGroup, Verdict v)
    {
        yield return new WaitForSecondsRealtime(0.5f);
        int sum = 0;
        foreach (var (group, line) in rows)
        {
            if (group == null) yield break;
            for (float t = 0; t < 0.18f; t += Time.unscaledDeltaTime)
            {
                group.alpha = t / 0.18f;
                group.transform.localScale = Vector3.one * Mathf.Lerp(1.08f, 1f, t / 0.18f);
                yield return null;
            }
            group.alpha = 1;
            group.transform.localScale = Vector3.one;
            Sfx.Play(line.Ok ? "tick" : "hover", line.Ok ? 1.2f : 0.7f);
            int from = sum;
            sum += line.Points;
            for (float t = 0; t < 0.25f && line.Points > 0; t += Time.unscaledDeltaTime)
            {
                total.text = Mathf.RoundToInt(Mathf.Lerp(from, sum, t / 0.25f)).ToString();
                yield return null;
            }
            total.text = sum.ToString();
            if (line.Points > 0) UiFx.Punch(total.transform, 0.12f, 0.25f);
            yield return new WaitForSecondsRealtime(0.15f);
        }
        total.text = v.Score.ToString();

        yield return new WaitForSecondsRealtime(0.3f);
        rank.text = v.Rank;
        var gt = gradeBg.transform;
        for (float t = 0; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            float k = 1 - Mathf.Pow(1 - t / 0.3f, 3);
            gradeGroup.alpha = k;
            gt.localScale = Vector3.one * Mathf.Lerp(2.4f, 1f, k);
            gt.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-30f, -6f, k));
            yield return null;
        }
        gradeGroup.alpha = 1;
        Sfx.Play("stamp");
        UiFx.Punch(gt, 0.15f, 0.3f);
        UiFx.Shake((RectTransform)body, 10f, 0.3f);
        Sfx.Play(v.Result == "convicted" ? "win" : "lose");
        if (v.Result == "convicted") ui.StartCoroutine(Confetti());
    }

    Color GradeColor(string grade) => grade switch
    {
        "S" => new Color(1f, 0.78f, 0.15f),
        "A" => T.accent,
        "B" => T.secondary,
        "C" => T.warning,
        _ => T.danger,
    };

    IEnumerator Confetti()
    {
        var T = UiKit.Theme;
        var layer = UiKit.Rect("Confetti", ui.OverlayRoot).Stretch();
        layer.SetAsLastSibling();
        Color[] colors = { T.accent, T.secondary, T.warning, T.danger, new Color(0.69f, 0.45f, 1f), Color.white };
        const int n = 90;
        var parts = new RectTransform[n];
        var vel = new Vector2[n];
        var spin = new float[n];
        for (int i = 0; i < n; i++)
        {
            var img = UiKit.Panel(layer, colors[i % colors.Length], T.pillSprite, "Bit");
            img.raycastTarget = false;
            var rt = (RectTransform)img.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.62f);
            rt.sizeDelta = new Vector2(Random.Range(10f, 18f), Random.Range(18f, 30f));
            img.Pill(rt.sizeDelta.x);
            parts[i] = rt;
            float a = Random.Range(20f, 160f) * Mathf.Deg2Rad;
            vel[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(500f, 1300f);
            spin[i] = Random.Range(-720f, 720f);
        }
        for (float t = 0; t < 3f; t += Time.unscaledDeltaTime)
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < n; i++)
            {
                vel[i] += new Vector2(0, -1400f) * dt;
                vel[i] *= 1f - 0.9f * dt;
                parts[i].anchoredPosition += vel[i] * dt;
                parts[i].Rotate(0, 0, spin[i] * dt);
            }
            if (t > 2.2f)
                foreach (var p in parts) p.GetComponent<Image>().canvasRenderer.SetAlpha(1f - (t - 2.2f) / 0.8f);
            yield return null;
        }
        Object.Destroy(layer.gameObject);
    }

    /// <summary>The gavel swings up slowly and knocks down, with a thud, until the verdict is in.</summary>
    IEnumerator Gavel(RectTransform g)
    {
        while (g != null && ui.Game.Phase == GamePhase.Deliberating)
        {
            for (float t = 0; t < 0.55f && g != null; t += Time.unscaledDeltaTime)
            {
                g.localRotation = Quaternion.Euler(0, 0, Mathf.SmoothStep(0, 35f, t / 0.55f));
                yield return null;
            }
            for (float t = 0; t < 0.09f && g != null; t += Time.unscaledDeltaTime)
            {
                g.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(35f, -8f, t / 0.09f));
                yield return null;
            }
            if (g == null) yield break;
            Sfx.Play("stamp", 1.3f, 0.6f);
            UiFx.Punch(g.parent, 0.06f, 0.2f);
            g.localRotation = Quaternion.identity;
            yield return new WaitForSecondsRealtime(0.35f);
        }
    }

    static IEnumerator CountUp(TextMeshProUGUI t, int target)
    {
        float time = 0;
        int last = -1;
        while (t != null && time < 1.2f)
        {
            time += Time.unscaledDeltaTime;
            int v = Mathf.RoundToInt(Mathf.Lerp(0, target, 1 - Mathf.Pow(1 - Mathf.Clamp01(time / 1.2f), 3)));
            if (v / 4 != last / 4) Sfx.Play("tick", 0.8f + v / 100f);
            last = v;
            t.text = v.ToString();
            yield return null;
        }
        if (t == null) yield break;
        t.text = target.ToString();
        UiFx.Punch(t.transform, 0.2f, 0.35f);
    }
}
