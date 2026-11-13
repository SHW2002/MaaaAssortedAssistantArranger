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

MAAA adaptations (2026-11): a new UDP-only native entry point and process-lifetime protocol; explicit executable-name validation; a global engine mutex; kernel TCP PASS rules for both families (`socks_local_router.h`); exact matching for complete `.exe` names (`process_routing_policy.h`); and replacement of Boost's deferred-packet object pool with `std::pmr::synchronized_pool_resource` (`intermediate_buffer_pool.h`). All other vendored files retain their upstream implementation.

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

MAAA downloads this official installer only through its explicit preparation action and never repackages, modifies, or re-signs the kernel driver. Installer license terms govern the separately installed driver. The driver API source vendored with the router retains its original copyright notices.
