using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class BingoHall : MonoBehaviour
{
    public static BingoHall Instance { get; private set; }

    public event Action OnTablesChanged;
    public event Action<BingoTable> OnBoardChanged;

    private readonly List<BingoTable> tables = new();
    public IReadOnlyList<BingoTable> Tables => tables;

    private GameBalance Balance => RunState.Instance.Balance;

    private void Awake() => Instance = this;

    private void Start()
    {
        var day = DayManager.Instance;
        day.onPhaseChanged += HandlePhaseChanged;

        if (day.CurrentPlan != null && day.Phase == DayPhase.Briefing)
            SetupTables(day.CurrentPlan.bingoTables);
    }

    private void OnDestroy()
    {
        if (DayManager.Instance != null)
            DayManager.Instance.onPhaseChanged -= HandlePhaseChanged;
    }

    private void HandlePhaseChanged()
    {
        var day = DayManager.Instance;
        if (day.Phase == DayPhase.Briefing)
            SetupTables(day.CurrentPlan.bingoTables);
    }

    private void SetupTables(int count)
    {
        tables.Clear();
        for (int i = 1; i <= count; i++)
        {
            var table = new BingoTable(i);
            int prefill = Random.Range(Balance.shiftStartCallsMin, Balance.shiftStartCallsMax + 1);
            for (int k = 0; k < prefill; k++) table.CallNext();

            table.callTimer = Random.Range(0f, Balance.secondsBetweenCalls);
            tables.Add(table);
        }
        OnTablesChanged?.Invoke();
    }

    private void Update()
    {
        if (DayManager.Instance.Phase != DayPhase.Shift) return;

        foreach (var table in tables)
        {
            if (table.waitingForVerdict) continue;

            table.callTimer -= Time.deltaTime;
            if (table.callTimer <= 0f)
            {
                table.callTimer += Balance.secondsBetweenCalls;
                if (table.CallNext()) OnBoardChanged?.Invoke(table);
            }
        }
    }

    public BingoTable GetTableReadyForBingo()
    {
        var ready = new List<BingoTable>();
        foreach (var table in tables)
            if (!table.waitingForVerdict && table.called.Count >= Balance.minCallsBeforeBingo)
                ready.Add(table);

        return ready.Count == 0 ? null : ready[Random.Range(0, ready.Count)];
    }

    public void PauseForClaim(BingoTable table)
    {
        table.waitingForVerdict = true;
        OnBoardChanged?.Invoke(table);
    }

    public void StartNewGame(BingoTable table)
    {
        table.Reset();
        OnBoardChanged?.Invoke(table);
    }
}