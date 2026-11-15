using System.Collections.Generic;
using UnityEngine;

public class BingoTable
{
    public readonly int number;
    public readonly List<int> calledOrder = new();
    public readonly HashSet<int> called = new();

    public bool waitingForVerdict;
    public float callTimer;

    public BingoTable(int number) => this.number = number;

    public int LastCalled => calledOrder.Count > 0 ? calledOrder[calledOrder.Count - 1] : 0;

    public void Reset()
    {
        calledOrder.Clear();
        called.Clear();
        waitingForVerdict = false;
    }

    public bool CallNext()
    {
        if (called.Count >= 75) return false;

        int n;
        do n = Random.Range(1, 76);
        while (called.Contains(n));

        called.Add(n);
        calledOrder.Add(n);
        return true;
    }
}
