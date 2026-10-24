# 更新日志

## v0.3.0（便携发布版）

### 新增
- 原生 C# .NET Framework WinForms 界面（深色 / 圆角 / 青绿色卡片）。
- 异步启动、停止、重启；界面在模型加载期间保持响应。
- 分别显示 Embedding / Reranker 的 PID、端口、运行时长与状态（READY / STOPPED / LOADING / FOREIGN）。
- NVIDIA GPU 显存、可用显存、负载、温度、功率监控。
- 运行日志面板与 `logs\hub.log` 落盘日志。
- 「一键复制酒馆 Agent 配置」，内容随当前设置（模型 ID / 端口 / pooling）实时变化。
- 设置窗口：llama-server 路径、Embedding / Reranker GGUF 路径、模型 ID、端口、Context、并行槽、GPU Layers、Embedding 池化策略（last / mean / cls / none）。
- 端口归属保护：只停止确实由本工具按当前配置启动的 `llama-server` 进程；被其它程序占用的端口显示 FOREIGN 且拒绝误杀。
- 配置文件原子保存（`File.Replace` + `.bak` 备份），避免写入中断损坏原配置。

### 便携 / 安全
- 不在程序内写死任何盘符；模型与 llama.cpp 路径均相对 exe 目录或用户自定义。
- 默认仅监听 127.0.0.1，不做开机自启。
- 首次启动未配置时给出清晰引导，绝不自动启动模型或改动旧端口。
- 新增无界面自检：`LocalRagHub.exe --selftest`，不创建任何窗口，输出到
  `selftest-result.txt` 并设置进程退出码。

### 已知限制
- 仅支持兼容 llama.cpp embedding / reranker 接口的 GGUF 模型，非任意 GGUF。
- 本发布包不包含 `llama-server.exe` 与模型权重。
- 部分 GUI 交互（点击、保存回显、剪贴板）需在真实桌面由用户现场验收。
