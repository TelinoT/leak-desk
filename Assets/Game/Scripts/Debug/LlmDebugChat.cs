using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Step 1 test bench: one hardcoded suspect, one input field. Verifies the Unity -> Worker -> Claude round trip
/// (in the Editor and in WebGL). Lives in Assets/Game/Scenes/LlmDebug.unity.
/// </summary>
public class LlmDebugChat : MonoBehaviour
{
    const string Persona =
        "You are Theo Brandt, a senior silicon engineer at Envydia, a fictional chip company. " +
        "Last night the slides for Thursday's keynote leaked to a tech blog at 02:14. " +
        "You were secretly in the lab at 2am running a crypto miner on test hardware, and you will lie about it " +
        "(you claim you went home at 7pm) unless someone shows you hard proof. You are defensive, sarcastic and tired. " +
        "Stay in character. Answer in 1-3 short sentences. Never mention that you are an AI.";

    readonly List<LlmMessage> history = new();
    ChatView chat;
    TMP_InputField input;
    Button send;

    void Start()
    {
        var canvas = UiKit.CreateCanvas("DebugCanvas");
        var theme = UiKit.Theme;

        var bg = UiKit.Panel(canvas.transform, new Color(0.10f, 0.11f, 0.18f, 1f), name: "Background");
        ((RectTransform)bg.transform).Stretch();

        var frame = UiKit.Panel(canvas.transform, theme.panel, theme.panelSprite, "Frame");
        ((RectTransform)frame.transform).Stretch(360, 60, 360, 60);
        frame.VLayout(14, 24, controlHeight: true);

        UiKit.Heading(frame.transform, "LLM Debug - Interrogating Theo Brandt");
        UiKit.Text(frame.transform, "Round trip: Unity -> Cloudflare Worker -> Claude. Ask him where he was at 02:14.",
            theme.smallSize, theme.muted);

        var chatHolder = UiKit.Panel(frame.transform, new Color(0, 0, 0, 0.25f), theme.panelSprite, "ChatHolder");
        chatHolder.Size(flexH: 1, flexW: 1);
        chat = ChatView.Create(chatHolder.transform);
        ((RectTransform)chat.transform).Stretch();

        var row = UiKit.Rect("InputRow", frame.transform);
        row.HLayout(10);
        input = UiKit.Input(row, "Type a question and press Enter...");
        input.onSubmit.AddListener(_ => Send());
        send = UiKit.Button(row, "Send", Send);
        send.Size(prefW: 140, prefH: 56);

        chat.AddBubble("System", "Theo Brandt has entered the interview room.", ChatView.Side.Center, theme.bubbleSystem);
        input.ActivateInputField();
    }

    void Send()
    {
        string text = input.text.Trim();
        if (text.Length == 0 || !send.interactable) return;

        input.text = "";
        chat.AddBubble("You", text, ChatView.Side.Right, UiKit.Theme.bubblePlayer);
        history.Add(LlmMessage.User(text));
        send.interactable = false;
        chat.ShowTyping("Theo");
        float started = Time.realtimeSinceStartup;

        LlmClient.Instance.Send(Persona, history,
            reply =>
            {
                history.Add(LlmMessage.Assistant(reply));
                chat.HideTyping();
                chat.AddBubble($"Theo  <size=70%>({Time.realtimeSinceStartup - started:0.0}s)</size>", reply, ChatView.Side.Left,
                    UiKit.Theme.bubbleSuspect);
                send.interactable = true;
                input.ActivateInputField();
            },
            error =>
            {
                history.RemoveAt(history.Count - 1);
                chat.HideTyping();
                chat.AddBubble("Error", error, ChatView.Side.Center, UiKit.Theme.danger, Color.white);
                send.interactable = true;
            });
    }
}
