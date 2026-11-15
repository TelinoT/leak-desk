using System.Collections.Generic;

public enum ClaimState { Pending, Held, Checking, Resolved }
public enum CheatType { None, UncalledDaub }
public enum ClaimOutcome { None, HonestPaid, CheaterCaught, FalseAccusation, CheaterPaid }

public class BingoCard
{
    public const int Size = 5;
    public const int FreeCell = 12; // center: row 2, column 2
    public readonly int[] numbers = new int[Size * Size]; // 0 = free space
    public readonly bool[] marked = new bool[Size * Size];

    public static int Index(int row, int col) => row * Size + col;
}

public class Claim
{
    public string guestName;
    public int prize;
    public bool isCheater;
    public CheatType cheat;

    public BingoTable table;
    public BingoCard card;
    public HashSet<int> calledNumbers; 

    public ClaimState state = ClaimState.Pending;
    public ClaimOutcome outcome = ClaimOutcome.None;

    public float countdownTotal;
    public float timeLeft;
    public float heldTime;
    public float checkTimeLeft;
    public float lingerTimeLeft;

    public readonly HashSet<int> flaggedCells = new(); 
    public int evidenceFound;  
    
    public GuestAgent guest; 
}