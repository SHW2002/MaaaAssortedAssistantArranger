# MAAA 任务 UDP 上游修正

[English](README.md) | [简体中文](README_CN.md)

基于 Mihomo v1.19.34（GPL-3.0），将 `common/net/packet/packet_sing.go` 中的空读取选项
改为 `N.ReadWaitOptions{MTU: 65535}`，避免 AnyTLS/UoT 收到超过原先 16 KiB 的 UDP 数据报时中断会话。
回归测试通过实际 UoT 读取路径覆盖 16 KiB 边界及最高 65 KB 回包，并逐字节检查内容。

发布包的 `components/udp/source/mihomo-v1.19.34-maaa.1-source.tar.gz` 包含完整修正版源码、
精确版本的 Go 依赖源码及许可证。解压后在其根目录使用 Go 1.26+ 执行：

```powershell
$env:CGO_ENABLED='0'
go test -mod=vendor -tags with_gvisor ./common/net/packet -run TestMaaaLargeUotDatagrams -count=1
go build -mod=vendor -trimpath -buildvcs=false -tags with_gvisor -ldflags '-s -w -X github.com/metacubex/mihomo/constant.Version=v1.19.34-maaa.1' -o mihomo.exe .
```

MAAA 的发布脚本校验固定源码哈希后应用这一行修正，复制测试、编译并打包完整对应源码。
编译器版本及结果哈希见 `upstream/build-info.json`。运行发布包不需要 Go，也无需另行安装 Mihomo。
