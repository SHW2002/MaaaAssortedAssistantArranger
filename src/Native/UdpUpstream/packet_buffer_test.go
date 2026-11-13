// MAAA local adaptation tests; applies to Mihomo common/net/packet.
package packet

import (
	"bytes"
	"encoding/binary"
	"fmt"
	"net"
	"testing"
	"time"

	M "github.com/metacubex/sing/common/metadata"
	"github.com/metacubex/sing/common/uot"
)

func TestMaaaLargeUotDatagrams(t *testing.T) {
	for _, size := range []int{1200, 16383, 16384, 16385, 48000, 65000} {
		t.Run(fmt.Sprint(size), func(t *testing.T) {
			reader, writer := net.Pipe()
			defer reader.Close()
			defer writer.Close()
			_ = reader.SetDeadline(time.Now().Add(5 * time.Second))
			payload := make([]byte, size)
			for i := range payload { payload[i] = byte(i % 251) }
			done := make(chan error, 1)
			go func() {
				if err := binary.Write(writer, binary.BigEndian, uint16(size)); err != nil {
					done <- err; return
				}
				_, err := writer.Write(payload)
				done <- err
			}()
			connection := NewEnhancePacketConn(uot.NewConn(reader, uot.Request{
				IsConnect: true, Destination: M.ParseSocksaddr("203.0.113.1:23301"),
			}))
			data, put, address, err := connection.WaitReadFrom()
			if put != nil { defer put() }
			if err != nil { t.Fatal(err) }
			if !bytes.Equal(data, payload) || address.String() != "203.0.113.1:23301" {
				t.Fatalf("datagram was changed: size=%d received=%d address=%v", size, len(data), address)
			}
			if err := <-done; err != nil { t.Fatal(err) }
		})
	}
}
