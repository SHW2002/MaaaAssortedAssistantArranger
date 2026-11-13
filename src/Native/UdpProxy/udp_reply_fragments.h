// SPDX-License-Identifier: AGPL-3.0-only
#pragma once

namespace maaa
{
    // Strip SOCKS framing before fragmentation. Removing its ten bytes from an
    // already fragmented IPv4 packet cannot preserve eight-byte fragment offsets.
    template <typename Emit>
    bool inject_udp_v4_reply(const INTERMEDIATE_BUFFER& original, const uint8_t* reply,
        const size_t size, const uint16_t identification, Emit&& emit)
    {
        constexpr size_t socks_size = 10;
        if (size < socks_size || size > 65507 + socks_size || reply[0] || reply[1] ||
            reply[2] || reply[3] != 1 || original.m_Length < 42) return false;
        const auto* original_ip = reinterpret_cast<const iphdr*>(original.m_IBuffer + ETHER_HEADER_LENGTH);
        if (original_ip->ip_v != 4 || original_ip->ip_hl < 5 ||
            original.m_Length < ETHER_HEADER_LENGTH + original_ip->ip_hl * 4 + sizeof(udphdr)) return false;
        const auto* original_udp = reinterpret_cast<const udphdr*>(
            reinterpret_cast<const uint8_t*>(original_ip) + original_ip->ip_hl * 4);
        std::vector<uint8_t> datagram(sizeof(udphdr) + size - socks_size);
        auto* udp = reinterpret_cast<udphdr*>(datagram.data());
        memcpy(&udp->th_sport, reply + 8, 2);
        udp->th_dport = original_udp->th_sport;
        udp->length = htons(static_cast<uint16_t>(datagram.size()));
        udp->th_sum = 0;
        memcpy(datagram.data() + sizeof(udphdr), reply + socks_size, size - socks_size);
        uint32_t checksum = IPPROTO_UDP + static_cast<uint32_t>(datagram.size());
        const auto sum_words = [&checksum](const uint8_t* bytes, const size_t length)
        {
            for (size_t i = 0; i < length; i += 2)
                checksum += (uint32_t(bytes[i]) << 8) | (i + 1 < length ? bytes[i + 1] : 0);
        };
        sum_words(reply + 4, 4);
        sum_words(reinterpret_cast<const uint8_t*>(&original_ip->ip_src), 4);
        sum_words(datagram.data(), datagram.size());
        while (checksum >> 16) checksum = (checksum & 0xffff) + (checksum >> 16);
        udp->th_sum = htons(static_cast<uint16_t>(~checksum) == 0 ? 0xffff : static_cast<uint16_t>(~checksum));
        constexpr size_t fragment_payload = ((1280 - sizeof(iphdr)) / 8) * 8;
        for (size_t offset = 0; offset < datagram.size(); offset += fragment_payload)
        {
            const auto length = std::min(fragment_payload, datagram.size() - offset);
            INTERMEDIATE_BUFFER packet{};
            packet.m_hAdapter = original.m_hAdapter;
            packet.m_8021q = original.m_8021q;
            packet.m_dwDeviceFlags = PACKET_FLAG_ON_RECEIVE;
            packet.m_Length = static_cast<DWORD>(ETHER_HEADER_LENGTH + sizeof(iphdr) + length);
            memcpy(packet.m_IBuffer, original.m_IBuffer, ETHER_HEADER_LENGTH);
            auto* ethernet = reinterpret_cast<ether_header*>(packet.m_IBuffer);
            std::swap(ethernet->h_source, ethernet->h_dest);
            auto* ip = reinterpret_cast<iphdr*>(ethernet + 1);
            ip->ip_v = 4;
            ip->ip_hl = 5;
            ip->ip_ttl = 64;
            ip->ip_p = IPPROTO_UDP;
            memcpy(&ip->ip_src, reply + 4, 4);
            ip->ip_dst = original_ip->ip_src;
            ip->ip_id = htons(identification);
            ip->ip_len = htons(static_cast<uint16_t>(sizeof(iphdr) + length));
            ip->ip_off = htons(static_cast<uint16_t>(offset / 8) |
                (offset + length < datagram.size() ? IP_MF : 0));
            memcpy(ip + 1, datagram.data() + offset, length);
            CNdisApi::RecalculateIPChecksum(&packet);
            if (!emit(packet)) return false;
        }
        return true;
    }
}
