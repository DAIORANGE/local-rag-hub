## Opt-in request monitoring (feature branch only)

The GUI includes **开启记录** (start relay capture), **请求列表** (record list) and double-click details on `[REQ #0001]` log lines. The request summary shows stage, declared source, group, HTTP status, duration and request byte count. Details show bounded request/response previews and any forwarding error.

The monitor is an **optional loopback-only local HTTP relay**. It does not intercept traffic sent directly to the existing llama.cpp servers:

| Stage | Existing server (unchanged) | Opt-in monitor endpoint |
| --- | --- | --- |
| Embedding | `127.0.0.1:11435` | `http://127.0.0.1:12435/src/TT-Nailong/v1` |
| Reranker | `127.0.0.1:11436` | `http://127.0.0.1:12436/src/TT-Nailong/v1` |
| Query | `127.0.0.1:11437` | `http://127.0.0.1:12437/src/TT-Nailong/v1` |

The caller may change `TT-Nailong` to a source label for its own plugin, or send `X-RAG-Source` on any monitor URL. This **identifies only the client-declared source**, not an independently verified application identity. Without either marker the source is labelled **来源未知**.

When several calls share the `X-RAG-Trace-ID` header, the GUI groups them under the same `RP:<id>`. Otherwise events from the same declared source within a 25-second activity window are shown as **≈候选** (heuristic association), not proof of one RP generation. Multiple concurrent generations may be grouped together by this heuristic.

The relay keeps the last 200 captured previews **in memory only** (request preview up to 700 characters, response preview up to 480 characters), and writes only brief metadata to the existing hub UI log. It does not write complete RP prompts, API keys, or responses to GitHub. The relay intentionally caps a single request at 16 MiB; requests beyond that should use the original ports instead.

**Lifecycle warning:** Capturing is OFF until clicked. When the GUI closes, forwarding ports 12435–12437 stop (with a warning). Do not point production plugins at these monitoring addresses unless you intend to keep the GUI running or have a separate fallback. The monitor never rewrites TT, SillyTavern, plugin configuration, or existing ports automatically.

This feature was tested with local synthetic HTTP servers (not actual TT plugin requests); real-world provenance, performance, and three-model simultaneous serving remain unverified.

Build this branch with `powershell -ExecutionPolicy Bypass -File scripts/build-windows.ps1`. The build uses **both** `src/LocalRagHub.cs` and `src/RequestMonitor.cs`.

