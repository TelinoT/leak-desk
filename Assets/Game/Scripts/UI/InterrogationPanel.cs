using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interrogation app. Left: suspect list (headshots, emotion, stress). Center: the "stage" with the live 3D suspect,
/// nameplate, emotion chip and heart-rate monitor; evidence can be dragged onto it. Right: the conversation.
/// </summary>
public class InterrogationPanel
{
    class SuspectCard
    {
        public Image Card;
        public Image StressFill;
        public TextMeshProUGUI Emotion;
        public Image EmotionBg;
        public CanvasGroup Group;
        public RawImage Headshot;
    }

    readonly DeskUI ui;
    readonly GameObject root;
    readonly Dictionary<SuspectAgent, SuspectCard> cards = new();
    readonly ChatView chat;
    readonly TMP_InputField input;
    readonly Button askButton, presentButton;

    // Stage
    readonly RectTransform stage, stageContent;
    readonly Image stageBg, spotlight, flash;
    readonly RawImage stageImage;
    readonly TextMeshProUGUI stageName, stageRole, stageEmotion, stageStress, stageInitials, crackedText, dropHint;
    readonly Image stageEmotionBg;
    readonly HeartRateGraph heart;
    readonly CanvasGroup crackedGroup;

    SuspectAgent current;
    EvidencePicker picker;

    UiTheme T => UiKit.Theme;
    LeakDeskGame Game => ui.Game;
    public SuspectAgent Current => current;

