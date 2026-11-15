using System;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class DayScreen : MonoBehaviour
{
    [Header("Briefing")]
    [SerializeField] private GameObject briefingPanel;
    [SerializeField] private TMP_Text briefingTitle;
    [SerializeField] private TMP_Text briefingBody;

    [Header("Review")]
    [SerializeField] private GameObject reviewPanel;
    [SerializeField] private TMP_Text reviewTitle;
    [SerializeField] private TMP_Text reviewBody;
    [SerializeField] private TMP_Text reviewButtonLabel;
    
    void Start()
    {
        DayManager.Instance.onPhaseChanged += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (DayManager.Instance != null)
        {
            DayManager.Instance.onPhaseChanged -= Refresh;
        }
    }

    private void Refresh()
    {
        var day = DayManager.Instance;
        var run = RunState.Instance;
        if (day.CurrentPlan == null) return;

        briefingPanel.SetActive(day.Phase == DayPhase.Briefing);
        reviewPanel.SetActive(day.Phase == DayPhase.Review);

        if (day.Phase == DayPhase.Briefing)
        {
            briefingTitle.text = $"Day {day.DayNumber}: {day.CurrentPlan.headline}";
            briefingBody.text = day.CurrentPlan.managerEmail;
        }
        else if (day.Phase == DayPhase.Review)
        {
            reviewTitle.text = run.isFired ? "We need to talk..." : $"Day {day.DayNumber} complete";
            reviewBody.text =
                $"Guest rating: {run.Stars:0.0} ★\n" +
                $"Money protected today: ${run.moneyToday}\n" +
                $"Bonus budget: ${day.LastBonus}\n" +
                $"Total budget: ${run.Budget}";

            if (run.isFired)
                reviewBody.text += "\n\nThe guests aren't happy. Thanks for your service, " +
                                   "but we'll be trying someone new tomorrow.";

            reviewButtonLabel.text = run.isFired ? "Start over" : "Next day";
        }
    }

    public void OnStartShiftClicked() => DayManager.Instance.BeginShift();

    public void OnReviewButtonClicked()
    {
        if (RunState.Instance.isFired)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        else
        {
            DayManager.Instance.NextDay();
        }
    }
}
