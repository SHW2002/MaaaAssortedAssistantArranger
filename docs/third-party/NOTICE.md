# UDP component third-party notices

`MaaaUdpProxy.exe` is an independently built native child component. The WPF application communicates with it through command-line arguments and redirected standard input/output; it does not load a ProxiFyre managed or native assembly.

## ProxiFyre native networking code

- Upstream: https://github.com/wiresock/proxifyre
- Version: v2.6.4
- Commit: `5e3bc845d73a47a0931b1233c745580cb916f4ba`
- Source subset: `include/`, `ndisapi/`, `netlib/src/`
- License: GNU Affero General Public License v3.0; see [full license](ProxiFyre-LICENSE.txt), also retained beside the vendored source.
- Original authors include Vadim Smirnov / NT KERNEL; upstream copyright notices are retained.

The native component is distributed under AGPL-3.0. Its corresponding source and build files, including all local adaptations and vendored dependencies, accompany the binary under `components/udp/source/`. This directory can be built directly with CMake and MSVC; it does not require a ProxiFyre release binary or downloads of source dependencies.

MAAA adaptations (2026-11): a UDP-only native entry point and process-lifetime protocol; executable-name validation; a global engine mutex; kernel TCP PASS rules for both families; exact complete-name matching; replacement of Boost's deferred-packet object pool with `std::pmr::synchronized_pool_resource`; and an IPv4 reply callback, bounded client-template cache, and SOCKS-free fragment injection for large replies. Modified router/socket files retain their original upstream notices. Corresponding tests and all modifications accompany the binary.

## Microsoft Guidelines Support Library

- Upstream: https://github.com/microsoft/GSL
- Version: v4.0.0
- Commit: `a3534567187d2edc428efd3f13466ff75fe5805c`
- Copyright: Microsoft Corporation
- License: MIT; see [full license](GSL-LICENSE.txt), also retained beside the vendored headers.

## Windows Packet Filter

- Upstream: https://github.com/wiresock/ndisapi
- Official installer: `Windows.Packet.Filter.3.6.2.1.x64.msi`, release `v3.6.2`
- SHA-256: `9c388c0b7f189f7fa98720bae2caecf7d64f30910838b80b438ecf8956b8502c`
- Download: https://github.com/wiresock/ndisapi/releases/download/v3.6.2/Windows.Packet.Filter.3.6.2.1.x64.msi

The original official MSI is redistributed unchanged in `components/udp/dependencies/`, verified before unattended installation when a task needs it, and downloaded automatically only if absent. MAAA does not modify or re-sign the kernel driver. Installer license terms govern the separately installed driver. The driver API source retains its original notices.

## Mihomo

- Upstream: https://github.com/MetaCubeX/mihomo
- Version: v1.19.34 with MAAA adaptation `maaa.1`, Windows amd64
- License: GPL-3.0; see [full license](Mihomo-LICENSE.txt).
- Built from verified source as `components/udp/upstream/mihomo.exe`; this is a modified build, not the original upstream binary.
- Local change: `common/net/packet/packet_sing.go` sets read MTU to 65535, preventing large AnyTLS/UoT datagrams from terminating the session with `UoT read: short buffer`. Regression tests cover the 16 KiB boundary and 65 KB replies.
- Complete corresponding source: `components/udp/source/mihomo-v1.19.34-maaa.1-source.tar.gz`, including the adaptation, tests, exact vendored Go dependencies, licenses, and `maaa/README.md` rebuild instructions.
- Source URL: https://codeload.github.com/MetaCubeX/mihomo/tar.gz/refs/tags/v1.19.34
- Original source archive SHA-256: `e3284a44f1052f1c454c90d418faa244516c2470bf232b455ea18fc4f5fa6028`.
- Compiler version and resulting binary/source hashes: `components/udp/upstream/build-info.json`.

MAAA starts this independent child process with a task-owned local configuration. No system proxy, TUN, or FlClash configuration is changed.

The native component also validates and restores segment boundaries in large token-bearing KCP batches only for an explicitly matched `StarRail.exe`, before returning them through its ordinary local UDP send path. Unknown/invalid payloads retain their datagram boundary.

## YamlDotNet

- Upstream: https://github.com/aaubry/YamlDotNet
- Version: 16.3.0; source commit `ae480660f4fb26f3eb0b41c1d1fcf21c0e9d9e73`.
- License: MIT; see [full license](YamlDotNet-LICENSE.txt).
- Used to parse the existing local FlClash YAML configuration without modifying it.
