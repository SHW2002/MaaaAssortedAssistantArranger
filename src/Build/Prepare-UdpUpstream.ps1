param([Parameter(Mandatory=$true)][string]$ProjectRoot,
      [Parameter(Mandatory=$true)][string]$SourceArchive)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Utility\Microsoft.PowerShell.Utility.psd1') -ErrorAction Stop
$buildRoot = [IO.Path]::GetFullPath((Join-Path $ProjectRoot 'artifacts\udp-dependencies\upstream-build'))
$source = Join-Path $buildRoot 'mihomo-1.19.34'
$output = Join-Path $ProjectRoot 'artifacts\udp-dependencies\mihomo\mihomo-maaa.exe'
New-Item -ItemType Directory -Path $buildRoot,(Split-Path $output) -Force | Out-Null
& tar -xf $SourceArchive -C $buildRoot
if ($LASTEXITCODE) { throw 'Cannot extract the verified upstream source.' }
$packetFile = Join-Path $source 'common\net\packet\packet_sing.go'
$original = [IO.File]::ReadAllText($packetFile)
$needle = 'rwOptions := N.ReadWaitOptions{}'
if ([regex]::Matches($original, [regex]::Escape($needle)).Count -ne 1) { throw 'Unexpected upstream packet-buffer implementation.' }
$patched = $original.Replace($needle, 'rwOptions := N.ReadWaitOptions{MTU: 65535}')
[IO.File]::WriteAllText($packetFile, $patched, [Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath (Join-Path $ProjectRoot 'src\Native\UdpUpstream\packet_buffer_test.go') `
    -Destination (Join-Path $source 'common\net\packet\packet_maaa_test.go') -Force
New-Item -ItemType Directory -Path (Join-Path $source 'maaa') -Force | Out-Null
Copy-Item -Path (Join-Path $ProjectRoot 'src\Native\UdpUpstream\README*.md') -Destination (Join-Path $source 'maaa') -Force
$previousCgo = $env:CGO_ENABLED
$env:CGO_ENABLED = '0'
Push-Location $source
try {
    & go test -tags with_gvisor ./common/net/packet -run TestMaaaLargeUotDatagrams -count=1
    if ($LASTEXITCODE) { throw 'Upstream large UDP regression tests failed.' }
    & go build -trimpath -buildvcs=false -tags with_gvisor -ldflags `
        '-s -w -X github.com/metacubex/mihomo/constant.Version=v1.19.34-maaa.1' -o $output .
    if ($LASTEXITCODE) { throw 'Cannot build the task UDP upstream.' }
    # Include exact third-party module sources, not just go.mod/go.sum, in corresponding source.
    & go mod vendor
    if ($LASTEXITCODE) { throw 'Cannot prepare corresponding vendored upstream source.' }
} finally {
    Pop-Location
    $env:CGO_ENABLED = $previousCgo
}
$sourceOutput = Join-Path $ProjectRoot 'artifacts\udp-dependencies\mihomo-v1.19.34-maaa.1-source.tar.gz'
& tar -czf $sourceOutput -C $buildRoot 'mihomo-1.19.34'
if ($LASTEXITCODE) { throw 'Cannot archive corresponding upstream source.' }
[ordered]@{
    upstream = 'v1.19.34'; adaptation = 'maaa.1'; go = (& go version)
    change = 'Sing packet read MTU increased to 65535 to preserve large UoT datagrams.'
    binarySha256 = (Get-FileHash -LiteralPath $output).Hash
    sourceSha256 = (Get-FileHash -LiteralPath $sourceOutput).Hash
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path (Split-Path $output) 'build-info.json') -Encoding utf8
Write-Output 'Built and tested task upstream with full-size UDP receive buffers.'
