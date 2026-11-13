// SPDX-License-Identifier: AGPL-3.0-only
#pragma once
#include "udp_reply_fragments.h"
#include "kcp_reply_segments.h"

namespace maaa
{
    using udp_local_reply = std::function<bool(const uint8_t*, size_t)>;
    using udp_reply_handler = std::function<bool(uint16_t, const uint8_t*, size_t, const udp_local_reply&)>;
    class udp_reply_injector
    {
        struct entry
        {
            INTERMEDIATE_BUFFER packet;
            std::chrono::steady_clock::time_point touched;
            bool starrail;
        };
        std::mutex mutex_;
        std::unordered_map<uint16_t, entry> clients_;
        std::atomic<uint16_t> identification_{static_cast<uint16_t>(GetTickCount())};

    public:
        void remember(const INTERMEDIATE_BUFFER& packet, const bool starrail)
        {
            const auto* ip = reinterpret_cast<const iphdr*>(packet.m_IBuffer + ETHER_HEADER_LENGTH);
            const auto* udp = reinterpret_cast<const udphdr*>(
                reinterpret_cast<const uint8_t*>(ip) + ip->ip_hl * 4);
            const auto now = std::chrono::steady_clock::now();
            std::scoped_lock lock(mutex_);
            std::erase_if(clients_, [now](const auto& pair) {
                return now - pair.second.touched > std::chrono::seconds(120);
            });
            const auto port = ntohs(udp->th_sport);
            if (clients_.size() >= 4096 && !clients_.contains(port)) return;
            clients_.insert_or_assign(port, entry{packet, now, starrail});
        }

        template <typename Emit>
        bool deliver(const uint16_t port, const uint8_t* data, const size_t size, Emit&& emit,
            const udp_local_reply& send_local)
        {
            // Smaller replies keep the existing redirect path. Large replies must
            // reach the stack as original UDP fragments, without SOCKS framing.
            if (size <= 1200) return false;
            INTERMEDIATE_BUFFER original{};
            bool starrail = false;
            {
                std::scoped_lock lock(mutex_);
                const auto it = clients_.find(port);
                if (it == clients_.end() || std::chrono::steady_clock::now() - it->second.touched >
                    std::chrono::seconds(120)) return true;
                original = it->second.packet;
                starrail = it->second.starrail;
            }
            if (starrail && size > 1210 && data[0] == 0 && data[1] == 0 && data[2] == 0 && data[3] == 1)
            {
                const auto segments = starrail_kcp_segments(data + 10, size - 10);
                if (!segments.empty())
                {
                    for (const auto& [offset, length] : segments)
                    {
                        std::vector<uint8_t> framed(data, data + 10);
                        framed.insert(framed.end(), data + 10 + offset, data + 10 + offset + length);
                        if (!send_local(framed.data(), framed.size()))
                        {
                            std::cerr << "MAAA UDP: KCP reply delivery failed" << std::endl;
                            break;
                        }
                    }
                    return true;
                }
            }
            const bool result = inject_udp_v4_reply(original, data, size,
                identification_.fetch_add(1), std::forward<Emit>(emit));
            if (!result) std::cerr << "MAAA UDP: large IPv4 reply delivery failed" << std::endl;
            return true; // Never send an unhandled oversized reply onto the public NIC.
        }
    };
}
