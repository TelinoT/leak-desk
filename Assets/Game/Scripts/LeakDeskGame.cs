using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GamePhase { Briefing, Investigating, Accusing, Deliberating, Verdict }

/// <summary>
/// Entry point of the Leak Desk scene: loads the case, owns all game state (evidence, suspects, Jensenbot, timer)
/// and builds the UI. Put one on an empty GameObject.
/// </summary>
public class LeakDeskGame : MonoBehaviour
{
    public static LeakDeskGame Instance { get; private set; }

    [SerializeField] string caseId = CaseLoader.DefaultCaseId;
    [Tooltip("Seconds the board waits once the clock hits the deadline before it takes whatever you have.")]
    [SerializeField] float graceSeconds = 45f;
    [Tooltip("Debug: shorten the round (0 = use the case's round length).")]
    [SerializeField] float debugRoundSeconds = 0f;

    public CaseFile Case { get; private set; }
    public EvidenceInventory Evidence { get; private set; }
    public List<SuspectAgent> Suspects { get; } = new();
    public JensenbotAgent Jensenbot { get; private set; }
    public GamePhase Phase { get; private set; } = GamePhase.Briefing;
    public float RoundSeconds { get; private set; }
    public float Elapsed { get; private set; }
    public float TimeLeft => Mathf.Max(0f, RoundSeconds - Elapsed);
    public bool Overtime { get; private set; }
    public float GraceLeft { get; private set; }
    public Verdict LastVerdict { get; private set; }

    public event Action PhaseChanged;

    /// <summary>Raised when a new batch of documents/suspects arrives.</summary>
    public event Action<CaseWave> WaveUnlocked;

    readonly HashSet<string> unlockedWaves = new();
    readonly HashSet<string> seenTriggers = new();
    float lastUnlockAt;
    const float MinWaveGap = 40f; // never drop two batches of reading on the player back to back

    DeskUI ui;

    void Awake()
    {
        Instance = this;
        Case = CaseLoader.Load(caseId);
        if (Case == null) { enabled = false; return; }

        RoundSeconds = debugRoundSeconds > 0 ? debugRoundSeconds : Case.RoundSeconds;
        Evidence = new EvidenceInventory(Case);
        Jensenbot = new JensenbotAgent(Case);
        foreach (var s in Case.Suspects)
        {
            var agent = new SuspectAgent(Case, s);
            agent.FactRevealed += OnFactRevealed;
            agent.Changed += _ => Trigger("any_question");
            Suspects.Add(agent);
        }
        Evidence.Added += item =>
        {
            Trigger(item.Id);
            if (item.FactId != null) Trigger(item.FactId);
            CheckLeads();
        };
        Evidence.Removed += _ => CheckLeads();

        // Waves due at t=0 are open from the start (no announcement).
        foreach (var w in Case.Waves)
            if (w.AfterSeconds <= 0) unlockedWaves.Add(w.Id);
    }

    void Start()
    {
        if (Case == null) return;
        ui = DeskUI.Build(this);
        ui.ShowBriefing();
    }

    /// <summary>Freezes the round clock and timed deliveries (used by the tutorial).</summary>
    public bool Paused { get; set; }

    void Update()
    {
        if (Phase is not (GamePhase.Investigating or GamePhase.Accusing) || Paused) return;

        if (!Overtime)
        {
            Elapsed += Time.deltaTime;
            var next = NextLockedWave;
            if (next != null)
            {
                bool due = Elapsed >= next.AfterSeconds;
                bool earned = Elapsed - lastUnlockAt >= MinWaveGap && next.Triggers.Exists(seenTriggers.Contains);
                if (due || earned) UnlockNextWave();
            }
            if (Elapsed >= RoundSeconds)
            {
                Elapsed = RoundSeconds;
                Overtime = true;
                GraceLeft = graceSeconds;
                OpenAccusation(forced: true);
            }
        }
        else
        {
            GraceLeft -= Time.deltaTime;
            if (GraceLeft <= 0f) ui.Accusation.SubmitNow(timedOut: true);
        }
    }

    // ---------- flow ----------

    public void StartInvestigation()
    {
        if (Phase != GamePhase.Briefing) return;
        SetPhase(GamePhase.Investigating);
    }

    public void OpenAccusation(bool forced)
    {
        if (Phase is GamePhase.Deliberating or GamePhase.Verdict) return;
        SetPhase(GamePhase.Accusing);
        ui.Accusation.Open(forced || Overtime);
    }

    public void CancelAccusation()
    {
        if (Phase != GamePhase.Accusing || Overtime) return;
        SetPhase(GamePhase.Investigating);
    }

    public void SubmitAccusation(Accusation accusation)
    {
        if (Phase is GamePhase.Deliberating or GamePhase.Verdict) return;
        SetPhase(GamePhase.Deliberating);
        (accusation.LeadsFound, accusation.LeadsTotal) = Board.Progress(Case, Evidence.Items, Jensenbot.Exposed);
        accusation.TimeLeft01 = RoundSeconds <= 0 ? 0 : TimeLeft / RoundSeconds;
        accusation.UsedAi = Suspects.Any(s => s.Broken) || Jensenbot.Exposed;
        accusation.Known = Evidence.Items.ToList();
        ui.ShowDeliberation(accusation);
        Judge.Run(Case, accusation, verdict =>
        {
            LastVerdict = verdict;
            SetPhase(GamePhase.Verdict);
            ui.ShowVerdict(accusation, verdict);
        });
    }

