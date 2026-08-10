import assert from "node:assert/strict";
import test from "node:test";
import { getCorsHeaders, validateSubmission } from "./index.js";

test("accepts the smoke payload and rejects invalid trust-boundary input", () => {
  const valid = {
    runId: "11111111-1111-4111-8111-111111111111",
    nickname: "TEST",
    score: 100
  };

  assert.equal(validateSubmission(valid), null);
  assert.equal(validateSubmission({ ...valid, nickname: "(TEST)" }), "invalid_nickname");
  assert.equal(validateSubmission({ ...valid, score: -1 }), "invalid_score");

  const allowed = getCorsHeaders("https://kitchengun.github.io", "https://kitchengun.github.io");
  assert.equal(allowed["Access-Control-Allow-Origin"], "https://kitchengun.github.io");
  assert.equal(getCorsHeaders("https://invalid.example", "https://kitchengun.github.io"), null);
});
