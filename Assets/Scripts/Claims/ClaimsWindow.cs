using System.Collections.Generic;
using UnityEngine;

public class ClaimsWindow : MonoBehaviour
{
    [SerializeField] private ClaimRow rowPrefab;
    [SerializeField] private Transform rowContainer;

    private readonly Dictionary<Claim, ClaimRow> rows = new();

    private void Start()
    {
        var manager = ClaimManager.Instance;
        manager.OnClaimAdded += AddRow;
        manager.OnClaimChanged += RefreshRow;
        manager.OnClaimRemoved += RemoveRow;

        foreach (var claim in manager.Claims) AddRow(claim);
    }

    private void OnDestroy()
    {
        var manager = ClaimManager.Instance;
        if (manager == null) return;
        manager.OnClaimAdded -= AddRow;
        manager.OnClaimChanged -= RefreshRow;
        manager.OnClaimRemoved -= RemoveRow;
    }

    private void AddRow(Claim claim)
    {
        if (rows.ContainsKey(claim)) return;
        var row = Instantiate(rowPrefab, rowContainer);
        row.Bind(claim);
        rows[claim] = row;
    }

    private void RefreshRow(Claim claim)
    {
        if (rows.TryGetValue(claim, out var row)) row.Refresh();
    }

    private void RemoveRow(Claim claim)
    {
        if (rows.TryGetValue(claim, out var row))
        {
            Destroy(row.gameObject);
            rows.Remove(claim);
        }
    }
}