    public InterrogationPanel(DeskUI ui, Transform parent)
    {
        this.ui = ui;
        var rt = UiKit.Rect("InterrogationPanel", parent).Stretch();
        root = rt.gameObject;
        rt.HLayout(16);
        rt.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;

        // ---------- suspect list ----------
        var list = UiKit.Panel(rt, T.panel, T.panelSprite, "SuspectList");
        list.Size(prefW: 280, flexH: 1);
        list.VLayout(12, 16);
        UiKit.Heading(list.transform, "Suspects", 28);
        UiKit.Text(list.transform, "Everyone who had the deck.", T.smallSize, T.muted);
        foreach (var agent in Game.Suspects) AddSuspectCard(list.transform, agent);
        UiKit.Spacer(list.transform, 0, 1);
        var tip = UiKit.Card(list.transform, new Color(1, 1, 1, 0.06f), 14, 6, "Tip");
        var tipRow = UiKit.Rect("Row", tip.transform);
        tipRow.HLayout(10);
        UiKit.Icon(tipRow, "bulb", 30, T.warning);
        UiKit.Text(tipRow, "Drag evidence from the tray onto the suspect to confront them.", T.smallSize - 1, T.muted).Size(flexW: 1);

        // ---------- room: stage + conversation ----------
        var room = UiKit.Panel(rt, T.panel, T.panelSprite, "Room");
        room.Size(flexW: 1, flexH: 1);
        room.HLayout(16, 16);
        room.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;

        // Stage
        stageBg = UiKit.Panel(room.transform, T.panelAlt, T.panelSprite, "Stage");
        stageBg.Size(prefW: 420, flexH: 1);
        stage = (RectTransform)stageBg.transform;
        stage.gameObject.AddComponent<RectMask2D>();
        // Everything visual lives in a stretched child so it can be shaken without fighting the layout system.
        stageContent = UiKit.Rect("Content", stage).Stretch();

        UiKit.GradientOverlay(stageContent, new Color(1f, 1f, 1f, 0.16f));
        var floor = UiKit.GradientOverlay(stageContent, new Color(0.05f, 0.03f, 0.12f, 0.55f), flip: true);
        ((RectTransform)floor.transform).anchorMax = new Vector2(1, 0.45f);
        spotlight = UiKit.Panel(stageContent, new Color(1, 1, 1, 0.18f), UiKit.AvatarSprite, "Spotlight");
        var sp = (RectTransform)spotlight.transform;
        sp.anchorMin = sp.anchorMax = new Vector2(0.5f, 0.42f);
        sp.sizeDelta = new Vector2(520, 520);

        stageInitials = UiKit.Text(stageContent, "", 120, new Color(1, 1, 1, 0.5f), TextAlignmentOptions.Center, FontStyles.Bold, "Initials");
        ((RectTransform)stageInitials.transform).Stretch();

        stageImage = UiKit.Rect("Character", stageContent).gameObject.AddComponent<RawImage>();
        var ci = (RectTransform)stageImage.transform;
        ci.anchorMin = ci.anchorMax = new Vector2(0.5f, 0f);
        ci.pivot = new Vector2(0.5f, 0f);
        ci.sizeDelta = new Vector2(560, 896);
        ci.anchoredPosition = new Vector2(0, -40);
        stageImage.raycastTarget = false;
        stageImage.enabled = false;

        // Nameplate (top)
        var plate = UiKit.Rect("Nameplate", stageContent).Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, -150), new Vector2(-18, -16));
        plate.VLayout(4);
        stageName = UiKit.Heading(plate, "", 38);
        stageName.textWrappingMode = TextWrappingModes.NoWrap;
        stageRole = UiKit.Text(plate, "", T.smallSize, new Color(1, 1, 1, 0.85f));
        var chipRow = UiKit.Rect("Chips", plate);
        chipRow.HLayout(8);
        stageEmotionBg = UiKit.Chip(chipRow, T.accent, 150, 36, "Emotion");
        stageEmotion = UiKit.Text(stageEmotionBg.transform, "CALM", T.smallSize, T.cardText, TextAlignmentOptions.Center, FontStyles.Bold);
        UiKit.Spacer(chipRow, 1);

        // Heart monitor (bottom)
        var monitor = UiKit.Panel(stageContent, new Color(0.05f, 0.04f, 0.12f, 0.82f), T.panelSprite, "Monitor");
        ((RectTransform)monitor.transform).Anchor(new Vector2(0, 0), new Vector2(1, 0), new Vector2(14, 14), new Vector2(-14, 120));
        var monRow = UiKit.Rect("Row", monitor.transform).Stretch(16, 10, 16, 10);
        monRow.HLayout(12);
        var monCol = UiKit.Rect("Col", monRow);
        monCol.VLayout(0);
        monCol.Size(prefW: 110);
        UiKit.Text(monCol, "STRESS", 14, T.muted, style: FontStyles.Bold);
        stageStress = UiKit.Text(monCol, "0", 44, T.text, style: FontStyles.Bold);
        var graphRt = UiKit.Rect("Heart", monRow);
        graphRt.Size(flexW: 1, flexH: 1);
        heart = graphRt.gameObject.AddComponent<HeartRateGraph>();
        heart.raycastTarget = false;

        // Drop highlight + cracked stamp + flash
        var drop = UiKit.Panel(stageContent, new Color(1f, 0.85f, 0.3f, 0.28f), T.panelSprite, "DropHighlight");
        ((RectTransform)drop.transform).Stretch(6, 6, 6, 6);
        dropHint = UiKit.Text(drop.transform, "DROP TO CONFRONT", 30, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        ((RectTransform)dropHint.transform).Stretch();
        drop.raycastTarget = false;
        drop.gameObject.SetActive(false);

        flash = UiKit.Panel(stageContent, new Color(1, 1, 1, 0), name: "Flash");
        ((RectTransform)flash.transform).Stretch();
        flash.raycastTarget = false;

        var cracked = UiKit.Rect("Cracked", stageContent).Stretch();
        crackedGroup = cracked.gameObject.AddComponent<CanvasGroup>();
        crackedGroup.alpha = 0;
        crackedGroup.blocksRaycasts = false;
        var crackBg = UiKit.Panel(cracked, T.danger, T.buttonSprite, "Stamp");
        var cb = (RectTransform)crackBg.transform;
        cb.anchorMin = cb.anchorMax = new Vector2(0.5f, 0.55f);
        cb.sizeDelta = new Vector2(380, 110);
        crackedText = UiKit.Text(crackBg.transform, "CRACKED!", 54, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        ((RectTransform)crackedText.transform).Stretch();
        if (T.headingFont != null) crackedText.font = T.headingFont;

        var zone = stage.gameObject.AddComponent<PresentDropZone>();
        zone.Highlight = drop;
        zone.ShakeTarget = stageContent;
        zone.CanAccept = () => current != null && !current.Busy && CanAct;
        zone.OnPresent = item => Present(item, slammed: true);

        // Conversation column
        var col = UiKit.Rect("Conversation", room.transform);
        col.VLayout(12);
        col.Size(flexW: 1, flexH: 1);
        var chatBg = UiKit.Panel(col, new Color(0, 0, 0, 0.22f), T.panelSprite, "ChatBg");
        chatBg.Size(flexH: 1, flexW: 1);
        chat = ChatView.Create(chatBg.transform);
        ((RectTransform)chat.transform).Stretch();

        var inputRow = UiKit.Rect("InputRow", col);
        inputRow.HLayout(10);
        input = UiKit.Input(inputRow, "Ask a question... (Enter to send)");
        input.onSubmit.AddListener(_ => Ask());
        askButton = UiKit.Button(inputRow, "Ask", Ask);
        askButton.Size(prefW: 96, prefH: 56);
        presentButton = UiKit.Button(inputRow, "", OpenPicker, T.warning, T.cardText);
        presentButton.Size(prefW: 64, prefH: 56);
        var pi = UiKit.Icon(presentButton.transform, "folder", 30, T.cardText);
        ((RectTransform)pi.transform).anchorMin = ((RectTransform)pi.transform).anchorMax = new Vector2(0.5f, 0.55f);
        ((RectTransform)pi.transform).sizeDelta = new Vector2(30, 30);

        Game.Evidence.Added += _ => UpdateButtons();
        Game.PhaseChanged += UpdateButtons;
        Game.WaveUnlocked += _ => { foreach (var a in Game.Suspects) UpdateCard(a); };

        var portraits = SuspectPortraits.Instance;
        if (portraits != null)
        {
            if (portraits.IsReady) OnPortraitsReady();
            else portraits.Ready += OnPortraitsReady;
        }

        Select(Game.Suspects[0]);
    }

    /// <summary>Selects a suspect by id (used by the Desks app's "Interrogate X" button).</summary>
    public void Open(string suspectId)
    {
        var agent = Game.Agent(suspectId);
        if (agent != null) Select(agent);
    }

    public void SetVisible(bool v)
    {
        root.SetActive(v);
        if (v && current != null)
        {
            Rebuild();
            input.ActivateInputField();
        }
    }

    // ---------- suspect list ----------

    void AddSuspectCard(Transform parent, SuspectAgent agent)
    {
        var card = UiKit.Panel(parent, T.panelAlt, T.cardSprite, "Suspect: " + agent.Data.Id);
        card.HLayout(12, 12);
        card.gameObject.AddComponent<Button>().onClick.AddListener(() => { Sfx.Play("whoosh"); Select(agent); });
        card.gameObject.AddComponent<ButtonPop>();

        var av = UiKit.Panel(card.transform, agent.Data.Color, UiKit.AvatarSprite, "Avatar");
        av.Size(prefW: 68, prefH: 68, minW: 68);
        av.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        var ini = UiKit.Text(av.transform, Initials(agent.Data.Name), 26, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        ((RectTransform)ini.transform).Stretch();
        var head = UiKit.Rect("Headshot", av.transform).Stretch().gameObject.AddComponent<RawImage>();
        head.raycastTarget = false;
        head.enabled = false;

        var col = UiKit.Rect("Col", card.transform);
        col.VLayout(4);
        col.Size(flexW: 1);
        var name = UiKit.Text(col, $"<b>{agent.Data.Name}</b>", T.bodySize - 2, T.text);
        name.textWrappingMode = TextWrappingModes.NoWrap;
        name.overflowMode = TextOverflowModes.Ellipsis;
        var role = UiKit.Text(col, agent.Data.Role, T.smallSize - 3, T.muted);
        role.textWrappingMode = TextWrappingModes.NoWrap;
        role.overflowMode = TextOverflowModes.Ellipsis;
        var row = UiKit.Rect("Row", col);
        row.HLayout(8);
        var emoBg = UiKit.Chip(row, SourceStyle.EmotionColor("calm"), 92, 24, "Emotion");
        var emo = UiKit.Text(emoBg.transform, "CALM", 13, T.cardText, TextAlignmentOptions.Center, FontStyles.Bold);
        var fill = UiKit.Bar(row, T.warning, 10);

        cards[agent] = new SuspectCard
        {
            Card = card, StressFill = fill, Emotion = emo, EmotionBg = emoBg, Headshot = head,
            Group = card.gameObject.AddComponent<CanvasGroup>(),
        };
        agent.Changed += OnAgentChanged;
        UpdateCard(agent);
    }

    void OnPortraitsReady()
    {
        var p = SuspectPortraits.Instance;
        foreach (var kv in cards)
        {
            var tex = p.Headshot(kv.Key.Data.Id);
            kv.Value.Headshot.texture = tex;
            kv.Value.Headshot.enabled = tex != null;
        }
        stageImage.texture = p.StageTexture;
        stageImage.enabled = p.StageTexture != null;
        if (current != null) p.Show(current.Data);
        stageInitials.enabled = !stageImage.enabled;
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
            kv.Value.Card.color = kv.Key == agent ? Color.Lerp(T.panelAlt, agent.Data.Color, 0.45f) : T.panelAlt;

        var c = agent.Data.Color;
        stageBg.color = Color.Lerp(new Color(0.12f, 0.10f, 0.26f), c, 0.55f);
        spotlight.color = new Color(1, 1, 1, 0.22f);
        stageName.text = agent.Data.Name;
        stageRole.text = $"{agent.Data.Role}  ·  {agent.Data.Age}";
        stageInitials.text = Initials(agent.Data.Name);
        UpdateHeader();
        Rebuild();
        ui.Tray?.Refresh();
        SuspectPortraits.Instance?.Show(agent.Data);
        UiFx.Punch(stageContent, 0.03f, 0.25f);
        if (root.activeInHierarchy) input.ActivateInputField();
    }

    // ---------- conversation ----------

    void Rebuild()
    {
        chat.Clear();
        foreach (var e in current.Log) AddEntry(e, animate: false);
        if (current.Busy) chat.ShowTyping(current.Data.FirstName);
        UpdateButtons();
    }

    void AddEntry(ChatEntry e, bool animate)
    {
        switch (e.Type)
        {
            case ChatEntry.Kind.Player:
                chat.AddBubble("You", e.Text, ChatView.Side.Right, T.bubblePlayer, T.accentText);
                break;
            case ChatEntry.Kind.Suspect:
                var label = $"{current.Data.FirstName}  <color=#{UiKit.Hex(Color.Lerp(SourceStyle.EmotionColor(e.Emotion), Color.black, 0.25f))}>· {SourceStyle.EmotionLabel(e.Emotion)}</color>";
                var head = SuspectPortraits.Instance != null ? SuspectPortraits.Instance.Headshot(current.Data.Id) : null;
                var bubble = chat.AddBubble(label, e.Text, ChatView.Side.Left, T.bubbleSuspect, null, head);
                if (animate)
                {
                    var texts = bubble.GetComponentsInChildren<TextMeshProUGUI>();
                    UiFx.Typewrite(texts[^1], 60f, current.Data.VoicePitch);
                }
                break;
            case ChatEntry.Kind.Evidence:
                var b = chat.AddBubble($"EVIDENCE  ·  {SourceStyle.Label(e.EvidenceSource)}", e.Text, ChatView.Side.Right,
                    new Color(1f, 0.94f, 0.74f));
                var stripe = UiKit.Panel(b.transform, SourceStyle.Color(e.EvidenceSource), T.pillSprite, "Stripe").Pill(6);
                stripe.Size(prefH: 6);
                stripe.transform.SetAsFirstSibling();
                break;
            case ChatEntry.Kind.System:
                chat.AddBubble(null, $"<i>{e.Text}</i>", ChatView.Side.Center, new Color(1, 1, 1, 0.10f), T.muted);
                break;
        }
    }

    void Ask()
    {
        string q = input.text.Trim();
        if (q.Length == 0 || current == null || current.Busy || !CanAct) return;
        input.text = "";
        Sfx.Play("click");
        var agent = current;
        bool wasBroken = agent.Broken;
        agent.Ask(q, _ => OnReply(agent, wasBroken));
        AfterSend(agent);
    }

    public void Present(EvidenceItem item) => Present(item, slammed: false);

    void Present(EvidenceItem item, bool slammed)
    {
        if (current == null || current.Busy || !CanAct) return;
        ui.ShowTab(DeskUI.Tab.Interrogate);
        if (!slammed)
        {
            Sfx.Play("slam");
            UiFx.Shake(stageContent, 12f, 0.3f);
        }
        SuspectPortraits.Instance?.Recoil();
        StartFlash(0.35f);
        var agent = current;
        bool wasBroken = agent.Broken;
        agent.Present(item, _ => OnReply(agent, wasBroken));
        AfterSend(agent);
    }

    bool CanAct => Game.Phase == GamePhase.Investigating;

    void AfterSend(SuspectAgent agent)
    {
        AddEntry(agent.Log[^1], animate: false);
        chat.ShowTyping(agent.Data.FirstName);
        UpdateButtons();
        input.ActivateInputField();
    }

    void OnReply(SuspectAgent agent, bool wasBroken)
    {
        if (agent != current) return; // the reply is in its Log; shown when the player switches back
        chat.HideTyping();
        AddEntry(agent.Log[^1], animate: true);
        UpdateButtons();
        if (!wasBroken && agent.Broken) ui.StartCoroutine(Cracked());
        if (root.activeInHierarchy) input.ActivateInputField();
    }

    IEnumerator Cracked()
    {
        Sfx.Play("crack");
        StartFlash(0.8f);
        UiFx.Shake(stageContent, 22f, 0.5f);
        ui.Toast($"{current.Data.FirstName} cracked! Their story is falling apart.", T.danger);
        var stamp = crackedText.transform.parent;
        crackedGroup.alpha = 1;
        for (float t = 0; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            float k = 1 - Mathf.Pow(1 - t / 0.35f, 3);
            stamp.localScale = Vector3.one * Mathf.Lerp(2.6f, 1f, k);
            stamp.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-25f, -8f, k));
            yield return null;
        }
        Sfx.Play("stamp");
        UiFx.Punch(stamp, 0.12f, 0.3f);
        yield return new WaitForSecondsRealtime(1.6f);
        for (float t = 0; t < 0.4f; t += Time.unscaledDeltaTime)
        {
            crackedGroup.alpha = 1 - t / 0.4f;
            yield return null;
        }
        crackedGroup.alpha = 0;
    }

    /// <summary>"+12" rising from the stress number, red when stress goes up, green when it drops.</summary>
    IEnumerator FloatDelta(int delta)
    {
        var t = UiKit.Text(stageContent, (delta > 0 ? "+" : "") + delta, 40, delta > 0 ? T.danger : T.accent, TextAlignmentOptions.Center, FontStyles.Bold, "Delta");
        if (T.headingFont != null) t.font = T.headingFont;
        var rt = (RectTransform)t.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
        rt.sizeDelta = new Vector2(160, 60);
        Vector2 start = new(110, 150);
        UiFx.Punch(stageStress.transform, 0.25f, 0.35f);
        for (float time = 0; time < 1.1f && t != null; time += Time.unscaledDeltaTime)
        {
            float k = time / 1.1f;
            rt.anchoredPosition = start + new Vector2(Mathf.Sin(k * 6f) * 6f, k * 120f);
            t.alpha = 1f - k * k;
            rt.localScale = Vector3.one * (1f + Mathf.Sin(Mathf.Min(1f, k * 4f) * Mathf.PI) * 0.3f);
            yield return null;
        }
        if (t != null) Object.Destroy(t.gameObject);
    }

    void StartFlash(float strength) => ui.StartCoroutine(Flash(strength));

    IEnumerator Flash(float strength)
    {
        for (float t = 0; t < 0.3f; t += Time.unscaledDeltaTime)
        {
            flash.color = new Color(1, 1, 1, strength * (1 - t / 0.3f));
            yield return null;
        }
        flash.color = new Color(1, 1, 1, 0);
    }

    // ---------- state ----------

    readonly Dictionary<SuspectAgent, int> lastStress = new();

    void OnAgentChanged(SuspectAgent agent)
    {
        lastStress.TryGetValue(agent, out int before);
        lastStress[agent] = agent.Stress;
        int delta = agent.Stress - before;
        if (agent == current && delta != 0) ui.StartCoroutine(FloatDelta(delta));
        UpdateCard(agent);
        if (agent == current) UpdateHeader();
        SuspectPortraits.Instance?.React(agent.Data, agent.Broken && agent.Stress >= 60 ? "broken" : agent.Emotion, agent.Stress);
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
        c.Emotion.text = !agent.Met ? "NOT MET" : agent.Broken ? "CRACKED" : SourceStyle.EmotionLabel(agent.Emotion);
        c.EmotionBg.color = !agent.Met ? new Color(0.6f, 0.62f, 0.7f) : agent.Broken ? T.danger : SourceStyle.EmotionColor(agent.Emotion);
    }

    void UpdateHeader()
    {
        stageEmotion.text = current.Broken ? "CRACKED" : SourceStyle.EmotionLabel(current.Emotion);
        stageEmotionBg.color = current.Broken ? T.danger : SourceStyle.EmotionColor(current.Emotion);
        stageStress.text = current.Stress.ToString();
        stageStress.color = StressColor(current.Stress);
        heart.stress = current.Stress;
        UiFx.Punch(stageEmotionBg.transform, 0.1f, 0.25f);
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
        picker.Open($"Confront {current.Data.FirstName} with...", Present);
    }

    public static string Initials(string name)
    {
        var parts = name.Split(' ');
        return parts.Length >= 2 ? $"{parts[0][0]}{parts[^1][0]}" : name.Substring(0, Mathf.Min(2, name.Length));
    }
}
