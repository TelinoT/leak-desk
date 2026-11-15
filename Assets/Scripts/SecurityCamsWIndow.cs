using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SecurityCamsWIndow : MonoBehaviour
{
    [SerializeField] private List<SecurityCamera> cameras = new();

    [Header("Grid of feeds")]
    [SerializeField] private CamFeedView feedPrefab;
    [SerializeField] private Transform feedGrid;

    [Header("Single big feed")]
    [SerializeField] private GameObject bigView;
    [SerializeField] private CamFeedView bigFeed;
    [SerializeField] private Button backButton;

    [Header("Selection")]
    [SerializeField] private TMP_Text selectionText;
    
    [SerializeField] private Button checkGuestButton;
    [SerializeField] private TMP_Text activityText;

    private readonly Queue<string> activity = new();

    private bool built;

    private void OnEnable()
    {
        Build();
        foreach (var cam in cameras) cam.SetLive(true);
        GuestSelection.Instance.OnSelectionChanged += RefreshSelection;
        ShowGrid();
        RefreshSelection();
        LiveChecks.Instance.OnResult += AddActivity;
    }

    private void OnDisable()
    {
        foreach (var cam in cameras)
            if (cam != null) cam.SetLive(false);

        if (GuestSelection.Instance != null)
            GuestSelection.Instance.OnSelectionChanged -= RefreshSelection;
        
        if (LiveChecks.Instance != null) LiveChecks.Instance.OnResult -= AddActivity;
    }

    private void Update()
    {
        // The selected guest's status changes over time (arriving, playing, claiming)
        if (GuestSelection.Instance.Selected != null) RefreshSelection();
    }

    private void Build()
    {
        if (built) return;
        built = true;

        foreach (var cam in cameras)
        {
            var feed = Instantiate(feedPrefab, feedGrid);
            feed.Bind(cam);
            feed.OnExpandClicked += Expand;
        }
        backButton.onClick.AddListener(ShowGrid);
        
        checkGuestButton.onClick.AddListener(() =>
            LiveChecks.Instance.Check(GuestSelection.Instance.Selected));
    }

    private void Expand(CamFeedView feed)
    {
        bigFeed.Bind(feed.Source);
        feedGrid.gameObject.SetActive(false);
        bigView.SetActive(true);
    }

    private void ShowGrid()
    {
        bigView.SetActive(false);
        feedGrid.gameObject.SetActive(true);
    }

    private void RefreshSelection()
    {
        var guest = GuestSelection.Instance.Selected;
        selectionText.text = guest == null
            ? "Click a guest on a feed to select them."
            : $"Selected: {guest.GuestName} · Table {guest.Table.TableNumber} · {Describe(guest)}";
        checkGuestButton.interactable = LiveChecks.Instance.CanCheck(guest);
    }

    private static string Describe(GuestAgent guest)
    {
        if (guest.IsBeingChecked) return "guard is checking...";
        if (guest.IsClaiming) return "claiming bingo!";
        return guest.State switch
        {
            GuestState.WalkingToSeat => "arriving",
            GuestState.Seated => "playing",
            _ => "leaving"
        };
    }
    
    private void AddActivity(string message, bool good)
    {
        string color = good ? "#7BE07B" : "#FF7B6B";
        activity.Enqueue($"<color={color}>{message}</color>");
        while (activity.Count > 4) activity.Dequeue();
        activityText.text = string.Join("\n", activity);
    }
}