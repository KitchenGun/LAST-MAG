const MAX_BODY_BYTES = 1024;
const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const NICKNAME_PATTERN = /^[A-Za-z0-9]{2,12}$/;
const PLAYER_CLASSES = new Set(["GRENADIER", "ENGINEER", "SNIPER"]);

export default {
  async fetch(request, env) {
    const origin = request.headers.get("Origin");
    const cors = getCorsHeaders(origin, env.ALLOWED_ORIGINS);

    if (origin && !cors) {
      return json({ error: "origin_not_allowed" }, 403);
    }

    if (request.method === "OPTIONS") {
      return new Response(null, {
        status: 204,
        headers: cors ?? {}
      });
    }

    const url = new URL(request.url);
    if (url.pathname === "/v1/leaderboard" && request.method === "GET") {
      return getLeaderboard(env, cors, url.searchParams.get("currentRunId"));
    }

    if (url.pathname === "/v1/scores" && request.method === "POST") {
      return submitScore(request, env, cors);
    }

    return json({ error: "not_found" }, 404, cors);
  }
};

async function submitScore(request, env, cors) {
  if (!request.headers.get("Content-Type")?.toLowerCase().startsWith("application/json")) {
    return json({ error: "content_type_must_be_json" }, 415, cors);
  }

  const clientKey = request.headers.get("CF-Connecting-IP") ?? "unknown";
  if (env.SUBMIT_RATE_LIMITER) {
    const rateLimit = await env.SUBMIT_RATE_LIMITER.limit({ key: clientKey });
    if (!rateLimit.success) {
      return json({ error: "rate_limited" }, 429, cors);
    }
  }

  const bodyText = await request.text();
  if (new TextEncoder().encode(bodyText).byteLength > MAX_BODY_BYTES) {
    return json({ error: "payload_too_large" }, 413, cors);
  }

  let body;
  try {
    body = JSON.parse(bodyText);
  } catch {
    return json({ error: "invalid_json" }, 400, cors);
  }

  const validationError = validateSubmission(body);
  if (validationError) {
    return json({ error: validationError }, 400, cors);
  }

  const boardVersion = env.BOARD_VERSION;
  const playerClass = body.playerClass ?? "UNKNOWN";
  const existing = await findScore(env.DB, boardVersion, body.runId);
  if (existing) {
    if (existing.nickname !== body.nickname || existing.player_class !== playerClass || existing.score !== body.score) {
      return json({ error: "run_id_conflict" }, 409, cors);
    }
    return submissionResponse(env.DB, boardVersion, existing, true, 200, cors);
  }

  // ponytail: v1 trusts client score; recompute after score rules freeze.
  try {
    await env.DB.prepare(
      `INSERT INTO scores (board_version, run_id, nickname, player_class, score)
       VALUES (?1, ?2, ?3, ?4, ?5)`
    ).bind(boardVersion, body.runId, body.nickname, playerClass, body.score).run();
  } catch (error) {
    const raced = await findScore(env.DB, boardVersion, body.runId);
    if (!raced) {
      throw error;
    }
    if (raced.nickname !== body.nickname || raced.player_class !== playerClass || raced.score !== body.score) {
      return json({ error: "run_id_conflict" }, 409, cors);
    }
    return submissionResponse(env.DB, boardVersion, raced, true, 200, cors);
  }

  const inserted = await findScore(env.DB, boardVersion, body.runId);
  return submissionResponse(env.DB, boardVersion, inserted, false, 201, cors);
}

async function submissionResponse(db, boardVersion, scoreRow, duplicate, status, cors) {
  const [totalResult, higherResult] = await Promise.all([
    db.prepare("SELECT COUNT(*) AS count FROM scores WHERE board_version = ?1")
      .bind(boardVersion).first(),
    db.prepare("SELECT COUNT(*) AS count FROM scores WHERE board_version = ?1 AND score > ?2")
      .bind(boardVersion, scoreRow.score).first()
  ]);

  const total = Number(totalResult?.count ?? 0);
  const higher = Number(higherResult?.count ?? 0);
  const percentile = Math.min(100, Math.max(1, Math.ceil((higher * 100) / Math.max(1, total))));

  return json({
    accepted: true,
    duplicate,
    runId: scoreRow.run_id,
    percentile
  }, status, cors);
}

async function getLeaderboard(env, cors, currentRunId) {
  if (currentRunId !== null && !UUID_PATTERN.test(currentRunId)) {
    return json({ error: "invalid_current_run_id" }, 400, cors);
  }

  const top10 = await readTop10(env.DB, env.BOARD_VERSION, currentRunId);
  return json({ boardVersion: env.BOARD_VERSION, top10 }, 200, cors);
}

async function findScore(db, boardVersion, runId) {
  return db.prepare(
    `SELECT run_id, nickname, player_class, score
     FROM scores
     WHERE board_version = ?1 AND run_id = ?2`
  ).bind(boardVersion, runId).first();
}

async function readTop10(db, boardVersion, currentRunId) {
  const result = await db.prepare(
    `SELECT run_id, nickname, player_class, score
     FROM scores
     WHERE board_version = ?1
     ORDER BY score DESC, submitted_at ASC, id ASC
     LIMIT 10`
  ).bind(boardVersion).all();

  return result.results.map((entry, index) => ({
    rank: index + 1,
    nickname: entry.nickname,
    playerClass: entry.player_class,
    score: entry.score,
    isCurrent: currentRunId !== null && entry.run_id === currentRunId
  }));
}

export function validateSubmission(body) {
  if (!body || typeof body !== "object" || Array.isArray(body)) {
    return "invalid_payload";
  }
  if (typeof body.runId !== "string" || !UUID_PATTERN.test(body.runId)) {
    return "invalid_run_id";
  }
  if (typeof body.nickname !== "string" || !NICKNAME_PATTERN.test(body.nickname)) {
    return "invalid_nickname";
  }
  if (body.playerClass !== undefined
      && (typeof body.playerClass !== "string" || !PLAYER_CLASSES.has(body.playerClass))) {
    return "invalid_player_class";
  }
  if (!Number.isSafeInteger(body.score) || body.score < 0 || body.score > 2147483647) {
    return "invalid_score";
  }
  return null;
}

export function getCorsHeaders(origin, configuredOrigins = "") {
  if (!origin) {
    return null;
  }

  const allowed = configuredOrigins.split(",").map(value => value.trim()).filter(Boolean);
  if (!allowed.includes(origin)) {
    return null;
  }

  return {
    "Access-Control-Allow-Origin": origin,
    "Access-Control-Allow-Headers": "Content-Type",
    "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
    "Access-Control-Max-Age": "86400",
    "Vary": "Origin"
  };
}

function json(payload, status, extraHeaders = null) {
  return new Response(JSON.stringify(payload), {
    status,
    headers: {
      "Content-Type": "application/json; charset=utf-8",
      "Cache-Control": "no-store",
      ...(extraHeaders ?? {})
    }
  });
}
