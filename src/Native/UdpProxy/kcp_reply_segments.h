// SPDX-License-Identifier: AGPL-3.0-only
#pragma once

namespace maaa
{
    // Validate the entire token-bearing KCP batch before restoring segment
    // boundaries. Only the explicitly matched StarRail.exe owner uses this path.
    inline std::vector<std::pair<size_t, size_t>> starrail_kcp_segments(
        const uint8_t* data, const size_t size)
    {
        constexpr size_t header = 28, mtu = 1200;
        std::vector<std::pair<size_t, size_t>> segments;
        if (size <= mtu || size > 65507) return segments;
        for (size_t offset = 0; offset < size;)
        {
            if (size - offset < header || memcmp(data, data + offset, 8)) return {};
            const auto command = data[offset + 8];
            if (command < 81 || command > 84) return {};
            uint32_t payload = 0;
            for (size_t i = 0; i < 4; ++i) payload |= uint32_t(data[offset + 24 + i]) << (i * 8);
            if (payload > mtu - header || payload > size - offset - header) return {};
            segments.emplace_back(offset, header + payload);
            offset += header + payload;
        }
        return segments;
    }
}
