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
