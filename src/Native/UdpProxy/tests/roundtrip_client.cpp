// SPDX-License-Identifier: AGPL-3.0-only
// Manual integration test: use a loopback SOCKS5 echo relay and target only this executable.
#include <WinSock2.h>
#include <ws2tcpip.h>
#include <iostream>
#include <vector>
#include <cstring>

int main()
{
    WSADATA data{};
    if (WSAStartup(MAKEWORD(2, 2), &data)) return 1;
    const auto socket = ::socket(AF_INET, SOCK_DGRAM, 0);
    DWORD timeout = 4000;
    setsockopt(socket, SOL_SOCKET, SO_RCVTIMEO, reinterpret_cast<char*>(&timeout), sizeof(timeout));
    sockaddr_in peer{};
    peer.sin_family = AF_INET;
    peer.sin_port = htons(23301);
    inet_pton(AF_INET, "203.0.113.1", &peer.sin_addr);
    if (connect(socket, reinterpret_cast<sockaddr*>(&peer), sizeof(peer))) return 4;
    for (const int length : {20, 1100, 1190, 1200, 1470, 8000, 12173, 65000})
    {
        // A small request asks the synthetic relay to return a deterministic larger reply.
        const auto request = htons(static_cast<unsigned short>(length));
        if (sendto(socket, reinterpret_cast<const char*>(&request), 2, 0,
            reinterpret_cast<sockaddr*>(&peer), sizeof(peer)) != 2) return 2;
        std::vector<char> reply(65535);
        sockaddr_in source{};
        int address_size = sizeof(source);
        const auto received = recvfrom(socket, reply.data(), static_cast<int>(reply.size()), 0,
            reinterpret_cast<sockaddr*>(&source), &address_size);
        const bool valid = received == length && source.sin_addr.s_addr == peer.sin_addr.s_addr &&
            source.sin_port == peer.sin_port;
        bool payload_valid = valid;
        for (int i = 0; payload_valid && i < received; ++i)
            payload_valid = static_cast<unsigned char>(reply[i]) == i % 251;
        std::cout << length << ": received=" << received << " valid=" << payload_valid <<
            " error=" << (received < 0 ? WSAGetLastError() : 0) << std::endl;
        if (!payload_valid) return 3;
    }
    closesocket(socket);
    WSACleanup();
    return 0;
}
