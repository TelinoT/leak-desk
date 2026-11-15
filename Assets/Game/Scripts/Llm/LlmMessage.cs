using System;

/// <summary>One chat turn in the Anthropic messages format.</summary>
[Serializable]
public class LlmMessage
{
    public string role;
    public string content;

    public LlmMessage(string role, string content)
    {
        this.role = role;
        this.content = content;
    }

    public static LlmMessage User(string text) => new("user", text);
    public static LlmMessage Assistant(string text) => new("assistant", text);
}
