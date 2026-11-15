# Leak Desk – Operation Leather Jacket entry

AI investigation game for the Operation Leather Jacket hackathon (Slush 2026, remote entry).
Deadline: Wed 18 Nov 2026, 21:00 CET. Scope is ruthless: one complete, polished case beats two half cases.

## Hackathon rules (hard constraints)
- AI must be essential to gameplay. The LLM runs inside the game; remove it and the game breaks.
- One full round ≤ 15 minutes (target 10–12).
- Fictional characters and companies only. No real people, real logos or cloned voices.
  The company is **Envydia**; the CEO is a fictional character in a leather jacket. No real names.
- Submission: public git repo, playable build (WebGL on itch.io), 3-min demo video, 1-page rules, short AI stack note.

## Concept
Forked from Too Lucky (Unity jam desk game). The player is Envydia's internal security analyst.
Someone is leaking, and the company AI might be in on it.

1. **Desk / evidence board** – handwritten case material: emails, Slack excerpts, badge logs, file transfers.
   Player flags suspicious lines; flagged lines become Evidence items.
2. **Interrogation** – free-text chat with 4 suspects, each an LLM persona built from the case truth file.
   Player can present Evidence into the chat; suspect must respond to it.
3. **Accusation** – pick suspect + attach up to 3 evidence items + one-sentence motive.
   An LLM "board" judges the case against the truth file and returns a verdict.
- Twist: **Jensenbot**, the desk assistant AI, summarizes logs on request. In the main case it is the
  accomplice and slants its summaries; contradictions with the raw logs are discoverable.
- Round timer: 12 minutes in-fiction ("board meeting starts at 09:00").

## Architecture
- Unity 6 URP, desktop UI (uGUI or UI Toolkit – whatever Too Lucky already uses; don't mix).
- `Assets/Game/Cases/<case_id>/truth.json` – single source of truth (schema below). Loaded via Resources or StreamingAssets (must work in WebGL).
- `LlmClient` – UnityWebRequest POST to the Cloudflare Worker proxy. Never put an API key in Unity.
  Config (proxy URL, game token) in a ScriptableObject `LlmConfig`. Must work in WebGL (async/coroutines, no threads).
- `SuspectAgent` – builds the system prompt from truth.json (public persona + private knowledge + lies + what breaks them), keeps per-suspect chat history, tracks stress 0–100.
- Suspect reply is **JSON only**: `{"reply": string, "emotion": "calm|nervous|angry|evasive", "stress_delta": int, "revealed": [fact_id]}`. Parse defensively; on failure, retry once, then show a fallback line.
- `Judge` – one call with truth.json + player accusation, returns `{"verdict": "convicted|insufficient|wrong", "score": 0-100, "board_comment": string}`.
- Keep prompts in text assets under `Assets/Game/Prompts/` so they can be tuned without recompiling.
- Show a typing indicator while waiting; LLM latency must feel like the suspect thinking.

## truth.json schema (sketch)
```json
{
  "case_id": "leak_01",
  "title": "The Keynote Leak",
  "leaker": "suspect_id",
  "accomplice": "jensenbot|null",
  "timeline": [{"time": "02:14", "event": "...", "witnessed_by": ["id"]}],
  "facts": [{"id": "f1", "text": "...", "known_by": ["id"], "proves": "leaker|alibi|red_herring"}],
  "suspects": [{
    "id": "...", "name": "...", "role": "...", "personality": "...", "speech_style": "...",
    "public_story": "...", "secret": "...", "lies": ["..."], "breaks_if_shown": ["evidence_id"]
  }],
  "evidence": [{"id": "e1", "source": "email|slack|badge|transfer", "content": "...", "relevant_to": ["f1"]}],
  "winning_evidence": [["e1", "e4"], ["e2", "e4"]]
}
```

## Working rules for Claude Code
- If a Unity Editor is open, drive it with the `unity` CLI (`unity status`, `unity command ... --caller plugin --skill unity-cli`). Never hand-edit .unity/.prefab/.asset YAML while the Editor is reachable.
- After C# edits run `unity recompile --project-path "$PWD"` and fix errors before moving on.
- One MonoBehaviour per file, file named after the class.
- Commit after each working milestone with a clear message.
- After each change, summarize briefly what changed and why.
- Prioritize: playable end-to-end loop first, then content, then polish. Ask before large refactors of Too Lucky code.
