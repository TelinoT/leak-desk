using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardCheckerWindow : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private BingoCellView cellPrefab;
    [SerializeField] private Transform cardGrid;
    [SerializeField] private Transform callBoardGrid;
    [SerializeField] private GameObject emptyState;
    [SerializeField] private GameObject claimView;

    [Header("Table tabs")]
    [SerializeField] private Button tabPrefab;
    [SerializeField] private Transform tabContainer;

    [Header("Info and actions")]
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private TMP_Text evidenceText;
    [SerializeField] private Button checkButton;
    [SerializeField] private Button approveButton;

    [Header("Colors")]
    [SerializeField] private Color unmarked = new(0.16f, 0.17f, 0.20f);
    [SerializeField] private Color daubed = new(0.25f, 0.45f, 0.85f);
    [SerializeField] private Color flagged = new(0.95f, 0.55f, 0.15f);
    [SerializeField] private Color fake = new(0.85f, 0.30f, 0.30f);
    [SerializeField] private Color free = new(0.40f, 0.40f, 0.45f);
    [SerializeField] private Color boardOff = new(0.14f, 0.15f, 0.18f);
    [SerializeField] private Color boardCalled = new(0.95f, 0.80f, 0.30f);
    [SerializeField] private Color boardLast = new(1f, 1f, 1f);
    [SerializeField] private Color dimText = new(0.55f, 0.55f, 0.60f);
    [SerializeField] private Color tabNormal = new(0.20f, 0.21f, 0.25f);
    [SerializeField] private Color tabSelected = new(0.38f, 0.40f, 0.50f);

    private readonly BingoCellView[] cardCells = new BingoCellView[25];
    private readonly BingoCellView[] boardCells = new BingoCellView[75];
    private readonly List<Button> tabs = new();
    private int selectedTable;
    private bool built;

    private Claim Selected => ClaimManager.Instance.SelectedClaim;

    private void OnEnable()
    {
        Build();

        var manager = ClaimManager.Instance;
        manager.OnSelectionChanged += RefreshCard;
        manager.OnClaimChanged += HandleClaimChanged;

        var hall = BingoHall.Instance;
        hall.OnTablesChanged += RebuildTabs;
        hall.OnBoardChanged += HandleBoardChanged;

        RebuildTabs();
        RefreshCard();
    }

    private void OnDisable()
    {
        var manager = ClaimManager.Instance;
        if (manager != null)
        {
            manager.OnSelectionChanged -= RefreshCard;
            manager.OnClaimChanged -= HandleClaimChanged;
        }

        var hall = BingoHall.Instance;
        if (hall != null)
        {
            hall.OnTablesChanged -= RebuildTabs;
            hall.OnBoardChanged -= HandleBoardChanged;
        }
    }

    private void Build()
    {
        if (built) return;
        built = true;

        for (int i = 0; i < 25; i++)
        {
            int cell = i; 
            var view = Instantiate(cellPrefab, cardGrid);
            view.Button.onClick.AddListener(() =>
            {
                if (Selected != null) ClaimManager.Instance.ToggleFlag(Selected, cell);
            });
            cardCells[i] = view;
        }

        for (int n = 1; n <= 75; n++)
        {
            var view = Instantiate(cellPrefab, callBoardGrid);
            view.Button.interactable = false;
            boardCells[n - 1] = view;
        }

        checkButton.onClick.AddListener(() =>
        {
            if (Selected != null) ClaimManager.Instance.Check(Selected);
        });
        approveButton.onClick.AddListener(() =>
        {
            if (Selected != null) ClaimManager.Instance.Approve(Selected);
        });
    }
    
    private void RebuildTabs()
    {
        foreach (var tab in tabs) Destroy(tab.gameObject);
        tabs.Clear();

        var tables = BingoHall.Instance.Tables;
        for (int i = 0; i < tables.Count; i++)
        {
            int index = i;
            var tab = Instantiate(tabPrefab, tabContainer);
            tab.GetComponentInChildren<TMP_Text>().text = $"Table {tables[i].number}";
            tab.onClick.AddListener(() => SelectTable(index));
            tabs.Add(tab);
        }

        selectedTable = Mathf.Clamp(selectedTable, 0, Mathf.Max(0, tables.Count - 1));
        RefreshTabs();
        RefreshBoard();
    }

    private void SelectTable(int index)
    {
        selectedTable = index;
        RefreshTabs();
        RefreshBoard();
    }

    private void RefreshTabs()
    {
        for (int i = 0; i < tabs.Count; i++)
            tabs[i].image.color = i == selectedTable ? tabSelected : tabNormal;
    }

    private void HandleBoardChanged(BingoTable table)
    {
        var tables = BingoHall.Instance.Tables;
        if (selectedTable < tables.Count && tables[selectedTable] == table)
            RefreshBoard();
    }

    private void RefreshBoard()
    {
        var tables = BingoHall.Instance.Tables;
        BingoTable table = tables.Count > 0 ? tables[selectedTable] : null;
        int last = table?.LastCalled ?? 0;

        for (int n = 1; n <= 75; n++)
        {
            bool called = table != null && table.called.Contains(n);
            Color bg = n == last ? boardLast : called ? boardCalled : boardOff;
            boardCells[n - 1].Show(n.ToString(), bg, called ? Color.black : dimText);
        }
    }
    
    private void HandleClaimChanged(Claim claim)
    {
        if (claim == Selected) RefreshCard();
    }

    private void RefreshCard()
    {
        var claim = Selected;
        emptyState.SetActive(claim == null);
        claimView.SetActive(claim != null);
        if (claim == null) return;

        bool resolved = claim.state == ClaimState.Resolved;
        headerText.text = $"{claim.guestName} · ${claim.prize} · Table {claim.table.number}";

        for (int i = 0; i < 25; i++)
        {
            int n = claim.card.numbers[i];
            var view = cardCells[i];

            if (n == 0)
            {
                view.Show("FREE", free, Color.white);
            }
            else if (!claim.card.marked[i])
            {
                view.Show(n.ToString(), unmarked, dimText);
            }
            else
            {
                bool isFake = !claim.calledNumbers.Contains(n);
                bool isFlagged = claim.flaggedCells.Contains(i);
                Color bg = resolved && isFake ? fake : isFlagged ? flagged : daubed;
                view.Show(n.ToString(), bg, Color.white);
            }
        }

        if (!resolved)
        {
            evidenceText.text = claim.flaggedCells.Count == 0
                ? "Compare with the table's board. Click a daubed number to flag it as never called."
                : $"Flagged {claim.flaggedCells.Count} number(s) as never called.";
        }
        else
        {
            evidenceText.text = claim.isCheater
                ? $"Cheater! The red number was never called. Evidence counted: {claim.evidenceFound}"
                : "Honest win: every daubed number was called.";
        }

        bool canRespond = ClaimManager.Instance.CanRespond(claim);
        checkButton.interactable = canRespond;
        approveButton.interactable = canRespond;
    }
}