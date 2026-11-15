using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ClaimRow : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text prizeText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Image countdownFill;

    [SerializeField] private Button holdButton;
    [SerializeField] private TMP_Text holdLabel;
    [SerializeField] private Button checkButton;
    [SerializeField] private Button approveButton;
    [SerializeField] private Button inspectButton;

    [Header("Debug")]
    [SerializeField] private bool revealCheaters;

    private Claim claim;

    private static readonly Color Neutral = new(0.85f, 0.85f, 0.85f);
    private static readonly Color Good = new(0.45f, 0.85f, 0.5f);
    private static readonly Color Bad = new(0.95f, 0.45f, 0.4f);

    public void Bind(Claim boundClaim)
    {
        claim = boundClaim;
        nameText.text = claim.guestName + (revealCheaters && claim.isCheater ? "  [CHEATER]" : "");
        prizeText.text = $"${claim.prize} | Table {claim.table.number}";

        var manager = ClaimManager.Instance;
        holdButton.onClick.AddListener(() =>
        {
            if (claim.state == ClaimState.Held) manager.Release(claim);
            else manager.Hold(claim);
        });
        checkButton.onClick.AddListener(() => manager.Check(claim));
        approveButton.onClick.AddListener(() => manager.Approve(claim));
        inspectButton.onClick.AddListener(() => manager.Inspect(claim));

        Refresh();
    }

    public void Refresh()
    {
        var manager = ClaimManager.Instance;
        bool canRespond = manager.CanRespond(claim);

        holdButton.interactable = claim.state == ClaimState.Held || manager.CanHold(claim);
        checkButton.interactable = canRespond;
        approveButton.interactable = canRespond;
        holdLabel.text = claim.state == ClaimState.Held ? "Release" : "Hold";
    }

    private void Update()
    {
        if (claim == null) return;
        countdownFill.fillAmount = Mathf.Clamp01(claim.timeLeft / claim.countdownTotal);
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        float patience = RunState.Instance.Balance.maxHoldSeconds - claim.heldTime;
        statusText.color = Neutral;

        switch (claim.state)
        {
            case ClaimState.Pending:
                statusText.text = $"Cashing out in {Mathf.CeilToInt(claim.timeLeft)}s";
                break;
            case ClaimState.Held:
                statusText.text = $"On hold | patience {Mathf.CeilToInt(patience)}s";
                break;
            case ClaimState.Checking:
                statusText.text = "Guard is checking...";
                break;
            case ClaimState.Resolved:
                SetOutcomeText();
                break;
        }
    }

    private void SetOutcomeText()
    {
        switch (claim.outcome)
        {
            case ClaimOutcome.HonestPaid:
                statusText.text = "Paid | honest win";
                statusText.color = Good;
                break;
            case ClaimOutcome.CheaterCaught:
                statusText.text = $"Caught a cheater! +${claim.prize}";
                statusText.color = Good;
                break;
            case ClaimOutcome.FalseAccusation:
                statusText.text = "Innocent guest, and they're not happy";
                statusText.color = Bad;
                break;
            case ClaimOutcome.CheaterPaid:
                statusText.text = $"Paid out to a cheater: -${claim.prize}";
                statusText.color = Bad;
                break;
        }
    }
}