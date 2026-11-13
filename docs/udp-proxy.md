# Per-task UDP proxy

[English](udp-proxy.md) | [简体中文](udp-proxy_CN.md)

MAAA ships `components/udp/MaaaUdpProxy.exe`, compiled from the vendored open-source native router. It does not require an installed ProxiFyre application, GUI, or Windows service. No game DLL injection is used.

## First use

1. Extract the complete MAAA package, then run MAAA as administrator.
2. Open a task's configuration and enable **为此任务启用 UDP 代理**.
3. Click **准备 UDP 组件**. After your confirmation, MAAA downloads the official Windows Packet Filter x64 MSI, verifies its pinned SHA-256, and installs it if a compatible driver is absent. Adapter installation can briefly reconnect the network; a required reboot is reported, never forced.
4. Keep a SOCKS5 upstream running. For FlClash, enter its actual mixed/SOCKS5 listening host and port; the example `127.0.0.1:7890` is only a default.
5. Enter exact target executable filenames, for example `StarRail.exe`. Separate several names with commas, semicolons, or newlines. Matching applies to every instance with that exact filename; directories, wildcards, and automatic child-process inheritance are unsupported.
6. Set the task's monitored process to a process that stays alive for the whole operation (`StarRail` for directly monitoring the game), and set a positive timeout. Close existing target processes before starting.
7. When using a local FlClash upstream, disable **运行前关闭 FlClash**. Conflicting task chains are rejected before the close hook; loading such a configuration also skips the startup close.

For unattended use, configure the existing Windows scheduled task to run MAAA with highest privileges. The system proxy itself need not be enabled if the configured SOCKS5 listener remains available.

## Task lifecycle

MAAA validates the settings and checks SOCKS5 `UDP ASSOCIATE` support. It launches its hidden component, waits up to 20 seconds for an explicit readiness acknowledgement after the listeners, process associations, and filter have started, and only then launches the task tool.

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

The release archive contains corresponding source under `components/udp/source/`; build it with:

```powershell
cmake -S components/udp/source -B build-udp -G "Visual Studio 17 2022" -A x64
cmake --build build-udp --config Release
ctest --test-dir build-udp -C Release --output-on-failure
```

See [third-party notices](third-party/NOTICE.md) for exact revisions, licenses, and local changes. Driver installation can be removed separately through Windows Installed Apps when it is no longer needed by any application.
