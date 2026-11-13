// SPDX-License-Identifier: AGPL-3.0-only
#include "../engine_includes.h"
#include "../options.h"

int main()
{
    // Do not use assert(): Release builds must execute these checks too.
    const auto require = [](bool condition) { if (!condition) throw std::runtime_error("UDP policy test failed"); };
    require(maaa::valid_process_name(L"StarRail.exe"));
    for (const auto& name : {L"", L"*", L"*.exe", L"C:\\Games\\StarRail.exe", L".exe", L"MaaaUdpProxy.exe"})
        require(!maaa::valid_process_name(name));
    require(proxy::should_bypass_unresolved_process(true, false));
    require(!proxy::should_bypass_unresolved_process(true, true));
    const std::multimap<size_t, std::wstring> names{{0, L"STARRAIL.EXE"}};
    const std::vector<std::wstring> excluded{};
    const proxy::owner_match_rules rules{names, excluded, true, GetCurrentProcessId()};
    const auto udp_target = [](size_t) { return proxy::proxy_route_target{true, true, 1234}; };
    const auto tcp_target = [](size_t) { return proxy::proxy_route_target{false, true, std::nullopt}; };
    iphelper::network_process game(42, L"STARRAIL.EXE", L"C:\\GAME\\STARRAIL.EXE");
    require(proxy::select_proxy_port(rules, game, udp_target).action == proxy::proxy_port_action::proxy);
    require(proxy::select_proxy_port(rules, game, tcp_target).action == proxy::proxy_port_action::none);
    for (const auto& name : {L"CODEX.EXE", L"GIT.EXE", L"FLCLASH.EXE", L"EVILSTARRAIL.EXE", L"STARRAIL.EXE.BAK"})
    {
        iphelper::network_process other(43, name, L"C:\\OTHER");
        require(proxy::select_proxy_port(rules, other, udp_target).action == proxy::proxy_port_action::none);
    }
    return 0;
}
