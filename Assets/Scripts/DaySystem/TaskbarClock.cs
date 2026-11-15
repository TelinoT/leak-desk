using UnityEngine;
using TMPro;

public class TaskbarClock : MonoBehaviour
{
    [SerializeField] private TMP_Text clockText;

    public int clockStepSize = 15;

    private void Update()
    {
        var day = DayManager.Instance;
        if (day == null || day.CurrentPlan == null) return;
        
        var balance = RunState.Instance.Balance;
        int start = balance.shiftStartHour;
        int end = balance.shiftEndHour;

        if (end <= start) end += 24;

        float hours = Mathf.Lerp(start, end, day.Progress);
        int h = Mathf.FloorToInt(hours) % 24;
        int m = Mathf.FloorToInt((hours % 1f) * 60f) / clockStepSize * clockStepSize;
        
        clockText.text = $"Day {day.DayNumber} | {h:00}:{m:00}";
    }
}

