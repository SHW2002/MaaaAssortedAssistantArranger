// SPDX-License-Identifier: AGPL-3.0-only
#include "../engine_includes.h"

void require(bool condition)
{
    if (!condition) throw std::runtime_error("UDP reply fragment test failed");
}
#include "kcp_reply_tests.h"

int main()
{
    INTERMEDIATE_BUFFER original{};
    original.m_Length = 42;
    original.m_hAdapter = reinterpret_cast<HANDLE>(123);
    auto* eth = reinterpret_cast<ether_header*>(original.m_IBuffer);
    eth->h_proto = htons(ETH_P_IP);
    eth->h_source[0] = 1;
    eth->h_dest[0] = 2;
    auto* ip = reinterpret_cast<iphdr*>(eth + 1);
    ip->ip_v = 4;
    ip->ip_hl = 5;
    ip->ip_p = IPPROTO_UDP;
    ip->ip_src.S_un.S_addr = htonl(0xc0000201);
    auto* udp = reinterpret_cast<udphdr*>(ip + 1);
    udp->th_sport = htons(23456);
    test_kcp_replies(original);
    for (const size_t payload : {0, 1, 1200, 1256, 1257, 12173, 65507})
    {
        std::vector<uint8_t> reply(10 + payload);
        reply[3] = 1;
        reply[4] = 203; reply[5] = 0; reply[6] = 113; reply[7] = 8;
        reply[8] = 0x5b; reply[9] = 0x05;
        for (size_t i = 0; i < payload; ++i) reply[10 + i] = i % 251;
        std::vector<uint8_t> reassembled;
        require(maaa::inject_udp_v4_reply(original, reply.data(), reply.size(), 4321,
            [&](const INTERMEDIATE_BUFFER& fragment)
            {
                const auto* ethernet = reinterpret_cast<const ether_header*>(fragment.m_IBuffer);
                const auto* header = reinterpret_cast<const iphdr*>(ethernet + 1);
                const auto length = ntohs(header->ip_len) - sizeof(iphdr);
                const auto flags = ntohs(header->ip_off);
                require(fragment.m_hAdapter == original.m_hAdapter);
                require(fragment.m_dwDeviceFlags == PACKET_FLAG_ON_RECEIVE);
                require(ethernet->h_dest[0] == 1 && ethernet->h_source[0] == 2);
                require(fragment.m_Length == ETHER_HEADER_LENGTH + ntohs(header->ip_len));
                require(ntohs(header->ip_len) <= 1280 && ntohs(header->ip_id) == 4321);
                require(header->ip_dst.S_un.S_addr == ip->ip_src.S_un.S_addr);
                require(memcmp(&header->ip_src, reply.data() + 4, 4) == 0);
                const auto* bytes = reinterpret_cast<const uint8_t*>(header);
                unsigned int sum = 0;
                for (size_t i = 0; i < sizeof(iphdr); i += 2) sum += (bytes[i] << 8) | bytes[i + 1];
                while (sum >> 16) sum = (sum & 65535) + (sum >> 16);
                require(sum == 65535);
                require((flags & 0x1fff) * 8 == reassembled.size());
                if (flags & IP_MF) require(length % 8 == 0);
                else require(reassembled.size() + length == sizeof(udphdr) + payload);
                reassembled.insert(reassembled.end(), bytes + sizeof(iphdr), bytes + sizeof(iphdr) + length);
                return true;
            }));
        const auto* result = reinterpret_cast<const udphdr*>(reassembled.data());
        require(result->th_dport == udp->th_sport && ntohs(result->th_sport) == 23301);
        require(ntohs(result->length) == sizeof(udphdr) + payload);
        unsigned int udp_sum = IPPROTO_UDP + reassembled.size();
        const auto add_words = [&udp_sum](const uint8_t* bytes, size_t length)
        {
            for (size_t i = 0; i < length; i += 2)
                udp_sum += (unsigned(bytes[i]) << 8) | (i + 1 < length ? bytes[i + 1] : 0);
        };
        add_words(reply.data() + 4, 4);
        add_words(reinterpret_cast<const uint8_t*>(&ip->ip_src), 4);
        add_words(reassembled.data(), reassembled.size());
        while (udp_sum >> 16) udp_sum = (udp_sum & 65535) + (udp_sum >> 16);
        require(udp_sum == 65535 && result->th_sum != 0);
        require(std::equal(reply.begin() + 10, reply.end(), reassembled.begin() + sizeof(udphdr)));
    }
    std::vector<uint8_t> reply(1300);
    reply[3] = 1;
    const auto reject_emit = [](auto&) { throw std::runtime_error("Malformed reply was emitted"); return true; };
    require(!maaa::inject_udp_v4_reply(original, reply.data(), 9, 0, reject_emit));
    reply[2] = 1;
    require(!maaa::inject_udp_v4_reply(original, reply.data(), reply.size(), 0, reject_emit));
    reply[2] = 0; reply[3] = 4;
    require(!maaa::inject_udp_v4_reply(original, reply.data(), reply.size(), 0, reject_emit));
    reply[3] = 1;
    require(!maaa::inject_udp_v4_reply(original, reply.data(), 65518, 0, reject_emit));
    require(!maaa::inject_udp_v4_reply(original, reply.data(), reply.size(), 0, [](auto&) { return false; }));
    original.m_Length = 41;
    require(!maaa::inject_udp_v4_reply(original, reply.data(), reply.size(), 0, reject_emit));
}
