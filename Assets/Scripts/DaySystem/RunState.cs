using UnityEngine;
using System;

public class RunState : MonoBehaviour
{
    public static RunState Instance {get; private set;}

    public event Action onChanged;
    [SerializeField] private GameBalance balance;
    
    public GameBalance Balance => balance;
    
    public float Stars {get; private set;}
    public int MoneyProtected {get; private set;}
    public int Budget {get; private set;}

    public int moneyAtDayStart;
    public int moneyToday => MoneyProtected - moneyAtDayStart;
    public bool isFired => Stars <= balance.fireAtStars;

    private void Awake()
    {
        Instance = this;
        Stars = balance.startStars;
    }

    public void ChangeStars(float amount)
    {
        Stars = Mathf.Clamp(Stars + amount, 0f, balance.maxStars);
        onChanged?.Invoke();
    }

    public void ChangeMoney(int amount)
    {
        MoneyProtected += amount;
        onChanged?.Invoke();
    }

    public void ChangeBudget(int amount)
    {
        Budget += amount;
        onChanged?.Invoke();
    }

    public void MarkDayStart() => moneyAtDayStart = MoneyProtected;

    public int CalculateDayBonus()
    {
        int fromStars = Mathf.RoundToInt(Stars * balance.bugdetPerStar);
        int fromMoney = Mathf.RoundToInt(Mathf.Max(0, moneyToday) * balance.moneyBonusShare);
        return balance.baseBudget + fromStars + fromMoney;
    }
}
