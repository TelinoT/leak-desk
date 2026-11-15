// Cloudflare Worker: keeps the Anthropic API key out of the Unity build.
// Secrets: ANTHROPIC_API_KEY, GAME_TOKEN (wrangler secret put ...)

const DEFAULT_MODEL = "claude-haiku-5-5"; // fast + cheap for live dialogue
// The game may pick a model per call (e.g. a smarter one for the verdict), but only from this list.
const ALLOWED_MODELS = ["claude-haiku-5-5", "claude-sonnet-5-5"];
const MAX_TOKENS_CAP = 600;

const CORS = {
  "Access-Control-Allow-Origin": "*", // tighten to your itch.io domain before submitting if you like
  "Access-Control-Allow-Methods": "POST, OPTIONS",
  "Access-Control-Allow-Headers": "content-type, x-game-token",
};

export default {
  async fetch(request, env) {
    if (request.method === "OPTIONS") return new Response(null, { headers: CORS });
    if (request.method !== "POST") return json({ error: "POST only" }, 405);
    if (request.headers.get("x-game-token") !== env.GAME_TOKEN) return json({ error: "unauthorized" }, 401);

    let body;
    try { body = await request.json(); } catch { return json({ error: "bad json" }, 400); }

    const { system = "", messages = [], max_tokens = 400 } = body;
    const model = ALLOWED_MODELS.includes(body.model) ? body.model : DEFAULT_MODEL;
    if (!Array.isArray(messages) || messages.length === 0 || messages.length > 60)
      return json({ error: "bad messages" }, 400);

    const upstream = await fetch("https://api.anthropic.com/v1/messages", {
      method: "POST",
      headers: {
        "x-api-key": env.ANTHROPIC_API_KEY,
        "anthropic-version": "2023-06-01",
        "content-type": "application/json",
      },
      body: JSON.stringify({
        model,
        system,
        messages,
        max_tokens: Math.min(max_tokens, MAX_TOKENS_CAP),
        // Both models think by default; for snappy replies we turn it off so the whole token
        // budget goes to the visible text. Sonnet 5.5 rejects "disabled" and uses "between_tools".
        thinking: { type: model.includes("sonnet") ? "between_tools" : "disabled" },
      }),
    });

    const data = await upstream.json();
    if (!upstream.ok) return json({ error: data?.error?.message || "upstream error" }, 502);

    const text = (data.content || []).filter(b => b.type === "text").map(b => b.text).join("");
    return json({ text, stop_reason: data.stop_reason });
  },
};

function json(obj, status = 200) {
  return new Response(JSON.stringify(obj), {
    status,
    headers: { "content-type": "application/json", ...CORS },
  });
}
