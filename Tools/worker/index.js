// Cloudflare Worker: keeps the Anthropic API key out of the Unity build.
// Secrets: ANTHROPIC_API_KEY, GAME_TOKEN (wrangler secret put ...)

const MODEL = "claude-haiku-5-5"; // fast + cheap for live dialogue; swap if you want
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
        model: MODEL,
        system,
        messages,
        max_tokens: Math.min(max_tokens, MAX_TOKENS_CAP),
        // Haiku 5.5 thinks by default; for snappy in-character dialogue we turn it off
        // so the whole token budget goes to the visible reply.
        thinking: { type: "disabled" },
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
