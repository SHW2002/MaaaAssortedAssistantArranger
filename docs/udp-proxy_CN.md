# 每任务 UDP 代理

[English](udp-proxy.md) | [简体中文](udp-proxy_CN.md)

MAAA 随包提供 `components/udp/MaaaUdpProxy.exe`，由开源原生转发代码编译。无需安装完整 ProxiFyre、运行其界面或注册其 Windows 服务，也不向游戏注入 DLL。

## 首次使用

1. 完整解压 MAAA 发布包，以管理员身份运行。
2. 展开任务“配置”，勾选“为此任务启用 UDP 代理”。
3. 点击“准备 UDP 组件”。确认后才会下载官方 Windows Packet Filter x64 MSI，校验固定 SHA-256，并在缺少兼容驱动时安装。安装可能短暂重连网卡；如需重启会明确提示，不会自动重启。
4. 保持 SOCKS5 上游运行。使用 FlClash 时填写实际混合或 SOCKS5 监听端口，默认示例为 `127.0.0.1:7890`。
5. 填写完整目标进程文件名，例如 `StarRail.exe`；多个以逗号、分号或换行分隔。同名进程的所有实例都会匹配，不支持目录、通配符或自动继承子进程。
6. 将上方“监控进程”设为任务全程存活的进程，例如直接监控星铁时填写 `StarRail`；超时必须大于 0。开始前先退出已经运行的目标游戏。
7. 使用本机 FlClash 上游时，取消“运行前关闭 FlClash”。存在冲突时，任务链会在执行关闭钩子前报错；启动加载这样的配置时也会跳过关闭 FlClash。

无人值守运行需要在 Windows 任务计划程序中，让 MAAA 使用最高权限运行。只要 SOCKS5 监听正常工作，就不要求打开系统代理。

## 生命周期

MAAA 先校验配置并检查 SOCKS5 `UDP ASSOCIATE`，再启动隐藏的内置组件。只有在监听器、进程关联和过滤器启动完成后收到明确的就绪信号，才会启动任务工具；就绪等待最长 20 秒。

组件随任务运行，等待配置的监控进程出现并退出。UDP 任务使用 500 毫秒轮询，不受普通调度轮询间隔影响。进程退出、超时、工具启动失败、手动停止或关闭 MAAA 时都会关闭组件，清理完成后才会执行下一个任务。停止时优先请求引擎释放过滤器；若无法及时退出，则由 Windows Job Object 终止。父进程管道关闭及 Job Object 同时负责异常退出后的清理。停止任务不会强制结束游戏。

组件中途异常退出时任务会标为失败；组件启动失败时不启动工具。任务结束后保留共享驱动，转发进程不会常驻，也不会注册 Windows 服务。

## 作用范围与边界

- 仅为显式列出的进程选择 UDP 代理。IPv4、IPv6 TCP 都有内核直接放行规则，继续使用原有路径；其他进程 UDP 维持原有路由。
- 保留上游代码对局域网和 UDP 53 端口 DNS 的直接放行。IPv6 分片数据报同样放行。因此这项功能用于游戏 UDP 转发，不是完整防泄漏机制。
- 支持游戏 IPv4、IPv6 目标；SOCKS5 上游需可通过 IPv4 到达，且无需认证。不支持 HTTP 代理或需要密码认证的 SOCKS5。
- `UDP ASSOCIATE` 检查只证明监听器支持该命令，不代表当前节点能访问所有游戏服务器，实际送达仍需用游戏验证。
- 全局命名互斥量防止多个 MAAA 转发引擎并行运行；检测到 ProxiFyre 进程运行时也会拒绝启动，避免争用驱动。
- 不写入 FlClash 配置、订阅、节点、系统代理或 TUN 设置。任务选项保存在 MAAA 的 `appsettings.Local.json`，不会被 FlClash 订阅更新覆盖。

## 源码与构建

原生组件及固定版本依赖位于 `src/Native/UdpProxy/`，由 CMake、MSVC v143 在 .NET 构建时自动编译。CRT 和驱动 API 静态链接，无需 ProxiFyre 二进制、C++/CLI、.NET Framework 或额外下载 VC 运行库。

发布包内的 `components/udp/source/` 包含对应源码和构建文件，可独立执行：

```powershell
cmake -S components/udp/source -B build-udp -G "Visual Studio 17 2022" -A x64
cmake --build build-udp --config Release
ctest --test-dir build-udp -C Release --output-on-failure
```

确切来源、许可证及本地修改见[第三方说明](third-party/NOTICE.md)。若所有程序均不再需要 Windows Packet Filter，可通过 Windows“已安装的应用”单独卸载该共享驱动。
