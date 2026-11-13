// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include <cwctype>
#include <stdexcept>
#include <string>
#include <vector>

namespace maaa
{
    inline bool valid_process_name(const std::wstring& name)
    {
        if (name.size() <= 4 || name.size() > 255 ||
            name.find_first_of(L"\\/:*?\"<>|\r\n\t,;") != std::wstring::npos ||
            std::iswspace(name.front()) || std::iswspace(name.back())) return false;
        auto upper = name;
        for (auto& character : upper) character = static_cast<wchar_t>(std::towupper(character));
        return upper.ends_with(L".EXE") && upper != L"MAAAUDPPROXY.EXE" &&
            upper != L"MAAAASSORTEDASSISTANTARRANGER.EXE";
    }

    struct options
    {
        std::string endpoint;
        std::vector<std::wstring> applications;
        bool validate_only = false;
    };

    inline options parse_options(int argc, wchar_t** argv)
    {
        options result;
        for (int index = 1; index < argc; ++index)
        {
            const std::wstring argument = argv[index];
            if (argument == L"--validate") { result.validate_only = true; continue; }
            if (++index >= argc) throw std::invalid_argument("Missing option value");
            const std::wstring value = argv[index];
            if (argument == L"--app")
            {
                if (!valid_process_name(value)) throw std::invalid_argument("Expected an exact .exe filename");
                result.applications.push_back(value);
            }
            else if (argument == L"--endpoint")
            {
                if (!result.endpoint.empty()) throw std::invalid_argument("Duplicate endpoint");
                for (const auto character : value)
                {
                    if (character < 33 || character > 126) throw std::invalid_argument("Invalid endpoint");
                    result.endpoint += static_cast<char>(character);
                }
            }
            else throw std::invalid_argument("Unknown option");
        }
        const auto colon = result.endpoint.find(':');
        if (result.applications.empty() || colon == std::string::npos || colon == 0 ||
            result.endpoint.find(':', colon + 1) != std::string::npos)
            throw std::invalid_argument("Explicit process names and an IPv4/hostname endpoint are required");
        const auto port = result.endpoint.substr(colon + 1);
        if (port.empty() || port.find_first_not_of("0123456789") != std::string::npos ||
            std::stoul(port) == 0 || std::stoul(port) > 65535)
            throw std::invalid_argument("Invalid SOCKS5 port");
        return result;
    }
}
