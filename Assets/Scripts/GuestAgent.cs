using System;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

public enum GuestState { WalkingToSeat, Seated, Leaving }

[RequireComponent(typeof(NavMeshAgent))]
public class GuestAgent : MonoBehaviour
{
    public static event Action<GuestAgent> OnTellStarted;

    [Header("Markers")]
    [SerializeField] private GameObject claimMarker;
    [SerializeField] private GameObject selectionRing;

    [Header("Tell props")]
    [SerializeField] private GameObject phoneProp;
    [SerializeField] private GameObject cardProp;
    [SerializeField] private float phoneLeanAngle = 15f;
    [SerializeField] private float cardTurnAngle = 75f;

    private NavMeshAgent agent;
    private Transform seat;
    private Transform exit;

    private LiveTellType currentTell;
    private float tellTimer;
    private float tellEndsAt = -999f;
    private int tellDirection = 1;

    public string GuestName { get; private set; }
    public HallTable Table { get; private set; }
    public GuestState State { get; private set; }
    public bool IsClaiming { get; private set; }
    public bool IsCheater { get; private set; }
    public bool IsBeingChecked { get; set; }

    public bool IsPerformingTell => Time.time < tellEndsAt;
    public bool WasRecentlyCheating(float grace) => Time.time < tellEndsAt + grace;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (claimMarker != null) claimMarker.SetActive(false);
        if (selectionRing != null) selectionRing.SetActive(false);
        UpdateProps();
    }

    public void Setup(string guestName, bool isCheater, HallTable table, Transform targetSeat, Transform exitPoint)
    {
        GuestName = guestName;
        IsCheater = isCheater;
        Table = table;
        seat = targetSeat;
        exit = exitPoint;
        gameObject.name = $"Guest_{guestName}";

        ResetTellTimer();
        State = GuestState.WalkingToSeat;
        agent.SetDestination(seat.position);
    }

    private void Update()
    {
        switch (State)
        {
            case GuestState.WalkingToSeat:
                if (HasArrived())
                {
                    State = GuestState.Seated;
                    agent.isStopped = true;
                    agent.updateRotation = false; 
                    agent.updateUpAxis = false;   
                }
                break;

            case GuestState.Seated:
                UpdateTell();
                transform.rotation = Quaternion.Slerp(transform.rotation, TargetRotation(), 8f * Time.deltaTime);
                UpdateProps();
                break;

            case GuestState.Leaving:
                if (HasArrived()) Destroy(gameObject);
                break;
        }
    }

    // ---------- Tells ----------

    private void UpdateTell()
    {
        if (!IsCheater || IsClaiming || IsBeingChecked || IsPerformingTell) return;

        var day = DayManager.Instance;
        if (day.Phase != DayPhase.Shift || day.CurrentPlan == null) return;

        var tells = day.CurrentPlan.liveTells;
        if (tells.Count == 0) return;

        tellTimer -= Time.deltaTime;
        if (tellTimer > 0f) return;

        currentTell = tells[Random.Range(0, tells.Count)];
        tellEndsAt = Time.time + RunState.Instance.Balance.tellDuration;
        tellDirection = Random.value < 0.5f ? -1 : 1;
        ResetTellTimer();
        OnTellStarted?.Invoke(this);
    }

    private Quaternion TargetRotation()
    {
        Quaternion facingTable = seat.rotation;
        if (!IsPerformingTell) return facingTable;

        return currentTell switch
        {
            LiveTellType.Phone => facingTable * Quaternion.Euler(phoneLeanAngle, 0f, 0f),
            LiveTellType.CardPass => facingTable * Quaternion.Euler(0f, cardTurnAngle * tellDirection, 0f),
            _ => facingTable
        };
    }

    private void UpdateProps()
    {
        bool active = IsPerformingTell;
        if (phoneProp != null) phoneProp.SetActive(active && currentTell == LiveTellType.Phone);
        if (cardProp != null) cardProp.SetActive(active && currentTell == LiveTellType.CardPass);
    }

    private void ResetTellTimer()
    {
        var balance = RunState.Instance.Balance;
        tellTimer = Random.Range(balance.tellIntervalMin, balance.tellIntervalMax);
    }

    // ---------- Movement and state ----------

    private bool HasArrived() =>
        !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.05f;

    public void SetClaiming(bool claiming)
    {
        IsClaiming = claiming;
        if (claimMarker != null) claimMarker.SetActive(claiming);
    }

    public void SetSelected(bool selected)
    {
        if (selectionRing != null) selectionRing.SetActive(selected);
    }

    public void Leave()
    {
        if (State == GuestState.Leaving) return;

        SetClaiming(false);
        tellEndsAt = -999f;
        UpdateProps();
        
        agent.updateRotation = true;
        agent.updateUpAxis = true;
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        Table.FreeSeat(this);
        State = GuestState.Leaving;
        agent.isStopped = false;
        agent.SetDestination(exit.position);
    }
}