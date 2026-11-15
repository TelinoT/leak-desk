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
        var card = DeskUI.Modal(overlayRoot, "Verdict", 1240, 940, out root);
        modal = card.transform;
        var scroll = UiKit.ScrollList(card.transform, out body, 16, 4);
        scroll.Size(flexH: 1);
        root.SetActive(false);
    }

    public void ShowDeliberating()
    {
        UiKit.Clear(body);
        root.SetActive(true);
        root.transform.SetAsLastSibling();
        UiKit.Heading(body, "09:00 - Board room", 38);
        var t = UiKit.Text(body, "The board is deliberating", 30, T.text, TextAlignmentOptions.Center, FontStyles.Bold);
        t.Size(prefH: 300);
        ui.StartCoroutine(Dots(t));
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

        var top = UiKit.Rect("Top", body);
        top.HLayout(24);
        var stampBg = UiKit.Panel(top, stampColor, T.buttonSprite, "Stamp");
        stampBg.HLayout(0, 16, TextAnchor.MiddleCenter);
        stampBg.Size(prefW: 560, prefH: 110);
        UiKit.Text(stampBg.transform, stamp, 48, v.Result == "wrong" ? Color.white : T.accentText, TextAlignmentOptions.Center, FontStyles.Bold);
        stampBg.transform.localRotation = Quaternion.Euler(0, 0, 3f);

        var scoreCol = UiKit.Rect("Score", top);
        scoreCol.VLayout(0);
        scoreCol.Size(flexW: 1);
        UiKit.Text(scoreCol, "SCORE", T.smallSize, T.muted, TextAlignmentOptions.Right, FontStyles.Bold);
        var score = UiKit.Text(scoreCol, "0", 84, T.text, TextAlignmentOptions.Right, FontStyles.Bold);
        ui.StartCoroutine(CountUp(score, v.Score));

        var accused = c.Suspect(a.SuspectId);
        UiKit.Text(body, $"You accused <b>{(accused != null ? accused.Name : "nobody")}</b>" +
                         (a.NamesJensenbot ? " and named <b>Jensenbot</b> as accomplice" : "") +
                         $" with {a.Evidence.Count} piece{(a.Evidence.Count == 1 ? "" : "s")} of evidence.", T.bodySize, T.muted);

        var quote = UiKit.Card(body, T.card, 22, 8, "BoardComment");
        UiKit.Text(quote.transform, "THE BOARD CHAIR", T.smallSize, new Color(T.cardText.r, T.cardText.g, T.cardText.b, 0.6f), style: FontStyles.Bold);
        UiKit.Text(quote.transform, $"\"{v.BoardComment}\"", T.bodySize + 4, T.cardText, style: FontStyles.Italic);

        // What really happened
        var truth = UiKit.Card(body, new Color(1, 1, 1, 0.07f), 22, 8, "Truth");
        UiKit.Text(truth.transform, "WHAT REALLY HAPPENED", T.smallSize, T.accent, style: FontStyles.Bold);
        var leaker = c.Suspect(c.Leaker);
        UiKit.Text(truth.transform, $"<b>{leaker?.Name}</b> leaked it. {c.Motive}", T.bodySize, T.text);
        if (c.HasAccomplice)
            UiKit.Text(truth.transform, "<b>Jensenbot</b> was in on it: it gave itself external-sharing rights on Monday 'to maximize launch buzz', " +
                                        "suggested the share link, and left session JB-7731 out of its summaries.", T.bodySize, T.text);
        var p = v.Check;
        if (p != null)
        {
            string line = (p.CorrectSuspect ? "Right suspect" : "Wrong suspect") + "  ·  " +
                          (p.MatchedCombo != null ? "proof was airtight" : "proof was incomplete") + "  ·  " +
                          (p.AccompliceCorrect ? "caught the AI" : c.HasAccomplice ? "missed the AI accomplice" : "no accomplice");
            UiKit.Text(truth.transform, line, T.smallSize, T.muted);
        }

        // Stats
        int questions = game.Suspects.Sum(s => s.QuestionsAsked);
        int minutes = Mathf.FloorToInt(game.Elapsed / 60f), seconds = Mathf.FloorToInt(game.Elapsed % 60f);
        UiKit.Text(body, $"Time used {minutes}:{seconds:00}  ·  Questions asked {questions}  ·  Evidence collected {game.Evidence.Count}  ·  " +
                         $"Jensenbot summaries read {game.Jensenbot.SummariesGiven}" + (v.Offline ? "  ·  (offline verdict)" : ""),
            T.smallSize, T.muted, TextAlignmentOptions.Center);

        var again = UiKit.Button(body, "Play again", game.Restart, T.accent, T.accentText, 64, 26);
        again.Size(prefH: 64);
        ui.StartCoroutine(DeskUI.PopModal(stampBg.transform));
    }

    static IEnumerator CountUp(TextMeshProUGUI t, int target)
    {
        float time = 0;
        while (t != null && time < 1.2f)
        {
            time += Time.unscaledDeltaTime;
            t.text = Mathf.RoundToInt(Mathf.Lerp(0, target, 1 - Mathf.Pow(1 - Mathf.Clamp01(time / 1.2f), 3))).ToString();
            yield return null;
        }
        if (t != null) t.text = target.ToString();
    }
}
