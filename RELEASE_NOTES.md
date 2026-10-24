Local RAG Hub v0.3.0-rc.1 — Windows portable pre-release

Includes:
- LocalRagHub.exe (native Windows GUI)
- config.example.json
- Chinese README_安装使用.md
- CHANGELOG.md, THIRD-PARTY-NOTICES.txt, SHA256SUMS.txt

Download and extract the ZIP to a writable folder. Download llama.cpp and compatible Embedding/Reranker GGUF separately; see the repository README for direct upstream links.

Checks completed: no-GUI compiler build, archive extraction/hash verification, command-line configuration tests. GUI settings/clipboard and model restart with this exact release build remain unverified, so this is marked prerelease.

The ZIP does NOT include CUDA runtimes, llama.cpp binaries, GGUF files, user data, tokens or billing records.