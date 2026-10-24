# Local RAG Hub｜本地检索服务台 v0.3.0（便携版）

一个原生 C# .NET Framework WinForms 小工具，用两个本地 llama.cpp 服务分别跑
**Embedding（向量化）** 和 **Reranker（重排）** 模型，并提供：

- 一键「启动 / 恢复服务」「停止服务」「重新启动」，异步执行，界面不卡死；
- 分别显示 Embedding / Reranker 的 PID、监听端口、运行时长与 READY/STOPPED/LOADING/FOREIGN 状态；
- NVIDIA GPU 显存占用、可用显存、GPU 负载、温度、功率监控；
- 运行日志；
- 「一键复制酒馆 Agent 配置」：把当前设置的模型 ID、端口、完整 API URL 复制到剪贴板，直接粘到
  SillyTavern / TauriTavern 的本地 Agent 或记忆插件。

> 重要：本工具是**本地检索服务**，不是聊天 / 角色扮演生成模型。
> 请勿把它的地址填进聊天模型（Chat Completion）位置，也不要指望它生成正文。

---

## 0. 运行环境

| 项目 | 要求 |
| --- | --- |
| 系统 | Windows 10 / 11（64 位） |
| 运行时 | .NET Framework 4.8（Win10/11 通常自带；如缺失请从微软官网安装） |
| 加速 | 推荐 NVIDIA 显卡 + 已装驱动；工具通过 `nvidia-smi` 读取 GPU 指标（没有也不影响启动模型，只是不显示指标） |
| 模型程序 | 需要你自己的 llama.cpp 发行版（`llama-server.exe`，建议 CUDA 版本） |
| 模型权重 | 需要你自己的、**与 llama.cpp embedding / reranker 接口兼容的 GGUF 模型** |

本发布包**不包含** `llama-server.exe`、也**不包含任何 GGUF 模型权重**（体积太大且版权/版本各异）。
请自行下载并放置，然后在设置里选择路径。

### 关于模型兼容性（请务必阅读）

- 本工具通过 llama.cpp 的 `--embedding` / `--reranking` 接口工作，只支持**兼容 llama.cpp 服务接口的 GGUF**，
  **不是**任意 GGUF 都能用。
- Embedding 与 Reranker 是两类不同模型，需要各自匹配的权重文件。
- **向量维度必须通过实际响应核对**：`Qwen3-Embedding-8B` 为 4096 维，其它模型（不同尺寸 / 量化 / pooling）
  维度可能不同，**不能照搬 4096**。维度填错会导致记忆插件检索异常。
- 更换 Embedding 模型、量化方式或维度后，**旧向量索引不能假定兼容**。生产数据请先备份、隔离验证，
  再按插件流程重建索引。

---

## 1. 首次启动

