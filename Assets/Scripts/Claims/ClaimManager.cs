using System;
using System.Collections.Generic;
using UnityEngine;

public class ClaimManager : MonoBehaviour
{
    public static ClaimManager Instance { get; private set; }

    public event Action<Claim> OnClaimAdded;
    public event Action<Claim> OnClaimChanged;
    public event Action<Claim> OnClaimRemoved;
    public event Action OnSelectionChanged;

    [Tooltip("Opened automatically when a shift starts")]
    [SerializeField] private ToolDefinition claimsTool;
    [Tooltip("Opened when the player inspects a claim")]
    [SerializeField] private ToolDefinition cardCheckerTool;

    private readonly List<Claim> claims = new();
    public IReadOnlyList<Claim> Claims => claims;
    public Claim SelectedClaim { get; private set; }

    private float spawnTimer;
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
        var phase = DayManager.Instance.Phase;
        if (phase == DayPhase.Shift)
        {
            spawnTimer = 3f;
            if (claimsTool != null) WindowManager.Instance.Open(claimsTool);
        }
        else if (phase == DayPhase.Review)
        {
            ClearAll();
        }
    }

    private void Update()
    {
        if (DayManager.Instance.Phase != DayPhase.Shift) return;
        float dt = Time.deltaTime;

        spawnTimer -= dt;
        if (spawnTimer <= 0f)
            spawnTimer = TrySpawnClaim() ? DayManager.Instance.CurrentPlan.secondsBetweenClaims : 1f;

        for (int i = claims.Count - 1; i >= 0; i--)
            Tick(claims[i], dt);
    }

    private bool TrySpawnClaim()
    {
        var table = BingoHall.Instance.GetTableReadyForBingo();
        if (table == null) return false;
        
        var guest = HallPopulation.Instance.PickClaimant(table.number);
        if (guest == null) return false; 

        var claim = ClaimGenerator.Create(DayManager.Instance.CurrentPlan, Balance, table, guest.IsCheater);        if (claim == null) return false;
        
        claim.guest = guest;
        claim.guestName = guest.GuestName;
        guest.SetClaiming(true);

        BingoHall.Instance.PauseForClaim(table);
        claims.Add(claim);
        OnClaimAdded?.Invoke(claim);
        return true;
    }

    private void Tick(Claim claim, float dt)
    {
        switch (claim.state)
        {
            case ClaimState.Pending:
                claim.timeLeft -= dt;
                if (claim.timeLeft <= 0f) Resolve(claim, paidOut: true);
                break;

            case ClaimState.Held:
                claim.heldTime += dt;
                if (claim.heldTime >= Balance.maxHoldSeconds)
                {
                    claim.state = ClaimState.Pending;
                    OnClaimChanged?.Invoke(claim);
                }
                break;

            case ClaimState.Checking:
                claim.checkTimeLeft -= dt;
                if (claim.checkTimeLeft <= 0f) Resolve(claim, paidOut: false);
                break;

            case ClaimState.Resolved:
                claim.lingerTimeLeft -= dt;
                if (claim.lingerTimeLeft <= 0f)
                {
                    claims.Remove(claim);
                    Deselect(claim);
                    OnClaimRemoved?.Invoke(claim);
                }
                break;
        }
    }
    
    public bool CanRespond(Claim claim) =>
        claim.state == ClaimState.Pending || claim.state == ClaimState.Held;

    public bool CanHold(Claim claim) =>
        claim.state == ClaimState.Pending && claim.heldTime < Balance.maxHoldSeconds;

    public void Hold(Claim claim)
    {
        if (!CanHold(claim)) return;
        claim.state = ClaimState.Held;
        OnClaimChanged?.Invoke(claim);
    }

    public void Release(Claim claim)
    {
        if (claim.state != ClaimState.Held) return;
        claim.state = ClaimState.Pending;
        OnClaimChanged?.Invoke(claim);
    }

    public void Check(Claim claim)
    {
        if (!CanRespond(claim)) return;
        claim.state = ClaimState.Checking;
        claim.checkTimeLeft = Balance.checkSeconds;
        OnClaimChanged?.Invoke(claim);
    }

    public void Approve(Claim claim)
    {
        if (!CanRespond(claim)) return;
        Resolve(claim, paidOut: true);
    }

    public void Inspect(Claim claim)
    {
        if (claim.guest != null) GuestSelection.Instance.Select(claim.guest);
        SelectedClaim = claim;
        OnSelectionChanged?.Invoke();
        if (cardCheckerTool != null) WindowManager.Instance.Open(cardCheckerTool);
    }

    public void ToggleFlag(Claim claim, int cell)
    {
        if (!CanRespond(claim)) return;
        if (claim.card.numbers[cell] == 0 || !claim.card.marked[cell]) return;

        if (!claim.flaggedCells.Remove(cell))
            claim.flaggedCells.Add(cell);

        OnClaimChanged?.Invoke(claim);
    }
    
    private void Resolve(Claim claim, bool paidOut)
    {
        var run = RunState.Instance;

        int correct = 0, wrong = 0;
        foreach (int cell in claim.flaggedCells)
        {
            if (claim.calledNumbers.Contains(claim.card.numbers[cell])) wrong++;
            else correct++;
        }
        claim.evidenceFound = Mathf.Max(0, correct - wrong);

        if (paidOut && claim.isCheater)
        {
            claim.outcome = ClaimOutcome.CheaterPaid;
            run.ChangeMoney(-claim.prize);
            run.ChangeStars(Balance.cheaterPaidStars);
        }
        else if (paidOut)
        {
            claim.outcome = ClaimOutcome.HonestPaid;
            run.ChangeStars(Balance.honestApprovedStars);
        }
        else if (claim.isCheater)
        {
            claim.outcome = ClaimOutcome.CheaterCaught;
            run.ChangeMoney(claim.prize);
            run.ChangeStars(Balance.cheaterCaughtStars + claim.evidenceFound * Balance.evidenceBonusStars);
        }
        else
        {
            claim.outcome = ClaimOutcome.FalseAccusation;
            run.ChangeStars(Balance.falseCheckStars);
        }

        if (!claim.isCheater && claim.heldTime > 0f)
            run.ChangeStars(-claim.heldTime * Balance.holdStarCostPerSecond);

        claim.state = ClaimState.Resolved;
        claim.lingerTimeLeft = Balance.resolvedClaimLinger;
        
        if (claim.guest != null) claim.guest.Leave();
        
        BingoHall.Instance.StartNewGame(claim.table);
        OnClaimChanged?.Invoke(claim);
    }

    private void Deselect(Claim claim)
    {
        if (SelectedClaim != claim) return;
        SelectedClaim = null;
        OnSelectionChanged?.Invoke();
    }

    private void ClearAll()
    {
        foreach (var claim in claims) OnClaimRemoved?.Invoke(claim);
        claims.Clear();
        SelectedClaim = null;
        OnSelectionChanged?.Invoke();
    }
}