// SPDX-License-Identifier: AGPL-3.0-only
#include "engine_includes.h"
#include "options.h"

namespace
{
    struct winsock_scope
    {
        winsock_scope()
        {
            WSADATA data{};
            if (WSAStartup(MAKEWORD(2, 2), &data) != 0) throw std::runtime_error("WSAStartup failed");
        }
        ~winsock_scope() { WSACleanup(); }
    };

    struct exclusive_engine
    {
        HANDLE mutex = CreateMutexW(nullptr, FALSE, L"Global\\MAAA.UdpProxy.Engine");
        exclusive_engine()
        {
            if (!mutex) throw std::runtime_error("Cannot open the UDP engine lock");
            const auto status = WaitForSingleObject(mutex, 0);
            if (status != WAIT_OBJECT_0 && status != WAIT_ABANDONED)
            {
                CloseHandle(mutex);
                mutex = nullptr;
                throw std::runtime_error("Another MAAA UDP engine is already running");
            }
        }
        ~exclusive_engine() { ReleaseMutex(mutex); CloseHandle(mutex); }
    };

    bool is_administrator()
    {
        SID_IDENTIFIER_AUTHORITY authority = SECURITY_NT_AUTHORITY;
        PSID sid = nullptr;
        if (!AllocateAndInitializeSid(&authority, 2, SECURITY_BUILTIN_DOMAIN_RID,
            DOMAIN_ALIAS_RID_ADMINS, 0, 0, 0, 0, 0, 0, &sid)) return false;
        BOOL member = FALSE;
        CheckTokenMembership(nullptr, sid, &member);
        FreeSid(sid);
        return member != FALSE;
    }
}

int wmain(int argc, wchar_t** argv)
{
    try
    {
        const auto options = maaa::parse_options(argc, argv);
        if (options.validate_only)
        {
            std::cout << "MAAA_UDP_VALIDATED" << std::endl;
            return 0;
        }
        if (!is_administrator()) throw std::runtime_error("Administrator privileges are required");
        exclusive_engine lock;
        winsock_scope winsock;
        auto diagnostic = std::shared_ptr<std::ostream>(&std::cerr, [](std::ostream*) {});
        std::shared_ptr<std::ofstream> capture;
        if (!options.capture_path.empty())
        {
            capture = std::make_shared<std::ofstream>(options.capture_path, std::ios::binary);
            if (!*capture) throw std::runtime_error("Cannot open diagnostic capture");
        }
        proxy::socks_local_router router(options.diagnostics ? netlib::log::log_level::debug :
            netlib::log::log_level::error, diagnostic, capture, true);
        if (!router.set_udp_only()) throw std::runtime_error("Failed to configure TCP pass-through filters");
        router.set_bypass_lan();
        // UDP is fixed here; neither task settings nor arguments can enable TCP interception.
        const auto proxy_id = router.add_socks5_proxy(options.endpoint,
            proxy::socks_local_router::supported_protocols::udp, std::nullopt,
            proxy::socks_local_router::supported_address_families::all);
        if (!proxy_id) throw std::runtime_error("Failed to create SOCKS5 UDP listeners");
        for (const auto& application : options.applications)
            if (!router.associate_process_name_to_proxy(application, *proxy_id))
                throw std::runtime_error("Failed to associate a target process");
        if (!router.start()) throw std::runtime_error("Failed to activate the Windows Packet Filter driver");
        std::cout << "MAAA_UDP_READY" << std::endl;
        // EOF also stops the engine when the parent dies or closes its pipe.
        std::string command;
        while (std::getline(std::cin, command) && command != "stop") { }
        if (!router.stop()) throw std::runtime_error("UDP router shutdown failed");
        std::cout << "MAAA_UDP_STOPPED" << std::endl;
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << "MAAA UDP: " << error.what() << std::endl;
        return 1;
    }
}
