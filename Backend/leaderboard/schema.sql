CREATE TABLE IF NOT EXISTS scores (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    board_version TEXT NOT NULL,
    run_id TEXT NOT NULL,
    nickname TEXT NOT NULL,
    score INTEGER NOT NULL CHECK (score >= 0 AND score <= 2147483647),
    submitted_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (board_version, run_id)
);

CREATE INDEX IF NOT EXISTS idx_scores_ranking
ON scores (board_version, score DESC, submitted_at ASC, id ASC);
