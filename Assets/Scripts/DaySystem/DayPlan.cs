using System.Collections.Generic;
using UnityEngine;

public enum DayType { Game, Cheat, Tool, Shop, Event, Endless }
public enum LiveTellType { Phone, CardPass}

[CreateAssetMenu(menuName = "Too Lucky/Day Plan", fileName = "Day_")]
public class DayPlan : ScriptableObject
{
    public DayType dayType;
    public string headline = " A quiet evening.";
    [TextArea(4, 10)] public string managerEmail;
    public float shiftSeconds = 150f;
    
    [Header("Unlocks")]
    public List<ToolDefinition> toolsToUnlock = new();
    
    [Header("Bingo")]
    [Min(1)] public int bingoTables = 1;
    [Min(1)] public int guestsPerTable = 4;
    
    [Header("Live cheats")]
    public List<LiveTellType> liveTells = new();
    
    [Header("Claims")]
    public float cheaterChance = 0.3f;
    public float claimCountdownSeconds = 25f;
    public float secondsBetweenClaims = 15f;
    
}
