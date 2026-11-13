"""Administrator-only integration check; does not contact a public server.

Run with the native Release directory and a result path as arguments.
"""
import json
import socket
import struct
import subprocess
import sys
import threading
from pathlib import Path


def receive(connection, length):
    result = b""
    while len(result) < length:
        part = connection.recv(length - len(result))
        if not part:
            raise EOFError("SOCKS control closed")
        result += part
    return result


def control(listener, relay):
    while True:
        connection, _ = listener.accept()
        threading.Thread(target=associate, args=(connection, relay), daemon=True).start()


def associate(connection, relay):
    with connection:
        version, count = receive(connection, 2)
        receive(connection, count)
        connection.sendall(b"\x05\x00")
        request = receive(connection, 10)
        if request[:4] != b"\x05\x03\x00\x01":
            return
        connection.sendall(b"\x05\x00\x00\x01\x7f\x00\x00\x01" +
                           struct.pack("!H", relay.getsockname()[1]))
        while connection.recv(1):
            pass


def echo(relay):
    while True:
        data, sender = relay.recvfrom(65535)
        if len(data) != 12:
            continue
        length = struct.unpack("!H", data[10:])[0]
        relay.sendto(data[:10] + bytes(i % 251 for i in range(length)), sender)


binary = Path(sys.argv[1])
result = Path(sys.argv[2])
listener = socket.socket()
listener.bind(("127.0.0.1", 0))
listener.listen()
relay = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
relay.bind(("127.0.0.1", 0))
threading.Thread(target=control, args=(listener, relay), daemon=True).start()
threading.Thread(target=echo, args=(relay,), daemon=True).start()
with result.with_suffix(".log").open("wb") as log:
    engine = subprocess.Popen([str(binary / "MaaaUdpProxy.exe"), "--endpoint",
        f"127.0.0.1:{listener.getsockname()[1]}", "--app", "MaaaUdpRoundtrip.exe", "--diagnostics"],
        stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=log,
        creationflags=subprocess.CREATE_NO_WINDOW)
    try:
        ready = engine.stdout.readline().decode().strip()
        if ready != "MAAA_UDP_READY":
            raise RuntimeError("Engine readiness failed")
        client = subprocess.run([str(binary / "MaaaUdpRoundtrip.exe")], capture_output=True,
                                timeout=50, creationflags=subprocess.CREATE_NO_WINDOW)
        result.write_text(json.dumps({"exitCode": client.returncode,
            "stdout": client.stdout.decode(), "stderr": client.stderr.decode()}), encoding="utf-8")
    finally:
        engine.communicate(b"stop\n", timeout=15)
