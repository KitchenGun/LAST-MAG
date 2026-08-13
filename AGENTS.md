# Project-local AI orchestration

## Scope and state

- Read `Goal.md` and the active worksheet before planning when they exist.
- Only the Sol coordinator may update Goal or worksheet state in normal project work. During an orchestration-configuration task, both are read-only.
- Preserve unrelated working-tree changes. Never reset, checkout, delete, or reformat files outside the assigned scope.

## Routing and delegation

- Follow `.codex/MODEL_ROUTING.md`. Use Sol for coordination and final high-risk decisions; use Terra only for bounded implementation or read-only review.
- Every delegated task must include `TASK-ID`, objective, inputs, read scope, write scope, forbidden changes, completion criteria, verification, and response format.
- One file has one writer at a time. Reviewers are read-only and never share a write scope with another agent.
- Subagents must not create subagents. Tasks outside their scope return `NEEDS_DECISION` without expanding scope.
- Use parallel work only for independent read-only discovery, test execution, or log analysis. Use separate worktrees for concurrent write sessions when supported; otherwise serialize writers.

## Verification and reporting

- Report changed files, commands run, verification evidence, and one final status: `PASS`, `REWORK`, `BLOCKED`, or `NEEDS_DECISION`.
- Do not describe an unrun check as passed. Separate static, build, Play Mode, and WebGL evidence.
- Stop for approval before destructive actions, external writes, purchases, or material scope expansion.
