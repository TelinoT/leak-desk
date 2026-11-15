# Leak Desk – AI stack note

**The LLM is the game.** Every suspect, the company AI "Jensenbot" and the board that judges you are live Claude calls. Without the model there's no one to interrogate and nothing to judge.

## Runtime architecture
```
Unity WebGL build ──HTTPS──> Cloudflare Worker (proxy) ──> Anthropic Messages API
   (no API key)               holds ANTHROPIC_API_KEY,         Claude Haiku 5.5 / Sonnet 5.5
                              checks a game token, caps tokens
```
- **Client:** Unity 6 (URP), `UnityWebRequest` + coroutines, so no threads and it works in WebGL. One automatic retry on network/5xx errors.
- **Proxy:** a ~60-line Cloudflare Worker (`Tools/worker`). It keeps the API key out of the build, enforces a token cap and a model allowlist, and turns off extended thinking so replies feel like a person answering (~1–1.5 s).

## Models and roles
| Role | Model | What it does |
|---|---|---|
| 4 suspects | `claude-haiku-5-5` | Each has a system prompt generated from the case file (`truth.json`): persona, speech style, public story, secret, lies, facts it knows, and which evidence breaks it. Its own chat history and a stress meter are kept per suspect. |
| Jensenbot | `claude-haiku-5-5` | Summarizes logs on request and answers questions in a cheerful corporate voice. |
| The board (judge) | `claude-sonnet-5-5` | One call per round: hidden truth + your accusation → verdict, score, witty board comment. |

## Making an LLM a reliable game mechanic
- **Structured replies.** Suspects answer only in JSON: `{"reply","emotion","stress_delta","revealed":[fact_ids]}`. Parsing is defensive: we extract the first balanced `{...}`, retry once, accept plain prose as a last resort, and otherwise use an in-character fallback line.
- **Truth stays in code.** The case file decides what is true. Breaking evidence bumps stress deterministically, revealed fact ids are checked against what that suspect actually knows, and the judge receives a **code-computed pre-check** (right suspect? winning evidence combination present?). The model can't overrule the verdict class, only flavor the score within its band and write the commentary.
- **Statements become evidence.** When a suspect reveals a fact, it lands in the evidence tray as a quotable statement. That's how interrogation feeds the accusation.
- **The Jensenbot twist without asking a model to lie.** Models resist deceiving the user they're talking to, so the deception lives in the game code instead. Jensenbot's "data feed" is tampered: the lines that would expose it are swapped for "scheduled automation job (routine)" before the model sees them. The model then summarizes faithfully, the player sees a confident summary, and the contradiction with the raw logs is the clue.
- **Prompts are data.** All prompts are text assets in `Assets/Game/Resources/Prompts/`, so they can be tuned without recompiling.

## Cost
A full 12-minute round is about 40–80 calls, roughly $0.03–0.08.

## Built with AI too
The game was built with Claude Code driving the Unity Editor through the Unity CLI: case design, C# systems, runtime-built UI, prompt tuning, play-mode testing via screenshots, and the WebGL build.
