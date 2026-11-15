using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public static class ClaimGenerator
{
    private static readonly string[] FirstNames =
    {
        "Doris", "Gerald", "Margaret", "Kevin", "Brenda", "Walter", "Ingrid", "Lenny",
        "Rosa", "Frank", "Agnes", "Marco", "Trudy", "Otto", "Peggy", "Hank", "Yuki", "Bea"
    };

    private static readonly string[] LastNames =
    {
        "Fischer", "Dempsey", "Moretti", "Huber", "Kowalski", "Fairweather", "Sato",
        "Brennan", "Pettigrew", "Oduya", "Lindqvist", "Marsh", "Bianchi", "Okafor"
    };

    private static readonly int[][] Lines = BuildLines();

    public static Claim Create(DayPlan plan, GameBalance balance, BingoTable table, bool cheater)    {
        var called = table.called;

        var possibleLines = new List<int[]>();
        foreach (var line in Lines)
            if (CanFill(line, called)) possibleLines.Add(line);
        if (possibleLines.Count == 0) return null;

        int[] winLine = possibleLines[Random.Range(0, possibleLines.Count)];
        int fakeCell = cheater ? RandomNonFreeCell(winLine) : -1;

        var card = new BingoCard();
        var used = new HashSet<int>();

        foreach (int cell in winLine)
        {
            if (cell == BingoCard.FreeCell) continue;
            int col = cell % BingoCard.Size;

            int n = -1;
            if (cell == fakeCell)
                n = PickFromColumn(col, x => !called.Contains(x) && !used.Contains(x));
            if (n < 0) 
            {
                n = PickFromColumn(col, x => called.Contains(x) && !used.Contains(x));
                if (cell == fakeCell) cheater = false;
            }

            card.numbers[cell] = n;
            used.Add(n);
        }

        for (int i = 0; i < card.numbers.Length; i++)
        {
            if (i == BingoCard.FreeCell || Array.IndexOf(winLine, i) >= 0) continue;
            int n = PickFromColumn(i % BingoCard.Size, x => !used.Contains(x));
            card.numbers[i] = n;
            used.Add(n);
        }
        card.numbers[BingoCard.FreeCell] = 0;

        for (int i = 0; i < card.numbers.Length; i++)
        {
            int n = card.numbers[i];
            card.marked[i] = n == 0 || called.Contains(n);
        }
        foreach (int cell in winLine) card.marked[cell] = true;

        var claim = new Claim
        {
            guestName = $"{Pick(FirstNames)} {Pick(LastNames)}",
            prize = Mathf.RoundToInt(Random.Range(balance.bingoPrizeMin, balance.bingoPrizeMax) / 10f) * 10,
            isCheater = cheater,
            cheat = cheater ? CheatType.UncalledDaub : CheatType.None,
            table = table,
            card = card,
            calledNumbers = new HashSet<int>(called),
            countdownTotal = plan.claimCountdownSeconds
        };
        claim.timeLeft = claim.countdownTotal;
        return claim;
    }

    private static bool CanFill(int[] line, HashSet<int> called)
    {
        var needed = new int[BingoCard.Size];
        foreach (int cell in line)
            if (cell != BingoCard.FreeCell) needed[cell % BingoCard.Size]++;

        for (int col = 0; col < BingoCard.Size; col++)
        {
            if (needed[col] == 0) continue;
            int available = 0;
            for (int n = col * 15 + 1; n <= col * 15 + 15; n++)
                if (called.Contains(n)) available++;
            if (available < needed[col]) return false;
        }
        return true;
    }

    // B = 1–15, I = 16–30, N = 31–45, G = 46–60, O = 61–75
    private static int PickFromColumn(int col, Func<int, bool> allowed)
    {
        var options = new List<int>();
        for (int n = col * 15 + 1; n <= col * 15 + 15; n++)
            if (allowed(n)) options.Add(n);
        return options.Count == 0 ? -1 : options[Random.Range(0, options.Count)];
    }

    private static int RandomNonFreeCell(int[] line)
    {
        var cells = new List<int>();
        foreach (int cell in line)
            if (cell != BingoCard.FreeCell) cells.Add(cell);
        return cells[Random.Range(0, cells.Count)];
    }

    private static int[][] BuildLines()
    {
        var lines = new List<int[]>();
        for (int r = 0; r < 5; r++)
        {
            var row = new int[5];
            for (int c = 0; c < 5; c++) row[c] = BingoCard.Index(r, c);
            lines.Add(row);
        }
        for (int c = 0; c < 5; c++)
        {
            var col = new int[5];
            for (int r = 0; r < 5; r++) col[r] = BingoCard.Index(r, c);
            lines.Add(col);
        }
        var diagA = new int[5];
        var diagB = new int[5];
        for (int i = 0; i < 5; i++)
        {
            diagA[i] = BingoCard.Index(i, i);
            diagB[i] = BingoCard.Index(i, 4 - i);
        }
        lines.Add(diagA);
        lines.Add(diagB);
        return lines.ToArray();
    }

    private static string Pick(string[] options) => options[Random.Range(0, options.Length)];
    
    public static string RandomName() => $"{Pick(FirstNames)} {Pick(LastNames)}";
}