using System;
using System.Collections.Generic;
using UnityEngine;

public enum DayPhase { Briefing, Shift, Review }

public class DayManager : MonoBehaviour
{
    public static DayManager Instance {get; private set;}
    public event Action onPhaseChanged;
    
    [SerializeField] private List<DayPlan> days = new();
    
    public int DayNumber { get; private set; }
    public DayPlan CurrentPlan { get; private set; }
    public DayPhase Phase { get; private set; }
    public float TimeLeft { get; private set; }
    public int LastBonus { get; private set; }
    
    public float Progress => CurrentPlan == null ? 0f : 1f - TimeLeft / CurrentPlan.shiftSeconds;
    
    private void Awake() => Instance = this;

    public void Start()
    {
        LoadDay(1);
    }
    
    private void Update()
    {
        if (Phase != DayPhase.Shift) return;

        TimeLeft -= Time.deltaTime;
        if (TimeLeft <= 0f)
        {
            TimeLeft = 0f;
            EndShift();
        }
    }

    public void LoadDay(int dayNumber)
    {
        DayNumber = dayNumber;
        int index = Mathf.Min(dayNumber - 1, days.Count - 1);
        CurrentPlan = days[index];

        foreach (var tool in CurrentPlan.toolsToUnlock)
            ToolUnlocks.Instance.Unlock(tool);

        TimeLeft = CurrentPlan.shiftSeconds;
        SetPhase(DayPhase.Briefing);
    }

    public void BeginShift()
    {
        if (Phase != DayPhase.Briefing) return;
        RunState.Instance.MarkDayStart();
        SetPhase(DayPhase.Shift);
    }

    public void EndShift()
    {
        if (Phase != DayPhase.Shift) return;
        LastBonus = RunState.Instance.CalculateDayBonus();
        RunState.Instance.ChangeBudget(LastBonus);
        SetPhase(DayPhase.Review);
    }

    public void NextDay() => LoadDay(DayNumber + 1);

    private void SetPhase(DayPhase phase)
    {
        Phase = phase; 
        onPhaseChanged?.Invoke();
    }
}
