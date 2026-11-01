# Local RAG Hub｜本地检索服务台

**让 Embedding 和 Reranker 在你的 Windows 电脑上运行，不必为每次向量化和重排请求支付云端 API 费用。**

- 深色桌面面板：独立监控两个模型的 READY / LOADING / STOPPED、PID、端口和 GPU 显存
- 一键启动、停止、重启，后台操作不锁住 UI
- 自定义模型文件、Model ID、端口、GPU Layers、并发、上下文与 Embedding pooling
- **一键复制酒馆 Agent 配置**，给 SillyTavern / TauriTavern 的本地 Agent 使用
- Windows 10/11、.NET Framework 4.8；当前基于 `llama.cpp` 的 GGUF Embedding 与 Reranker 接口

> **v0.3.0 预发布提醒：** 已通过无头编译、ZIP、配置校验测试；**最新版本 GUI 交互、保存/恢复及不同模型兼容性尚未全部人工验证**。请在正式数据库以外先验证，备份旧配置，不要直接重建生产索引。

## 1. 下载软件

在 **[Releases](https://github.com/DAIORANGE/local-rag-hub/releases)** 下载 `Local-RAG-Hub-v0.3.0-portable.zip`。解压到可写文件夹（例如 `D:\LocalRAGHub`），双击 `LocalRagHub.exe`。

发布 ZIP 只有启动器，**不含 llama.cpp，也不含大模型 GGUF**，下载完还需按下一步准备它们。

## 2. 下载程序与两个模型

| 用途 | 下载地址 | 需要的文件 |
| --- | --- | --- |
| 推理程序 | [llama.cpp Windows 发布页](https://github.com/ggml-org/llama.cpp/releases) | 选择适合自己显卡/CPU 的 Windows 版本，解压后找到 `llama-server.exe`（必须保留它依赖的 DLL） |
| Embedding | [Qwen3-Embedding-8B-GGUF（Qwen 官方）](https://huggingface.co/Qwen/Qwen3-Embedding-8B-GGUF/tree/main) | `Qwen3-Embedding-8B-Q4_K_M.gguf` |
| Reranker | [Qwen3-Reranker-8B-GGUF（gorse-io 转换）](https://huggingface.co/gorse-io/Qwen3-Reranker-8B-GGUF/tree/main) | `Qwen3-Reranker-8B-Q4_K_M.gguf` |

两个示例模型各约 **4.68GB**。这是已在 RTX 5090 上验证过的组合；其他兼容模型也可以选，但**不是任意 GGUF 都支持 embedding/rerank**，其他模型需按自身说明调整 pooling、上下文等参数。

把文件放在自己容易找到的地方即可，**不需要固定路径或 S 盘**。

## 3. 第一次设置（5 步）

1. 打开 `LocalRagHub.exe`，点右上角 **设置**。
2. 依次选择 `llama-server.exe`、Embedding 的 `.gguf`、Reranker 的 `.gguf`。
3. 按示例填写模型 ID：`Qwen3-Embedding-8B` 和 `Qwen3-Reranker-8B`。
4. 端口默认 `11435` / `11436`，Qwen3 Embedding 池化（pooling）选 `last`；其他参数先保持默认。
5. 保存后点击 **启动 / 恢复服务**。两个服务都显示 **READY** 才算连接正常。

> 显卡显存不够时，试着降低 GPU Layers、并行槽或换用 4B / 更小的兼容模型。它只监听本机 `127.0.0.1`，不自动开机启动。

## 请求日志与 RP 调用关联（候选功能）

新版面板的「运行日志」区域增加了 **开启记录** 和 **请求列表**。记录模式不截获原有端口，而是提供独立的本地 HTTP 代理入口；软件与插件都不会被自动重配。必须把希望观察的插件连接地址主动调整至监控入口，才有可用于归因的请求记录。

- Embedding: `http://127.0.0.1:12435/src/TT-Nailong/v1`
- Reranker: `http://127.0.0.1:12436/src/TT-Nailong/v1`
- Query: `http://127.0.0.1:12437/src/TT-Nailong/v1`

日志摘要会显示服务、来源标签、关联分组、状态、耗时与数据大小。**双击 [REQ #编号] 行**或在请求列表中双击记录可查看限长预览。来源标签由请求端申报，不能作为独立身份认证；共享 `X-RAG-Trace-ID` 才能明确关联同一轮 RP。否则仅给出标为 `≈候选` 的时间邻近推测。直接调用原服务端口时无法识别来源。

原始剧情预览只保存在软件内存中，关闭程序后不保留；日志文件仅写简略摘要。开启监控不会启动模型服务，但**关闭面板会停止代理入口**，因此不可直接将生产插件永久依赖此实验代理。详见 [请求监控完整说明](docs/REQUEST_MONITOR.md)。

该分支的 Windows 构建需要 `src/LocalRagHub.cs` 和 `src/RequestMonitor.cs` 两份源码，可运行 `scripts/build-windows.ps1` 生成候选 EXE。真实 TT/奶龙插件接入、完整 RP 请求关联与三模型性能仍未验收。

## 可选第三服务：检索前 Query 重写（功能分支候选）

此功能目前只在 `feature/query-rewrite-service-20261102` 上验证，尚未作为正式版发布。默认 **QueryEnabled=false**，原有 Embedding/Reranker 端口 11435 / 11436 与旧配置保持不变。

主界面三个服务卡分别提供「启动 / 停止」按钮，且右上角「Query设置」可在另外两个模型运行时独立保存 Query 配置。也可在主设置中进入「Query 重写设置（可选）」，选择兼容 llama.cpp 的 **Chat GGUF** 并启用，可单独设置模型 ID、端口、上下文长度、并行数与 GPU Layers。初始示例：`Qwen3.5-9B`（Q5_K_M）、端口 `11437`、上下文 `8192`、并行 `1`、GPU Layers `99`，默认尝试关闭思考。4B 为 A/B 对照，也可选择其他受 llama.cpp 支持的 Chat GGUF。

- Chat Endpoint：`http://127.0.0.1:11437/v1/chat/completions`
- 模型清单：`http://127.0.0.1:11437/v1/models`（应返回设置的模型别名）
- 流式：客户端可使用 `stream:false`，`max_tokens:1024`。
- 客户端附带 `enable_thinking:false` 时，本程序不解析它；兼容性以具体 llama.cpp 版本实测为准。
- 启用第三服务后，启动 / 恢复 / 停止 / 重启会遍历三个服务，端口已被其他进程占用仍显示 FOREIGN 且不会结束非本应用进程。
- 真实 Chat 推理及 3,000 token 输入的性能验收尚未执行。关闭思考由 `--reasoning off` 和 `--chat-template-kwargs` 实施，并需在实际模型上验证结果。第三个模型的权重、KV 缓存与运行时开销必须依据实际显存检测决定 GPU/CPU 卸载比例，不能预先承诺三模型全驻显存。
- 更换已有两个模型、重建索引及向 TT / SillyTavern 写入配置均 **不在本功能更新内**。


### 2026-11-02 — 本地安全加固（仍属于功能分支候选）

- 进程归属校验改为精确核对模型文件路径、`--host`、`--port` 和 `--alias`；相似文件名、端口前缀与别名后缀不会被错误视为可停止的服务。
- 如果 `config.json` 已损坏或无法解析，面板禁止按临时默认配置进行服务操作，并拒绝覆盖原文件；须先备份、修复或移走损坏文件。
- 无界面 `--selftest` 的配置异常返回非零退出码（3），不会把 `NOT-READY` 标为通过。
- 总状态优先显示端口 `FOREIGN` 冲突，即使其他服务正常运行也不能掩盖。
- 检查代码：`tests/GuardRegression.cs`、`tests/ConfigRegression.cs`、`tests/README.md`。本轮隔离回归共 22 项断言通过；真实 GGUF 推理、GUI 点击、三服务共存与延迟均未测试。
- 只在此仓库的功能分支及隔离候选 EXE 中更新；不自动替换正式数据库、现有向量索引或其他项目。

## 4. 在酒馆中怎么填？

程序点击 **「一键复制酒馆 Agent 配置」**，把文本粘贴给本地 Agent，或手动填入支持相应接口的记忆插件。

默认地址：

| 设置 | 内容 |
| --- | --- |
| Embedding 完整地址 | `http://127.0.0.1:11435/v1/embeddings` |
| Embedding Model ID | `Qwen3-Embedding-8B` |
| Reranker 完整地址 | `http://127.0.0.1:11436/v1/rerank` |
| Reranker Model ID | `Qwen3-Reranker-8B` |
| API Key | 本地服务没有鉴权；若插件强制要求填 Key，可输入占位文字 `local-rag` |

- **奶龙（龙血玄黄 / shujuku）**：需要完整 Endpoint 地址。
- **狐忆等其他插件**：请先核实其接口格式，不可假定相同。
- **更换 Embedding 模型或量化方式**：旧向量索引未必兼容。正式切换前应备份并在隔离环境验证，再按插件规则重建索引。
- **不要**把 Embedding / Reranker 地址填进正文聊天模型（Chat Completion）接口。

## 5. 常见问题

**只启动一个模型？** 看日志、GPU 显存、端口是否冲突和 GGUF 是否匹配；缺失的一方可点启动/恢复，不需要重复启动已正常的模型。

**提示 FOREIGN？** 端口被其他进程占用，程序会拒绝关闭未知进程。改用空闲端口或手动排查。

**关闭软件会停止模型吗？** 不会。需要释放显存，请先点「停止服务」，再关闭窗口。

**想看更详细的说明？** 阅读发布包内 `README_安装使用.md`，里面有逐字段指南和故障排查。

## 开源与免责声明

代码见 [src/LocalRagHub.cs](src/LocalRagHub.cs)。本项目以 MIT License 发布；不附带 llama.cpp、CUDA DLL 或 Qwen 权重，相关软件及模型各自遵循其上游许可证。详见 [第三方说明](THIRD-PARTY-NOTICES.txt)。

本工具仍属预发布版本，不承诺支持所有硬件或模型。只对本地服务进行管理；不会自动替你修改酒馆聊天、数据库和世界书。
