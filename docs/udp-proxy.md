# Per-task UDP proxy

[English](udp-proxy.md) | [简体中文](udp-proxy_CN.md)

MAAA ships `components/udp/MaaaUdpProxy.exe`, compiled from the vendored open-source native router. It does not require an installed ProxiFyre application, GUI, or Windows service. No game DLL injection is used.

## First use

1. Extract the complete MAAA package, then run MAAA as administrator.
2. Open a task's configuration and enable **为此任务启用 UDP 代理**.
3. Task startup automatically prepares a missing driver. The full package contains the official Windows Packet Filter x64 MSI; MAAA verifies its pinned SHA-256 before unattended installation and downloads it only if absent. **准备 UDP 组件** also prepares it in advance. Installation may briefly reconnect adapters; a required reboot is reported, never forced.
4. Keep a SOCKS5 upstream running. For FlClash, enter its actual mixed/SOCKS5 listening host and port; the example `127.0.0.1:7890` is only a default.
5. Leave **自定义(可空)** empty to use automatic game presets, or the monitored process for other tasks. Check **生效进程** for the exact selection. To override it, enter complete executable filenames separated by commas or semicolons. Existing multiline overrides remain supported. Matching applies to every instance with that exact filename; directories, wildcards, and automatic child-process inheritance are unsupported.
6. Set the task's monitored process to a process that stays alive for the whole operation (`StarRail` for directly monitoring the game), and set a positive timeout. Close existing target processes before starting.
7. When using a local FlClash upstream, disable **运行前关闭 FlClash**. Conflicting task chains are rejected before the close hook; loading such a configuration also skips the startup close.

For unattended use, configure the existing Windows scheduled task to run MAAA with highest privileges. The system proxy itself need not be enabled if the configured SOCKS5 listener remains available.

## Automatic process selection

An explicit custom list takes precedence. With an empty list, MAAA recognizes built-in task IDs, known game monitor names, or task names containing the game/assistant name. It uses these presets:

| Game | UDP target |
| --- | --- |
| Honkai: Star Rail / March7th | `StarRail.exe` |
| Zenless Zone Zero | `ZenlessZoneZero.exe` |
| Genshin Impact / BetterGI | `YuanShen.exe`; `GenshinImpact.exe` when that is the monitored process |
| Arknights: Endfield / MaaEnd | `Endfield.exe` |
| Wuthering Waves / ok-ww | `Client-Win64-Shipping.exe` (the game, rather than its launcher) |

Other tasks use the monitored process, adding `.exe` if needed. Arknights has no universal emulator preset, so its configured monitor determines the target. Filenames containing dots are preserved. An empty monitor still fails lifecycle validation; automatic selection never selects all processes. Presets and custom lists both match every same-named instance, including other applications using the same executable filename.

Automatic targets are resolved when needed, without writing them into the custom field. Existing empty configurations work immediately, changing the monitor updates the fallback, and clearing a custom override restores automatic mode. The preview, engine arguments, and startup log use the same selection.

## Task lifecycle

MAAA validates the settings and checks SOCKS5 `UDP ASSOCIATE` support. It launches its hidden component, waits up to 20 seconds for an explicit readiness acknowledgement after the listeners, process associations, and filter have started, and only then launches the task tool.

For a running local FlClash upstream, MAAA reads its cached configuration and saved node choice, then runs the bundled Mihomo as a task-owned local exit forced through that node. This prevents process/GEOIP direct rules from routing game UDP back to a direct connection. Direct, unavailable, and UDP-disabled selections fail explicitly. Credentials are kept in an administrator-only temporary directory, removed at exit, and excluded from logs. Other SOCKS5 upstreams continue using their configured endpoint.

Large IPv4 replies are stripped of SOCKS5 framing before MTU-aligned fragment injection and Windows reassembly, avoiding the upstream redirector's inability to rewrite fragmented replies.

For an already matched `StarRail.exe`, large batches that fully validate as token-bearing 28-byte KCP frames are separated at segment boundaries and returned through the ordinary UDP path. Payloads remain unchanged; incomplete/invalid batches are not split and other processes do not use this adaptation.

The component remains alive while MAAA monitors the configured process. UDP tasks poll every 500 ms independently of the normal scheduling interval. Monitor exit, timeout, tool-launch failure, cancellation, or closing MAAA all stop the component. A stop request first asks the router to release its filters gracefully; a Windows Job Object terminates a stalled component. Parent pipe EOF and the Job Object also cover abnormal parent termination. Existing game processes are not terminated when you stop a task.

If the component exits unexpectedly, the task is marked as failed. A startup error prevents the tool from launching. Cleanup completes before the next task starts. The driver stays installed because it is a shared system component; the forwarding process does not stay resident or register a Windows service.

## Routing scope and limits

- Only UDP for explicitly listed executable names is selected. TCP has kernel PASS rules for IPv4 and IPv6 and remains on its existing route. Other UDP processes retain their existing route.
- LAN destinations and DNS UDP port 53 retain the upstream router's direct-bypass behavior. Fragmented IPv6 datagrams are also passed through. This feature is intended for application/game UDP forwarding, not complete leak prevention.
- Game traffic can use IPv4 or IPv6; the SOCKS5 server must be reachable through IPv4. Only unauthenticated SOCKS5 is supported. HTTP proxies and authenticated SOCKS5 are rejected.
- Passing `UDP ASSOCIATE` proves the listener supports that command, not that the selected upstream node can deliver packets to every game server. That requires a real application test.
- A global named mutex prevents concurrent MAAA engines. MAAA also refuses to start while a ProxiFyre process is running, to avoid competing driver clients.
- No FlClash profile, subscription, node selection, system proxy, or TUN setting is rewritten. Task configuration remains in MAAA's `appsettings.Local.json` and is unaffected by FlClash subscription refreshes.

## Source and build

The native component and its third-party dependencies are under `src/Native/UdpProxy/`. CMake and MSVC v143 build it automatically during the .NET build. The CRT and driver API are statically linked; no ProxiFyre binary, C++/CLI, .NET Framework, or separate VC runtime download is needed.

`dotnet publish` obtains and verifies pinned driver and Mihomo sources, builds the adapted upstream using Go, and includes exact vendored dependencies in its complete corresponding source archive. The adaptation preserves AnyTLS/UoT replies above the original 16 KiB receive limit. Packages include `components/udp/dependencies/`, `components/udp/upstream/`, and `components/udp/source/mihomo-v1.19.34-maaa.1-source.tar.gz`; users do not manually install a proxy service. Publishing requires Go 1.26 or later.

The release archive contains corresponding source under `components/udp/source/`; build it with:

```powershell
cmake -S components/udp/source -B build-udp -G "Visual Studio 17 2022" -A x64
cmake --build build-udp --config Release
ctest --test-dir build-udp -C Release --output-on-failure
```

See [third-party notices](third-party/NOTICE.md) for exact revisions, licenses, and local changes. Driver installation can be removed separately through Windows Installed Apps when it is no longer needed by any application.
