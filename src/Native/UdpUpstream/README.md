# MAAA task UDP upstream adaptation

[English](README.md) | [简体中文](README_CN.md)

This directory describes MAAA's adaptation to Mihomo v1.19.34 (GPL-3.0).
`common/net/packet/packet_sing.go` uses `N.ReadWaitOptions{MTU: 65535}` instead of an empty option set.
The original 16 KiB buffer closes AnyTLS/UoT sessions on larger UDP replies. The local test sends
deterministic packets across that boundary and up to 65 KB through the actual UoT read path.

The release includes complete modified source and exact vendored Go dependencies in
`components/udp/source/mihomo-v1.19.34-maaa.1-source.tar.gz`. Extract it and run from its root with Go 1.26+:

```powershell
$env:CGO_ENABLED='0'
go test -mod=vendor -tags with_gvisor ./common/net/packet -run TestMaaaLargeUotDatagrams -count=1
go build -mod=vendor -trimpath -buildvcs=false -tags with_gvisor -ldflags '-s -w -X github.com/metacubex/mihomo/constant.Version=v1.19.34-maaa.1' -o mihomo.exe .
```

MAAA's publishing script downloads only pinned, hashed source, applies the one-line adaptation,
copies the regression test, builds, and packages corresponding source. `upstream/build-info.json`
records compiler and resulting hashes. Runtime users do not need Go or install Mihomo separately.