1. 把整个 `Local-RAG-Hub-v0.3.0-portable` 文件夹解压到**可写**目录（例如 `D:\LocalRAGHub\`）。
   不要把程序放在 `C:\Program Files` 等只读位置，否则无法写入 `config.json` 与日志。
2. 双击 `LocalRagHub.exe`。
3. **第一次启动没有 `config.json`**，程序会在主窗口顶部显示
   `⚠ 尚未完成配置：请点击右上角「设置」…`，并**自动弹出设置窗口**。
   程序**不会**自动启动模型，也**不会**改动任何旧端口。
4. 在设置窗口里逐项填写（见下一节），点「保存配置」。

> 建议目录布局（相对路径以 exe 所在目录为基准，程序**不会写死任何盘符**）：
>
> ```
> Local-RAG-Hub-v0.3.0-portable\
>   LocalRagHub.exe
>   config.json            ← 保存后自动生成
>   llama.cpp\
>     llama-server.exe
>   models\
>     embedding\  <你的 embedding gguf>
>     reranker\   <你的 reranker gguf>
>   logs\                  ← 自动生成
> ```

---

## 2. 设置窗口逐项说明

| 字段 | 说明 |
| --- | --- |
| ① llama-server.exe 路径 | 你的 llama.cpp 里 `llama-server.exe` 的完整路径。可点右侧「…」浏览选择。 |
| ② Embedding GGUF 路径 | 向量模型权重（如 `Qwen3-Embedding-8B-Q4_K_M.gguf`）。 |
| ③ Reranker GGUF 路径 | 重排模型权重（如 `Qwen3-Reranker-8B-Q4_K_M.gguf`）。 |
| ④ Embedding 模型 ID | 对外暴露的模型名，可自定义（默认 `Qwen3-Embedding-8B`）。 |
| ⑤ Reranker 模型 ID | 同上（默认 `Qwen3-Reranker-8B`）。 |
| Embedding 端口 | Embedding 服务监听端口，默认 `11435`。范围 1024–65535，两个端口必须不同。 |
| Reranker 端口 | Reranker 服务监听端口，默认 `11436`。 |
| Context Tokens | 上下文长度（`-c`）。默认 `16384`。 |
| Embedding 并行槽 (-np) | Embedding 并发槽位，默认 `4`。 |
| Reranker 并行槽 (-np) | Reranker 并发槽位，默认 `8`。 |
| GPU Layers (-ngl) | 卸载到 GPU 的层数，默认 `99`（尽量全量）。显存不足时调小。 |
| ⑥ Embedding 池化策略 | `last` / `mean` / `cls` / `none`。**Qwen3-Embedding 用 `last`**。其它模型按模型说明选择。 |

窗口底部会实时显示文件检测结果：三个文件路径都存在时显示绿色「三项齐全」，
否则提示补全缺失项（✘）。保存时会做校验，配置不合法会弹窗提示且**不会写入**。

**修改配置前请先在主窗口点「停止服务」**：正在运行的模型切换路径 / 端口容易混淆新旧模型，
程序在检测到有模型运行时也会提醒你先停止。

---

## 3. 启动与验证

1. 主窗口点「▶ 启动 / 恢复服务」。
2. 状态会依次经历 `LOADING`（加载中，模型大时可能需要数十秒）→ `READY`。
3. 顶部总状态显示 `✔ 运行正常 · 两个服务 READY` 即表示两个服务都就绪。
4. 如果长时间停在 `LOADING` 或变成 `STOPPED`，请看 `logs\` 下对应的
   `embedding-server.log` / `reranker-server.log`，通常是显存不足或模型路径/参数问题。

也可以手动用浏览器/命令行核对健康状态：

```
http://127.0.0.1:11435/health
http://127.0.0.1:11436/health
```

两个服务默认**只监听 127.0.0.1**（仅本机），不做开机自启。

---

## 4. 复制给 SillyTavern / TauriTavern

1. 主窗口点「▣ 一键复制酒馆 Agent 配置」。
2. 剪贴板里会得到当前设置对应的文本，包含：

   - Embedding 模型 ID 与 Base URL：`http://127.0.0.1:<Embedding端口>/v1`
   - Embedding 完整 Endpoint：`http://127.0.0.1:<Embedding端口>/v1/embeddings`
   - Reranker 模型 ID 与 Base URL：`http://127.0.0.1:<Reranker端口>/v1`
   - Reranker 完整 Endpoint：`http://127.0.0.1:<Reranker端口>/v1/rerank`
   - 模型权重路径、健康检查地址等。

3. 把它粘贴到 TT 酒馆对应的**本地记忆 / RAG Agent 或插件**配置里。
   - **奶龙（龙血玄黄 / shujuku）**支持完整 Endpoint 格式。
   - **狐忆**及其它记忆插件请按其自身接口契约核验字段名。
4. 本启动器不设 API Key；如果插件强制要求填写，可填任意非真实占位字符串（如 `local-rag`）。

---

## 5. 更换模型 / 重建索引注意事项

- 更换 Embedding 模型、量化方式或**维度**后，旧向量索引**不能假定兼容**。
- 生产环境请：**先备份 → 隔离验证 → 按插件流程重建索引**。
- 未经授权不要修改世界书、聊天记录和正式数据库。

---

## 6. 常见问题（FAQ）

**Q：界面能显示中文，但命令行窗口里日志是乱码？**
A：那是终端代码页问题，不影响 WinForms 界面显示。`logs\hub.log` 是 UTF-8 编码，用支持 UTF-8 的编辑器查看正常。

**Q：第一次打开没有模型，会不会自动帮我下载或启动？**
A：不会。程序只会提示「尚未完成配置」并引导你打开设置，绝不自动下载、自动启动模型或改动旧端口。

**Q：卡在 LOADING 很久？**
A：大模型 + 首次加载较慢；若超过 90 秒仍无 READY，程序会提示超时。请查看 `logs\` 对应服务日志。

**Q：某个服务显示 STOPPED？**
A：可能是该模型进程已退出。看服务日志确认原因（常见为显存不足、GGUF 不兼容、端口冲突）。

**Q：显示 FOREIGN（端口被占用）？**
A：该端口被其它程序占用。程序会**拒绝**停止不属于本工具的进程，避免误杀你的生产服务。
请改一个空闲端口，或先处理占用端口的程序。

**Q：GPU 显存不足？**
A：调小「GPU Layers (-ngl)」或减小 Context，也可降低并行槽数量。

**Q：需要重建索引吗？**
A：更换 Embedding 模型 / 量化 / 维度后需要；仅改端口或重启服务通常不需要。

**Q：支持任意 GGUF 吗？**
A：不支持。只支持兼容 llama.cpp embedding / reranker 服务接口的模型，并且 pooling 等参数要与模型匹配。

---

## 7. 卸载 / 回滚

- 本工具是绿色便携程序：删除文件夹即可，不写注册表、不设开机自启。
- 想回滚只需换回旧版 `LocalRagHub.exe`。
