import assert from "node:assert/strict";
import test from "node:test";
import worker, { getCorsHeaders, validateSubmission } from "./index.js";

test("accepts the smoke payload and rejects invalid trust-boundary input", () => {
  const valid = {
    runId: "11111111-1111-4111-8111-111111111111",
    nickname: "TEST",
    playerClass: "ENGINEER",
    score: 100
  };

  assert.equal(validateSubmission(valid), null);
  assert.equal(validateSubmission({ ...valid, nickname: "(TEST)" }), "invalid_nickname");
  assert.equal(validateSubmission({ ...valid, playerClass: "MEDIC" }), "invalid_player_class");
  assert.equal(validateSubmission({ runId: valid.runId, nickname: "TEST", score: 100 }), null);
  assert.equal(validateSubmission({ ...valid, score: -1 }), "invalid_score");

  const allowed = getCorsHeaders("https://kitchengun.github.io", "https://kitchengun.github.io");
  assert.equal(allowed["Access-Control-Allow-Origin"], "https://kitchengun.github.io");
  assert.equal(getCorsHeaders("https://invalid.example", "https://kitchengun.github.io"), null);
});

test("rejects an invalid current run id before reading D1", async () => {
  const response = await worker.fetch(
    new Request("https://example.test/v1/leaderboard?currentRunId=bad"),
    { ALLOWED_ORIGINS: "" }
  );
  assert.equal(response.status, 400);
  assert.deepEqual(await response.json(), { error: "invalid_current_run_id" });
});
