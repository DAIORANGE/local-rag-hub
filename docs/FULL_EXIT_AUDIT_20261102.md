# Full-exit ownership audit — 2026-11-02

## Problem

The previous X-close implementation relied on an in-memory `HashSet<int>` populated only by the current GUI process. A reopened window could recognise a configured `llama-server` as READY but lack evidence that this product started it. An abrupt GUI termination also did not necessarily terminate the model process. These are **design vulnerabilities**, not proof that a model was left running in the reported session.

## Independent current-state observation

At 05:24 JST on 2026-11-02, an inspection of the authorised Windows host showed:

- `LocalRagHub.exe` processes: 0
- `llama-server.exe` processes: 0
- Listeners on 11435, 11436, 11437, 12435, 12436, 12437: 0
- Previous real Embedding/Reranker shutdown log entries recorded both model PIDs stopped.

Thus, the current session was **clean**, despite the underlying design gap.

## Candidate fix

- New `src/ModelProcessOwner.cs`: a Windows **Job Object** with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` attaches newly launched child models to the GUI. The OS ends these child processes when the owning GUI process exits or crashes.
- An app-local `managed-model-processes.json` launch journal stores PID, creation ticks, exact model, alias, port, and executable. A subsequent launch can stop **only** a process matching the complete recorded identity, preventing PID-reuse mishaps.
- If the journal cannot be parsed or a model cannot be assigned to a Job Object, a new model launch is **refused/cleaned up** rather than running without shutdown protection.
- Close verifies the recorded child processes are gone before dismissing the window. If a matching service is running but its origin is **not** recorded, the close handler blocks and shows a diagnostic rather than blindly killing another program's process or silently claiming complete closure.
- No registry, model weights, listening ports, TT/SillyTavern settings, or vector indexes were changed.
- The journal is local runtime state, **not** a file to commit to public GitHub.

## Execution evidence (isolated fake TCP model only)

| Scenario | Result |
| --- | --- |
| Normal GUI close after synthetic READY | PASS; child PID gone, test port released |
| Close while synthetic model loading | PASS; PID gone, port released |
| Force-kill GUI process while synthetic model READY | PASS; OS Job Object ends child, port released |
| Simulate a new GUI reading a verified launch journal | PASS; matching previous child stopped |
| Simulate reused PID / mismatched start timestamp | PASS; unrelated active fake process remains untouched |
| Installed candidate `--selftest` | PASS; valid config, exit 0 |
| Config changed by update | NO; SHA unchanged |
| Live GGUF loaded in the production GUI and closed again | NOT_RUN for this patch |
| Third-party externally launched llama-server close policy | SAFE-BLOCKED BY DESIGN; must be identified, not guessed |

Local synthetic harnesses: `tests/ShutdownSmoke.cs`, `tests/JobCrashHarness.cs`, `tests/JournalReplay.cs`. The PowerShell harnesses and results are kept in the product's isolated Windows testing tree. Do not run synthetic tests against production models.

## Installation and rollback

Updated only the independent Local RAG Hub candidate executable; the desktop shortcut was left pointing to the same executable location. The pre-fix executable was saved as `ui-rollback/LocalRagHub-before-verified-full-exit-20261102.exe`.

New EXE SHA-256: `B567C72F487A5DDB82CE26F54458EEDED8045B3CC3EAAFEE12A4D3B2BB3005FC`.

This does not claim formal main release acceptance or live TT plugin integration. The original ports and Query disabled state remain unchanged.