    public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

    void SetPhase(GamePhase phase)
    {
        Phase = phase;
        PhaseChanged?.Invoke();
    }

    void OnFactRevealed(SuspectAgent agent, CaseFact fact)
    {
        var item = Evidence.AddTestimony(agent.Data, fact);
        if (item != null && ui != null) ui.Toast($"{agent.Data.FirstName} let something slip. Added to evidence.", SourceStyle.Color("testimony"));
    }

    // ---------- desk searches ----------

    readonly HashSet<string> searchedSpots = new();

    public int SpotsSearched => searchedSpots.Count;

    public bool IsSpotSearched(string suspectId, string spotId) => searchedSpots.Contains(suspectId + ":" + spotId);

    /// <summary>Searches a desk spot: costs clock time, may add evidence. Returns the evidence (or null).</summary>
    public EvidenceItem SearchSpot(string suspectId, DeskSpot spot)
    {
        if (Phase != GamePhase.Investigating || !searchedSpots.Add(suspectId + ":" + spot.Id)) return null;
        SpendTime(Case.DeskSearchSeconds);
        return string.IsNullOrEmpty(spot.EvidenceId) ? null : Evidence.AddCaseEvidence(spot.EvidenceId);
    }

    /// <summary>Moves the round clock forward (actions that take time in-fiction).</summary>
    public void SpendTime(float seconds)
    {
        if (Overtime) return;
        Elapsed = Mathf.Min(RoundSeconds - 1f, Elapsed + seconds);
    }

    // ---------- suspect board ----------

    readonly Dictionary<string, CellStatus> boardState = new();
    bool aiFound;

    /// <summary>(suspect, slot, new status) whenever a board entry changes. Suspect is null for the AI question.</summary>
    public event Action<string, string, CellStatus> BoardChanged;

    /// <summary>Re-evaluates the board (also call after Jensenbot gets exposed).</summary>
    public void CheckLeads()
    {
        foreach (var row in Case.Board.Rows)
            foreach (var slot in Board.Slots)
            {
                var st = Board.Status(Case, Evidence.Items, row.Suspect, slot);
                string key = row.Suspect + ":" + slot;
                boardState.TryGetValue(key, out var before);
                if (st == before) continue;
                boardState[key] = st;
                BoardChanged?.Invoke(row.Suspect, slot, st);
            }
        bool ai = Board.AiFound(Case, Evidence.Items, Jensenbot.Exposed);
        if (ai != aiFound)
        {
            aiFound = ai;
            BoardChanged?.Invoke(null, "ai", ai ? CellStatus.Supported : CellStatus.Unknown);
        }
    }

    // ---------- progressive unlocks ----------

    public bool IsWaveUnlocked(CaseWave w) => unlockedWaves.Contains(w.Id);

    public bool IsDocumentUnlocked(string docId)
    {
        var wave = Case.Waves.Find(w => w.Documents.Contains(docId));
        return wave == null || unlockedWaves.Contains(wave.Id);
    }

    public bool IsSuspectUnlocked(string suspectId)
    {
        var wave = Case.Waves.Find(w => w.Suspects.Contains(suspectId));
        return wave == null || unlockedWaves.Contains(wave.Id);
    }

    /// <summary>Seconds until the next wave arrives on its timer (or -1 if everything is unlocked).</summary>
    public float NextWaveIn
    {
        get
        {
            var next = NextLockedWave;
            return next == null ? -1 : Mathf.Max(0, next.AfterSeconds - Elapsed);
        }
    }

    CaseWave NextLockedWave => Case.Waves.Find(w => !unlockedWaves.Contains(w.Id));

    /// <summary>Records a discovery. Update() opens the next wave early if it lists one (after a minimum gap).</summary>
    void Trigger(string id)
    {
        if (!string.IsNullOrEmpty(id)) seenTriggers.Add(id);
    }

    void UnlockNextWave()
    {
        var next = NextLockedWave;
        if (next == null) return;
        unlockedWaves.Add(next.Id);
        lastUnlockAt = Elapsed;
        WaveUnlocked?.Invoke(next);
    }

    // ---------- clock ----------

    /// <summary>In-fiction wall clock, running from the case's start time to the deadline over the round.</summary>
    public string ClockText
    {
        get
        {
            int start = ParseMinutes(Case.StartTime, 8 * 60 + 48);
            int end = ParseMinutes(Case.DeadlineTime, 9 * 60);
            if (end <= start) end += 24 * 60;
            float minutes = Mathf.Lerp(start, end, RoundSeconds <= 0 ? 1 : Elapsed / RoundSeconds);
            int m = Mathf.FloorToInt(minutes);
            return $"{m / 60 % 24:00}:{m % 60:00}";
        }
    }

    public string CountdownText
    {
        get
        {
            if (Overtime) return $"Board is seated! {Mathf.CeilToInt(Mathf.Max(0, GraceLeft))}s";
            int s = Mathf.CeilToInt(TimeLeft);
            return $"{s / 60}:{s % 60:00} left";
        }
    }

    static int ParseMinutes(string hhmm, int fallback)
    {
        if (string.IsNullOrEmpty(hhmm)) return fallback;
        var parts = hhmm.Split(':');
        return parts.Length == 2 && int.TryParse(parts[0], out int h) && int.TryParse(parts[1], out int m) ? h * 60 + m : fallback;
    }

    public SuspectAgent Agent(string id) => Suspects.FirstOrDefault(s => s.Data.Id == id);
}
