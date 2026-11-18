# Leak Desk

*An AI interrogation game for the **Operation Leather Jacket** hackathon (#OperationLeatherJacket).*

Someone leaked the keynote. The board wants a name by 09:00. You have 12 minutes, the overnight logs, four suspects who talk back, and a company AI that is *very* eager to help.

## The AI is the game
- **Four suspects, each a live Claude character.** You type any question; they answer in character (and out loud, via Fish Audio), lie about their secrets, and crack when you present the right evidence. What they admit becomes evidence.
- **Jensenbot, the AI assistant, has holes in its data.** The game strips certain lines from the logs it reads, so its summaries are confident and incomplete. You catch it by comparing them with the raw logs.
- **An AI board judges your accusation** and your written motive. Code checks the facts; the model writes the verdict.

Without the models there's nobody to interrogate and nobody to judge you.

## What it is
You're the internal security analyst at **Envydia**, a (fictional) chip company. Read raw logs, search suspects' desks, flag suspicious lines, and interrogate four LLM-driven suspects in free text. Each has a public story, a secret and lies that crack when you present the right evidence. Then accuse one of them in front of an AI board that knows the truth.

The twist: **Jensenbot**, the desk assistant that summarizes logs for you, has its own reasons to keep certain entries quiet.

All characters and companies are fictional.

## Tech
- Unity 6 (6000.0.74f1), URP, uGUI + TextMeshPro, built for WebGL
- Claude Haiku 5.5 (suspects, Jensenbot) and Claude Sonnet 5.5 (judge) via a Cloudflare Worker proxy
- Fish Audio (s2.1-pro) for the voices, through the same proxy (Fish Audio's own designed voices, no clones)
- The whole case lives in one data file: [`Assets/Game/Resources/Cases/leak_01/truth.json`](Assets/Game/Resources/Cases/leak_01/truth.json)
- Prompts: [`Assets/Game/Resources/Prompts/`](Assets/Game/Resources/Prompts/)

### Project layout
```
Assets/Game/
  Scripts/Llm/            LlmConfig, LlmClient (UnityWebRequest, WebGL-safe), JSON helpers
  Scripts/Case/           truth.json data model, loader, evidence inventory
  Scripts/Interrogation/  SuspectAgent (prompt, history, stress, JSON replies)
  Scripts/Desk/           JensenbotAgent (summaries from a tampered feed)
  Scripts/Verdict/        Judge (code pre-check + LLM board)
  Scripts/UI/             runtime-built UI (UiKit, panels, overlays), 3D suspect stage (SuspectRig),
                          desk search, procedural sound (Sfx), Jensenbot face
  Fonts/                  Rubik (SIL OFL)
  Scenes/LeakDesk.unity   the game · LlmDebug.unity: LLM round-trip test bench
Tools/worker/             Cloudflare Worker proxy (keeps the API key out of the build)
```

## Running it yourself
1. **Proxy:** in `Tools/worker`, run `npx wrangler deploy`, then `npx wrangler secret put ANTHROPIC_API_KEY`, `npx wrangler secret put GAME_TOKEN` and (optional, for voices) `npx wrangler secret put FISH_API_KEY`.
2. **Unity:** create `Assets/Game/Resources/LlmConfig.asset` (*Assets > Create > Leak Desk > LLM Config*) with the Worker URL and game token. It's gitignored.
3. Open `Assets/Game/Scenes/LeakDesk.unity` and press Play, or build with **Leak Desk > Build WebGL**.

### Third-party assets (not in this repo)
The visuals use paid packs that are licensed for use in builds but can't be redistributed in a public repo, so they are gitignored:
- **Synty POLYGON Office** (office backdrop and suspect characters): `Assets/Synty/`
- **GUI Pro Kit – Simple Casual** by Layer Lab (UI sprites): `Assets/ThirdParty/GuiPro/`

Without them the game still runs: the UI falls back to flat shapes and colored initials, and the scene shows missing references for the office.

## Credits
- Font: Rubik by the Rubik Project Authors (SIL Open Font License)
- All sound effects and music are synthesized in code
- Voices: Fish Audio (designed voices from the Fish Audio library)

Made by TelinoT, built with Claude Code. Forked from our earlier jam game *Too Lucky*.
