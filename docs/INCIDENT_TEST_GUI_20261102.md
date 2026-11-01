# 2026-11-02 — Test-window timeout and configuration warning

## Observed symptoms
- A user-visible .NET Framework modal showed `TIMEOUT_GUI_SHUTDOWN`.
- A Local RAG Hub window displayed a locked-service warning about a damaged `config.json`, with log entries timestamped around 04:44.

## Verified cause and scope
- `TIMEOUT_GUI_SHUTDOWN` occurred only in the pre-fix synthetic `tests/ShutdownSmoke.cs` timer. The application code does not contain that exception text.
- The early smoke-test harness was allowed to display a WinForms window on the active Windows desktop and had a malformed test configuration during a failed initial run. It produced the confusing UI and framework modal.
- The **actual candidate package** `package/config.json` was separately inspected as a JSON object with model paths present and ports 11435/11436/11437; the installed `LocalRagHub.exe --selftest` returned 0 with `ConfigRead: PASS` and `ConfigValidation: PASS`.
- No evidence supported replacing the valid actual configuration with the old backup; the actual file was left unchanged.

## Corrections
- `tests/ShutdownSmoke.cs`: reject any non-synthetic model/configuration, hide the test window, and report timeout/failures through test exit status rather than unhandled WinForms timer exceptions.
- `src/LocalRagHub.cs`: distinguish historical log lines from the current process session. Preserve historical records, not misleadingly present them as current state.
- Continue all simulation exclusively under the independent `tests/` directory; production GGUFs and live services must never be launched to exercise test shutdown paths.

## Evidence and limitations
- Synthetic running-model shutdown smoke: PASS, owned PIDs=0 and test ports released.
- Corrupted synthetic config guard: nonzero exit with no test window, PASS.
- Compiled candidate and installed package selftest: PASS (exit=0).
- Installed config SHA-256 before/after: unchanged, `94B66ECAA7B69D0BE0BAC311E78384E9D485E92D067F33AA8CBDA3D58A643C00`.
- Live 8B/9B shutdown under real requests: NOT_RUN. Desktop visual acceptance by the user: NOT_RUN.
