# AI orchestration

## Checked availability

| Role | Model | Effort | Status |
| --- | --- | --- | --- |
| Coordinator and final decision | `gpt-5.6-sol` | medium by default; high/xhigh for high-risk work | configured |
| Bounded implementation | `gpt-5.6-terra` | medium | configured |
| Read-only review | `gpt-5.6-terra` | high | configured |
| High-volume batch work | `gpt-5.6-luna` | low/medium | configured |
| Exploration and mechanical edits | `gpt-5.4-mini` | low/medium | configured |
| Interactive local iteration | `gpt-5.3-codex-spark` | low/medium | available; no autonomous agent file by default |
| Legacy escalation only | `gpt-5.5`, `gpt-5.4` | medium/high | available; not assigned a default role |

The project default is Sol medium. The temporary effort used to configure this policy does not change that default.

## Routing matrix

| Work shape | Route | Effort | Notes |
| --- | --- | --- | --- |
| Small, reversible lookup or formatting | coordinator directly | low | Do not delegate a single cheap read. |
| Bounded implementation with clear acceptance checks | Terra worker | medium | One writer per file. |
| Repetitive extraction, classification, or structured summaries | Luna batch/structured | low/medium | Read-only; escalate exceptions to Terra. |
| File map, grep, or narrow mechanical edits | Mini explorer/mechanical | low/medium | Mechanical writer needs an explicit write scope. |
| User-supervised local iteration | Spark directly | low/medium | Do not create a persistent Spark agent unless a repeated role is proven. |
| Cross-file implementation, review, or ambiguous failure | Sol coordinator + Terra reviewer | high | Coordinator owns the final decision. |
| Security, data loss, release, migration, or unresolved design decision | Sol coordinator | xhigh | No external or destructive action without approval. |

## Mandatory task contract

Every delegated task must contain:

```text
TASK-ID:
Objective:
Inputs:
Read scope:
Write scope:
Forbidden changes:
Completion criteria:
Verification:
Response format:
```

Agents must return `NEEDS_DECISION` for missing requirements, ownership conflicts, or scope expansion. A failed check is reported as `REWORK` or `BLOCKED`; it is never silently retried outside scope.

## Enforced operating rules

1. Sol owns planning, task decomposition, state updates, conflict resolution, and final synthesis.
2. The project default is Sol medium; use high/xhigh only when the decision risk justifies it.
3. Terra workers receive a bounded task contract and only the assigned write scope.
4. Terra reviewers are read-only and return `PASS`, `REWORK`, or `BLOCKED`.
5. No agent creates subagents.
6. Goal and worksheet state is Sol-owned in normal work and read-only during this configuration task.
7. Never assign two writers to the same file concurrently.
8. A reviewer never edits a writer's scope.
9. Parallelism is limited to independent read-only discovery, tests, and log analysis.
10. Concurrent write work uses separate worktrees when the client supports them.
11. Without separate worktrees, writers run serially.
12. Preserve dirty files outside the explicit write scope.
13. Do not reset, checkout, delete, or bulk-format without explicit authorization.
14. Do not add a dependency, service, account, or purchase without explicit authorization.
15. Do not perform external writes without explicit authorization.
16. Use existing project conventions before proposing an abstraction.
17. Ask for a decision when a requirement materially changes product behavior.
18. Do not infer success from static inspection when runtime evidence is required.
19. Classify verification as static, build, runtime/Play Mode, WebGL, or `not_run`.
20. Keep commands and evidence in the final task report.
21. Keep agent prompts lean; include only task-specific context and acceptance criteria.
22. Use the lowest effort that can meet the required verification quality.
23. Escalate to Sol high/xhigh for security, destructive, release, migration, or unresolved cross-file decisions.
24. Spark is used directly for short user-supervised iterations; it is not a persistent autonomous worker by default.

## Result template

```text
TASK-ID:
STATUS: PASS | REWORK | BLOCKED | NEEDS_DECISION
Changed files:
Verification:
Evidence:
Next action:
```
