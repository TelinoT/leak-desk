// Cloudflare Worker: keeps the Anthropic API key out of the Unity build.
// Secrets: ANTHROPIC_API_KEY, GAME_TOKEN, FISH_API_KEY (wrangler secret put ...)
// Routes: POST /        -> Claude chat     {system, messages, max_tokens, model} -> {text}
//         POST /tts     -> Fish Audio TTS  {text, voice}                         -> audio/mpeg
//         POST /voices  -> Fish voice library search {title, tags, language}     -> JSON

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

    const path = new URL(request.url).pathname;
    if (path === "/tts") return tts(body, env);
    if (path === "/voices") return voices(body, env);

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

// Text-to-speech through Fish Audio. Short lines only (keeps the credits and latency down).
async function tts(body, env) {
  if (!env.FISH_API_KEY) return json({ error: "tts not configured" }, 503);
  const text = String(body.text || "").slice(0, 500).trim();
  const voice = String(body.voice || "");
  if (!text || !/^[a-f0-9]{32}$/.test(voice)) return json({ error: "bad tts request" }, 400);
  const upstream = await fetch("https://api.fish.audio/v1/tts", {
    method: "POST",
    headers: { "Authorization": `Bearer ${env.FISH_API_KEY}`, "content-type": "application/json", "model": "s2.1-pro" },
    body: JSON.stringify({ text, reference_id: voice, format: "mp3", mp3_bitrate: 64, latency: "balanced",
                           prosody: { speed: Math.min(1.4, Math.max(0.8, Number(body.speed) || 1.08)) } }),
  });
  if (!upstream.ok) return json({ error: `tts upstream ${upstream.status}` }, 502);
  return new Response(upstream.body, { headers: { "content-type": "audio/mpeg", ...CORS } });
}

// Voice library search (used once to pick voices; returns only what we need).
async function voices(body, env) {
  if (!env.FISH_API_KEY) return json({ error: "tts not configured" }, 503);
  const q = new URLSearchParams({ page_size: "20", page_number: "1", sort_by: "score" });
  if (body.title) q.set("title", body.title);
  if (body.tag) q.set("tag", body.tag);
  if (body.language) q.set("language", body.language);
  const r = await fetch(`https://api.fish.audio/model?${q}`, { headers: { "Authorization": `Bearer ${env.FISH_API_KEY}` } });
  const data = await r.json();
  const items = (data.items || []).map(m => ({ id: m._id, title: m.title, tags: m.tags, languages: m.languages,
    description: (m.description || "").slice(0, 160), likes: m.like_count, uses: m.task_count, author: m.author?.nickname }));
  return json({ total: data.total, items });
}

function json(obj, status = 200) {
  return new Response(JSON.stringify(obj), {
    status,
    headers: { "content-type": "application/json", ...CORS },
  });
}
