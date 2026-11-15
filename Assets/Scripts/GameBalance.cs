using UnityEngine;

[CreateAssetMenu(menuName = "Too Lucky/Game Balance", fileName = "GameBalance")]
public class GameBalance : ScriptableObject
{
    [Header("Stars")] 
    public float startStars = 4f;
    public float fireAtStars = 1f;
    public float maxStars = 5f;

    [Header("End of day Budget")] public int baseBudget = 100;
    public int bugdetPerStar = 50;
    public float moneyBonusShare = 0.1f;

    [Header("Clock")] public int shiftStartHour = 18;
    public int shiftEndHour = 2;
    
    [Header("Claims")]
    public int bingoPrizeMin = 50;
    public int bingoPrizeMax = 400;
    public float resolvedClaimLinger = 3f;

    [Header("Responses")]
    public float checkSeconds = 4f;
    public float maxHoldSeconds = 20f;
    public float holdStarCostPerSecond = 0.02f;

    [Header("Outcomes (stars)")]
    public float honestApprovedStars = 0.05f;
    public float cheaterCaughtStars = 0.15f;
    public float evidenceBonusStars = 0.05f;
    public float falseCheckStars = -0.5f;
    public float cheaterPaidStars = -0.3f;
    
    [Header("Bingo tables")]
    public float secondsBetweenCalls = 1.5f;
    public int minCallsBeforeBingo = 15;
    public int shiftStartCallsMin = 8;
    public int shiftStartCallsMax = 15;
    
    [Header("Guests")]
    public float guestSpawnInterval = 2f;
    
    [Header("Live tells")]
    public float tellIntervalMin = 12f;
    public float tellIntervalMax = 25f;
    public float tellDuration = 3f;
    public float redHandedGrace = 3f;

    [Header("Live catches (optional bonus)")]
    public int liveCatchMoney = 150;
    public float liveCatchStars = 0.2f;
    public float redHandedBonusStars = 0.1f;
}
