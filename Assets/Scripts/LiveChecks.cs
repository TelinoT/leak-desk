using System;
using System.Collections.Generic;
using UnityEngine;

public class LiveChecks : MonoBehaviour
{
    public static LiveChecks Instance { get; private set; }

    /// message, true if it was a good outcome
    public event Action<string, bool> OnResult;

    private class PendingCheck
    {
        public GuestAgent guest;
        public float timeLeft;
        public bool redHanded;
    }

    private readonly List<PendingCheck> pending = new();
    private GameBalance Balance => RunState.Instance.Balance;

    private void Awake() => Instance = this;

    private void Start() => DayManager.Instance.onPhaseChanged += HandlePhaseChanged;

    private void OnDestroy()
    {
        if (DayManager.Instance != null)
            DayManager.Instance.onPhaseChanged -= HandlePhaseChanged;
    }

    private void HandlePhaseChanged()
    {
        if (DayManager.Instance.Phase == DayPhase.Shift) return;
        foreach (var check in pending)
            if (check.guest != null) check.guest.IsBeingChecked = false;
        pending.Clear();
    }

    public bool CanCheck(GuestAgent guest) =>
        guest != null
        && DayManager.Instance.Phase == DayPhase.Shift
        && guest.State == GuestState.Seated
        && !guest.IsClaiming
        && !guest.IsBeingChecked;

    public void Check(GuestAgent guest)
    {
        if (!CanCheck(guest)) return;

        guest.IsBeingChecked = true;
        pending.Add(new PendingCheck
        {
            guest = guest,
            timeLeft = Balance.checkSeconds,
            redHanded = guest.WasRecentlyCheating(Balance.redHandedGrace)
        });
    }

    private void Update()
    {
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            var check = pending[i];
            if (check.guest == null) { pending.RemoveAt(i); continue; }

            check.timeLeft -= Time.deltaTime;
            if (check.timeLeft > 0f) continue;

            pending.RemoveAt(i);
            Resolve(check);
        }
    }

    private void Resolve(PendingCheck check)
    {
        var run = RunState.Instance;
        var guest = check.guest;

        if (guest.IsCheater)
        {
            float stars = Balance.liveCatchStars + (check.redHanded ? Balance.redHandedBonusStars : 0f);
            run.ChangeMoney(Balance.liveCatchMoney);
            run.ChangeStars(stars);
            OnResult?.Invoke(check.redHanded
                ? $"Caught {guest.GuestName} red-handed!"
                : $"{guest.GuestName} was cheating. Caught!", true);
            guest.Leave();
        }
        else
        {
            run.ChangeStars(Balance.falseCheckStars);
            guest.IsBeingChecked = false;
            OnResult?.Invoke($"{guest.GuestName} was innocent, and not amused.", false);
        }
    }
}