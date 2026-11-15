using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Interrogation app: suspect list with stress + emotion, chat with the selected suspect, present evidence.</summary>
public class InterrogationPanel
{
    class SuspectCard
    {
        public Image Card;
        public Image StressFill;
        public TextMeshProUGUI Emotion;
        public Image EmotionBg;
        public CanvasGroup Group;
    }

    readonly DeskUI ui;
    readonly GameObject root;
    readonly Dictionary<SuspectAgent, SuspectCard> cards = new();
    readonly ChatView chat;
    readonly TMP_InputField input;
    readonly Button askButton, presentButton;
    readonly TextMeshProUGUI headerName, headerRole, headerEmotion, headerStress, avatarInitials;
    readonly Image headerEmotionBg, headerStressFill, avatar;
    readonly RectTransform portraitSlot;
    SuspectAgent current;
    EvidencePicker picker;

    UiTheme T => UiKit.Theme;
    LeakDeskGame Game => ui.Game;
    public SuspectAgent Current => current;

    /// <summary>Where a 3D portrait (RenderTexture) can be placed by SuspectPortraits.</summary>
    public RectTransform PortraitSlot => portraitSlot;

    public InterrogationPanel(DeskUI ui, Transform parent)
    {
        this.ui = ui;
        var rt = UiKit.Rect("InterrogationPanel", parent).Stretch();
        root = rt.gameObject;
        rt.HLayout(16);
        rt.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;

        // Suspect list
        var list = UiKit.Panel(rt, T.panel, T.panelSprite, "SuspectList");
        list.Size(prefW: 340, flexH: 1);
        list.VLayout(12, 16);
        UiKit.Heading(list.transform, "Suspects", 26);
        UiKit.Text(list.transform, "Everyone who had the deck.", T.smallSize, T.muted);
        foreach (var agent in Game.Suspects) AddSuspectCard(list.transform, agent);
        UiKit.Spacer(list.transform, 0, 1);

        // Room
        var room = UiKit.Panel(rt, T.panel, T.panelSprite, "Room");
        room.Size(flexW: 1, flexH: 1);
        room.VLayout(12, 20);

        var header = UiKit.Rect("Header", room.transform);
        header.HLayout(18);
        header.Size(prefH: 150);
        avatar = UiKit.Panel(header, Color.white, UiKit.AvatarSprite, "Avatar");
        avatar.Size(prefW: 150, prefH: 150, minW: 150);
        portraitSlot = (RectTransform)avatar.transform;
        avatarInitials = UiKit.Text(avatar.transform, "", 56, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        ((RectTransform)avatarInitials.transform).Stretch();

        var info = UiKit.Rect("Info", header);
        info.VLayout(6);
        info.Size(flexW: 1);
        headerName = UiKit.Heading(info, "", 34);
        headerRole = UiKit.Text(info, "", T.bodySize - 2, T.muted);
        var chips = UiKit.Rect("Chips", info);
        chips.HLayout(10);
        headerEmotionBg = UiKit.Chip(chips, T.accent, 130, 34, "Emotion");
        headerEmotion = UiKit.Text(headerEmotionBg.transform, "CALM", T.smallSize, T.cardText, TextAlignmentOptions.Center, FontStyles.Bold);
        headerStress = UiKit.Text(chips, "Stress 0", T.smallSize, T.text, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        headerStress.Size(prefW: 120);
        headerStressFill = UiKit.Bar(chips, T.warning, 16);

        var chatBg = UiKit.Panel(room.transform, new Color(0, 0, 0, 0.22f), T.panelSprite, "ChatBg");
        chatBg.Size(flexH: 1, flexW: 1);
        chat = ChatView.Create(chatBg.transform);
        ((RectTransform)chat.transform).Stretch();

        var inputRow = UiKit.Rect("InputRow", room.transform);
        inputRow.HLayout(10);
        input = UiKit.Input(inputRow, "Ask a question... (Enter to send)");
        input.onSubmit.AddListener(_ => Ask());
        askButton = UiKit.Button(inputRow, "Ask", Ask);
        askButton.Size(prefW: 120, prefH: 56);
        presentButton = UiKit.Button(inputRow, "Present evidence", OpenPicker, T.warning, T.cardText);
        presentButton.Size(prefW: 240, prefH: 56);

        Game.Evidence.Added += _ => UpdateButtons();
        Game.WaveUnlocked += _ => { foreach (var a in Game.Suspects) UpdateCard(a); };
        Game.PhaseChanged += UpdateButtons;
        Select(Game.Suspects[0]);
    }

    public void SetVisible(bool v)
    {
        root.SetActive(v);
        if (v && current != null) { Rebuild(); input.ActivateInputField(); }
    }

    void AddSuspectCard(Transform parent, SuspectAgent agent)
    {
        var card = UiKit.Panel(parent, T.panelAlt, T.cardSprite, "Suspect: " + agent.Data.Id);
        card.HLayout(12, 12);
        card.gameObject.AddComponent<Button>().onClick.AddListener(() => Select(agent));
        card.gameObject.AddComponent<ButtonPop>();

        var av = UiKit.Panel(card.transform, agent.Data.Color, UiKit.AvatarSprite, "Avatar");
        av.Size(prefW: 64, prefH: 64, minW: 64);
        var ini = UiKit.Text(av.transform, Initials(agent.Data.Name), 26, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        ((RectTransform)ini.transform).Stretch();

        var col = UiKit.Rect("Col", card.transform);
        col.VLayout(4);
        col.Size(flexW: 1);
        UiKit.Text(col, $"<b>{agent.Data.Name}</b>", T.bodySize - 1, T.text).textWrappingMode = TextWrappingModes.NoWrap;
        var role = UiKit.Text(col, agent.Data.Role, T.smallSize - 2, T.muted);
        role.textWrappingMode = TextWrappingModes.NoWrap;
        role.overflowMode = TextOverflowModes.Ellipsis;
        var row = UiKit.Rect("Row", col);
        row.HLayout(8);
        var emoBg = UiKit.Chip(row, SourceStyle.EmotionColor("calm"), 92, 24, "Emotion");
        var emo = UiKit.Text(emoBg.transform, "CALM", 13, T.cardText, TextAlignmentOptions.Center, FontStyles.Bold);
        var fill = UiKit.Bar(row, T.warning, 10);

        cards[agent] = new SuspectCard { Card = card, StressFill = fill, Emotion = emo, EmotionBg = emoBg, Group = card.gameObject.AddComponent<CanvasGroup>() };
        agent.Changed += OnAgentChanged;
        UpdateCard(agent);
    }

    void Select(SuspectAgent agent)
    {
        if (!Game.IsSuspectUnlocked(agent.Data.Id))
        {
            ui.Toast($"{agent.Data.FirstName} isn't available yet. New leads unlock more interviews.", T.warning);
            return;
        }
        current = agent;
        agent.Meet();
        foreach (var kv in cards)
            kv.Value.Card.color = kv.Key == agent ? Color.Lerp(T.panelAlt, agent.Data.Color, 0.35f) : T.panelAlt;

        headerName.text = agent.Data.Name;
        headerRole.text = $"{agent.Data.Role}  ·  {agent.Data.Age}";
        avatar.color = agent.Data.Color;
        avatarInitials.text = Initials(agent.Data.Name);
        UpdateHeader();
        Rebuild();
        ui.Tray?.Refresh();
        SuspectPortraits.Instance?.Show(agent.Data);
        if (root.activeInHierarchy) input.ActivateInputField();
    }

    void Rebuild()
    {
        chat.Clear();
        foreach (var e in current.Log) AddEntry(e);
        if (current.Busy) chat.ShowTyping(current.Data.FirstName);
        UpdateButtons();
    }

    void AddEntry(ChatEntry e)
    {
        switch (e.Type)
        {
            case ChatEntry.Kind.Player:
                chat.AddBubble("You", e.Text, ChatView.Side.Right, T.bubblePlayer, T.accentText);
                break;
            case ChatEntry.Kind.Suspect:
                var label = $"{current.Data.FirstName}  <color=#{UiKit.Hex(SourceStyle.EmotionColor(e.Emotion) * 0.75f)}>[{SourceStyle.EmotionLabel(e.Emotion)}]</color>";
                chat.AddBubble(label, e.Text, ChatView.Side.Left, T.bubbleSuspect);
                break;
            case ChatEntry.Kind.Evidence:
                var b = chat.AddBubble($"EVIDENCE PRESENTED  ·  {SourceStyle.Label(e.EvidenceSource)}", e.Text, ChatView.Side.Right,
                    new Color(1f, 0.95f, 0.78f));
                var stripe = UiKit.Panel(b.transform, SourceStyle.Color(e.EvidenceSource), T.pillSprite, "Stripe").Pill(6);
                stripe.Size(prefH: 6);
                stripe.transform.SetAsFirstSibling();
                break;
            case ChatEntry.Kind.System:
                chat.AddBubble(null, $"<i>{e.Text}</i>", ChatView.Side.Center, new Color(1, 1, 1, 0.12f), T.muted);
                break;
        }
    }

    void Ask()
    {
        string q = input.text.Trim();
        if (q.Length == 0 || current == null || current.Busy || !CanAct) return;
        input.text = "";
        var agent = current;
        agent.Ask(q, _ => OnReply(agent));
        AfterSend(agent);
    }

    public void Present(EvidenceItem item)
    {
        if (current == null || current.Busy || !CanAct) return;
        ui.ShowTab(DeskUI.Tab.Interrogate);
        var agent = current;
        agent.Present(item, _ => OnReply(agent));
        AfterSend(agent);
    }

    bool CanAct => Game.Phase == GamePhase.Investigating;

    void AfterSend(SuspectAgent agent)
    {
        // Show the player's message immediately, then the typing indicator.
        AddEntry(agent.Log[^1]);
        chat.ShowTyping(agent.Data.FirstName);
        UpdateButtons();
        input.ActivateInputField();
    }

    void OnReply(SuspectAgent agent)
    {
        if (agent != current) return; // reply is in its Log; shown when the player switches back
        chat.HideTyping();
        AddEntry(agent.Log[^1]);
        UpdateButtons();
        if (root.activeInHierarchy) input.ActivateInputField();
    }

    void OnAgentChanged(SuspectAgent agent)
    {
        UpdateCard(agent);
        if (agent == current) UpdateHeader();
        if (agent.Broken && agent.Stress >= 80)
            SuspectPortraits.Instance?.React(agent.Data, "broken");
        else
            SuspectPortraits.Instance?.React(agent.Data, agent.Emotion);
    }

    void UpdateCard(SuspectAgent agent)
    {
        var c = cards[agent];
        bool unlocked = Game.IsSuspectUnlocked(agent.Data.Id);
        c.Group.alpha = unlocked ? 1f : 0.45f;
        if (!unlocked)
        {
            c.Emotion.text = "LATER";
            c.EmotionBg.color = new Color(0.45f, 0.47f, 0.55f);
            return;
        }
        UiKit.SetBar(c.StressFill, agent.Stress / 100f);
        c.StressFill.color = StressColor(agent.Stress);
        c.Emotion.text = agent.Met ? SourceStyle.EmotionLabel(agent.Emotion) : "NOT MET";
        c.EmotionBg.color = agent.Met ? SourceStyle.EmotionColor(agent.Emotion) : new Color(0.6f, 0.62f, 0.7f);
    }

    void UpdateHeader()
    {
        headerEmotion.text = SourceStyle.EmotionLabel(current.Emotion);
        headerEmotionBg.color = SourceStyle.EmotionColor(current.Emotion);
        headerStress.text = current.Broken ? $"Stress {current.Stress}  <color=#{UiKit.Hex(T.danger)}>CRACKED</color>" : $"Stress {current.Stress}";
        UiKit.SetBar(headerStressFill, current.Stress / 100f);
        headerStressFill.color = StressColor(current.Stress);
        UpdateCard(current);
    }

    void UpdateButtons()
    {
        bool can = current != null && !current.Busy && CanAct;
        askButton.interactable = can;
        presentButton.interactable = can && Game.Evidence.Count > 0;
    }

    Color StressColor(int stress) => stress < 40 ? T.accent : stress < 75 ? T.warning : T.danger;

    void OpenPicker()
    {
        if (current == null || current.Busy) return;
        picker ??= new EvidencePicker(ui);
        picker.Open($"Show evidence to {current.Data.FirstName}", Present);
    }

    public static string Initials(string name)
    {
        var parts = name.Split(' ');
        return parts.Length >= 2 ? $"{parts[0][0]}{parts[^1][0]}" : name.Substring(0, Mathf.Min(2, name.Length));
    }
}
