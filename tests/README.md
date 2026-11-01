# Local RAG Hub — Candidate regression tests

These tests target the opt-in three-service feature branch only. Do not execute them in a production installation.

## Run on Windows (.NET Framework 4.8)

1. Copy the candidate `LocalRagHub.exe` and a **valid copy** of `config.json` into a separate, writable test directory. Do not reuse your live config.
2. Use the .NET Framework C# compiler (`csc.exe`) to build `tests/GuardRegression.cs` as a console executable in that same test directory, referencing `LocalRagHub.exe`, `System.Web.Extensions.dll`, `System.Windows.Forms.dll`, and `System.Drawing.dll`. Run it.
3. Build `tests/ConfigRegression.cs` in the same way. Run it only against the isolated test directory. It intentionally writes an invalid config and restores the original bytes.
4. Run `LocalRagHub.exe --selftest` with a valid config, a colliding Query port, and malformed JSON. A valid config must return 0; invalid config must return 3 and never start a UI.

## Tested on 2026-11-02

- Command-line ownership: valid Query/Embedding commands accepted; unrelated or ambiguously matching model paths, aliases, ports, hosts, and missing or duplicated flags rejected.
- Corrupt config: errors surfaced, existing file not silently overwritten; valid isolated config save/restore works.
- Model service operations, real GGUF compatibility, GUI click behaviour, and three-model latency are **NOT_RUN**. These tests neither download models nor start llama-server.

The existing `main` release has not been updated. Keep development and test evidence isolated in this repository.
