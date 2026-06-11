# AliceBot 构建指南

## 项目结构

```
AliceBot/
├── WPF/                          # WPF 设置面板 (.NET 8)
├── Tools/IconConverter/          # PNG → ICO 图标转换工具
├── Assets/StreamingAssets/       # WPF 构建产物自动输出到此
└── Assets/Scripts/               # Unity C# 脚本
```

## 环境要求

- **Unity 2022.3** (Editor + Build Support for Windows)
- **.NET 8.0 SDK** (含 WindowsDesktop 运行时)
- **SkiaSharp 2.88.9** (ICO 工具依赖，`dotnet restore` 自动安装)

## 构建步骤

### 1. WPF 设置面板

```powershell
dotnet build .\WPF\AliceBotSettings.csproj
```

构建产物自动复制到 `Assets\StreamingAssets\`。

### 2. ICO 图标 (如需更换)

```powershell
dotnet run --project .\Tools\IconConverter -- Assets\Icon\ALICE.png Assets\Icons\app.ico
Copy-Item Assets\Icons\app.ico -Destination Assets\StreamingAssets\app.ico -Force
Copy-Item Assets\Icons\app.ico -Destination WPF\app.ico -Force
```

### 3. Unity Build

`File → Build Settings → Build`

## 本地测试

1. WPF 设置面板启动后通过 TCP `127.0.0.1:19876` 连接 Unity PipeServer
2. 日志文件：
   - Unity: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\AliceBotDesktop\Player.log`
   - TCP 调试: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\AliceBotDesktop\pipe_debug.log`
   - WPF 客户端: `%TEMP%\wpf_tcp.log`
3. 配置文件（Expression Defaults、WebSocket URL 等）保存在 `Application.persistentDataPath`
