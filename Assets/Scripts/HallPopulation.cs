using System.Collections.Generic;
using UnityEngine;

public class HallPopulation : MonoBehaviour
{
    public static HallPopulation Instance { get; private set; }

    [SerializeField] private GuestAgent guestPrefab;
    [Tooltip("Character models picked at random for each guest")]
    [SerializeField] private GameObject[] characterModels;
    [SerializeField] private Transform entrance;
    [SerializeField] private List<HallTable> tables = new();

    private readonly List<GuestAgent> guests = new();
    private float spawnTimer;

    private GameBalance Balance => RunState.Instance.Balance;

    private void Awake() => Instance = this;

    private void Start()
    {
        var day = DayManager.Instance;
        day.onPhaseChanged += HandlePhaseChanged;
        if (day.CurrentPlan != null && day.Phase == DayPhase.Briefing) PrepareDay();
    }

    private void OnDestroy()
    {
        if (DayManager.Instance != null)
            DayManager.Instance.onPhaseChanged -= HandlePhaseChanged;
    }

    private void HandlePhaseChanged()
    {
        var phase = DayManager.Instance.Phase;
        if (phase == DayPhase.Briefing) PrepareDay();
        else if (phase == DayPhase.Review) SendEveryoneHome();
    }

    private void PrepareDay()
    {
        foreach (var guest in guests)
            if (guest != null) Destroy(guest.gameObject);
        guests.Clear();

        int openTables = DayManager.Instance.CurrentPlan.bingoTables;
        foreach (var table in tables)
            table.gameObject.SetActive(table.TableNumber <= openTables);

        spawnTimer = 0f;
    }

    private void Update()
    {
        if (DayManager.Instance.Phase != DayPhase.Shift) return;

        guests.RemoveAll(g => g == null); // forget guests who left and were destroyed

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            spawnTimer = Balance.guestSpawnInterval;
            TrySpawnGuest();
        }
    }

    private void TrySpawnGuest()
    {
        int wanted = DayManager.Instance.CurrentPlan.guestsPerTable;

        var candidates = new List<HallTable>();
        foreach (var table in tables)
            if (table.gameObject.activeSelf && table.FreeSeats > 0 && GuestsAt(table) < wanted)
                candidates.Add(table);
        if (candidates.Count == 0) return;

        var chosen = candidates[Random.Range(0, candidates.Count)];
        var guest = Instantiate(guestPrefab, entrance.position, entrance.rotation, transform);

        if (characterModels.Length > 0)
            Instantiate(characterModels[Random.Range(0, characterModels.Length)], guest.transform);

        chosen.TryReserveSeat(guest, out var seat);
        bool cheater = Random.value < DayManager.Instance.CurrentPlan.cheaterChance;
        guest.Setup(ClaimGenerator.RandomName(), cheater, chosen, seat, entrance);
        guests.Add(guest);
    }

    private int GuestsAt(HallTable table)
    {
        int count = 0;
        foreach (var guest in guests)
            if (guest != null && guest.Table == table && guest.State != GuestState.Leaving)
                count++;
        return count;
    }

    public GuestAgent PickClaimant(int tableNumber)
    {
        var options = new List<GuestAgent>();
        foreach (var guest in guests)
            if (guest != null && guest.State == GuestState.Seated && !guest.IsClaiming
                && !guest.IsBeingChecked && guest.Table.TableNumber == tableNumber)
                options.Add(guest);

        return options.Count == 0 ? null : options[Random.Range(0, options.Count)];
    }

    private void SendEveryoneHome()
    {
        foreach (var guest in guests)
            if (guest != null) guest.Leave();
    }
}