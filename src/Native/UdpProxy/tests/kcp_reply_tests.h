// SPDX-License-Identifier: AGPL-3.0-only
#pragma once

inline void test_kcp_replies(const INTERMEDIATE_BUFFER& original)
{
    std::vector<uint8_t> segment(1200);
    segment[0] = 17; segment[4] = 23; segment[8] = 81;
    segment[24] = 0x94; segment[25] = 0x04;
    std::vector<uint8_t> framed{0, 0, 0, 1, 203, 0, 113, 8, 0x5b, 0x05};
    for (size_t i = 0; i < 10; ++i) framed.insert(framed.end(), segment.begin(), segment.end());
    const auto parsed = maaa::starrail_kcp_segments(framed.data() + 10, framed.size() - 10);
    require(parsed.size() == 10);
    for (size_t i = 0; i < parsed.size(); ++i)
        require(parsed[i].first == i * 1200 && parsed[i].second == 1200);
    require(maaa::starrail_kcp_segments(segment.data(), segment.size()).empty());
    auto invalid = framed;
    invalid[1210] ^= 1;
    require(maaa::starrail_kcp_segments(invalid.data() + 10, invalid.size() - 10).empty());
    invalid = framed; invalid[1218] = 0;
    require(maaa::starrail_kcp_segments(invalid.data() + 10, invalid.size() - 10).empty());
    invalid = framed; invalid[1234] = 0xff; invalid[1235] = 0xff;
    require(maaa::starrail_kcp_segments(invalid.data() + 10, invalid.size() - 10).empty());
    require(maaa::starrail_kcp_segments(framed.data() + 10, framed.size() - 11).empty());
    maaa::udp_reply_injector injector;
    injector.remember(original, true);
    size_t sent = 0;
    const auto unexpected_fragment = [](auto&) { require(false); return false; };
    require(injector.deliver(23456, framed.data(), framed.size(), unexpected_fragment,
        [&](const uint8_t* data, size_t size)
        {
            require(size == 1210 && memcmp(data, framed.data(), 10) == 0);
            require(memcmp(data + 10, segment.data(), segment.size()) == 0);
            ++sent;
            return true;
        }));
    require(sent == 10);
    // Other games retain their complete datagram; only matched StarRail owners split KCP batches.
    injector.remember(original, false);
    size_t fragments = 0;
    require(injector.deliver(23456, framed.data(), framed.size(),
        [&](auto&) { ++fragments; return true; },
        [](auto*, size_t) { require(false); return false; }));
    require(fragments > 1);
}
