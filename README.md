# Maaa Assorted Assistant Arranger

[English](README.md) | [简体中文](README_CN.md)

A Windows desktop hub for running daily game assistants in sequence, monitoring their processes, and handling notifications and cleanup. MAAA is independent of [MAA](https://github.com/MaaAssistantArknights/MaaAssistantArknights), and can orchestrate MAA alongside other tools.

## Features

- Launch executable tools or registered URIs, with arguments, delays, process monitoring, and per-task timeouts.
- Run individual tasks or an ordered task chain; add, disable, configure, and delete tasks in the UI.
- Start automatically at login within a configured time window, optionally at a randomized time.
- Mute audio and lower brightness through Twinkle Tray, with optional restoration on completion.
- Send configurable webhook notifications, or relay assistant webhooks to collect richer execution logs.
- Generate AI summaries through configurable providers, with separate HTTP proxy settings per provider.
- Record task execution and export logs.
- Switch between system, light, and dark appearance, with live updates and a saved preference. The title bar shows the installed version.
- Enable **UDP proxying per task**, using a bundled component built from open-source networking code; see [UDP proxy setup](docs/udp-proxy.md).
- Optionally shut down Windows after successful completion.

## Install and run

Download a Windows x64 package from [Releases](https://github.com/SHthemW/MaaaAssortedAssistantArranger/releases), extract the whole archive, and run `MaaaAssortedAssistantArranger.exe`. Framework-dependent packages require the .NET 8 Desktop and ASP.NET Core runtimes.

The first launch creates `appsettings.Local.json` beside the executable. UI changes are saved automatically. To migrate settings, close MAAA and copy this file to the new program directory. The file contains service credentials; keep it private.

Choose **跟随系统 / 浅色 / 深色** under **设置 → 主题**. Existing configurations default to following the Windows app theme; manual selection takes effect immediately, including the title bar and help window.

### Task settings

Each task's **配置** button opens its settings:

| Field | Purpose |
| --- | --- |
| Task name | Human-readable name used in logs and notifications. |
| Tool path | Executable path, or a registered URI such as `bettergi://startOneDragon`. |
| Arguments | Arguments passed to executable launches. |
| Monitored process | Process name, with or without `.exe`. The task waits for it to appear and exit. If left empty, the task completes after launch unless log monitoring is enabled. UDP proxying requires a monitored process. |
| Delay | Milliseconds to wait before launching the task. |
| Timeout | Maximum minutes to wait for the monitored process. A failed or timed-out task does not prevent subsequent tasks from running. |
| Log monitoring | Off by default. The inactivity window defaults to 10 minutes. Each external log received by the shared Webhook relay restarts the current task's window. If the window expires first, MAAA force-stops the task and continues the chain. |
| UDP proxy | Off by default. Game presets select target processes automatically; other tasks follow the monitored process. Optional exact `.exe` filenames override automatic selection. Configure an unauthenticated SOCKS5 endpoint. Administrator privileges and the Windows Packet Filter driver are required. |

Log monitoring uses the existing Webhook relay address. Because the relay does not receive a task identifier, incoming logs during a run count toward the currently running task. With no monitored process, MAAA waits for the launched process to exit. If a launcher, including a URI handler, provides no active process when termination is needed, MAAA uses the configured monitored process name.

Only enabled tasks participate in a chain. When the queue finishes, notification and summary settings apply even if the final task timed out or was terminated. Automatic shutdown still requires all tasks to succeed.

For Star Rail, leave **自定义(可空)** empty: the preset targets `StarRail.exe` automatically. The monitored process can be `StarRail`, and the upstream is the actual FlClash SOCKS5/mixed port, for example `127.0.0.1:7890`. **生效进程** previews the exact targets. Keep FlClash running and disable **运行前关闭 FlClash** for this setup.

### Scheduling and quiet operation

Enabling login startup registers the current executable in Windows Task Scheduler with `--autorun`. An automatic launch outside the allowed time window exits silently. An end time earlier than the start time represents an overnight window. Moving the executable requires re-registering startup.

Audio, brightness, and shutdown can be limited to automatic runs. Brightness control requires a running Twinkle Tray instance. Restoration uses the state captured before MAAA changed it. UDP tasks require the scheduled task to run with highest privileges.

### Webhooks and AI summaries

Webhook requests use the configured JSON template; `__TIME__` and `__CONTENT__` expand to the log time and content. You can filter notification categories or send only the final AI summary. The webhook relay listens on its configured port (default `5058`), records requests, and forwards them to the configured original URL.

AI settings include provider, API key and URL, model, prompts, thinking, temperature, streaming, timeout, retry count, and retry delay. HTTP proxy settings belong to the selected provider; disabling a provider's proxy forces a direct connection for that provider. Use the UI tests to validate credentials and prompts, including the most recent saved prompt log.

### Logs

Session logs are saved to `logs/session-*.log`, AI prompts to `logs/aiprompt-*.log`, and exported logs to `logs/runtime-*.log`. Session and prompt logs are retained for five days by default.

## Development

Requirements: Windows x64, .NET 8 SDK, CMake 3.23+, Visual Studio 2022 Build Tools with the MSVC v143 C++ workload and a Windows SDK.

```powershell
dotnet build
dotnet test src/Tests/MaaaAssortedAssistantArranger.Tests.csproj
ctest --test-dir artifacts/native-udp -C Release --output-on-failure
dotnet publish -c Release -r win-x64 --self-contained false
```

The .NET build also compiles the native UDP component using CMake and vendored sources. Publishing requires Go 1.26+, downloads verified driver/Mihomo sources, builds the upstream with full-size UDP receive buffers, and bundles its complete corresponding source. Task startup automatically prepares a missing driver. A local FlClash upstream is followed through a task-owned exit without rewriting FlClash rules. If the SDK workload resolver fails, set `$env:MSBuildEnableWorkloadResolver='false'` before building.

Every publish creates an archive in `bin/Release-Archives/` named `MaaaAssortedAssistantArranger-win-x64-{portable|self-contained}-yyMMdd-HHmmss.zip`. The package includes the native component, its corresponding source, licenses, and UDP documentation; private local settings are excluded.

Source is organized under `src/`: WPF views and view models, models, services, tests, and `Native/UdpProxy/`. Legacy batch scripts remain in `src/batch/`.

## Open-source UDP component

The component reuses the native router from [ProxiFyre 2.6.4](https://github.com/wiresock/proxifyre/tree/v2.6.4), the Windows Packet Filter API, and Microsoft GSL. It is compiled as `components/udp/MaaaUdpProxy.exe`, with no ProxiFyre GUI, Windows service, C++/CLI assembly, or external ProxiFyre installation. See [third-party notices](docs/third-party/NOTICE.md) for licenses, pinned sources, and local adaptations.
