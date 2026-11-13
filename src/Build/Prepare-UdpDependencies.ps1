param([Parameter(Mandatory=$true)][string]$ProjectRoot)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Utility\Microsoft.PowerShell.Utility.psd1') -ErrorAction Stop
$directory = Join-Path $ProjectRoot 'artifacts\udp-dependencies'
New-Item -ItemType Directory -Path $directory -Force | Out-Null
function Get-VerifiedAsset([string]$Name, [string]$Url, [string]$Hash) {
    $path = Join-Path $directory $Name
    if (Test-Path -LiteralPath $path) {
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ieq $Hash) { return $path }
        throw "Cached dependency hash mismatch: $Name"
    }
    $temporary = $path + '.download'
    Invoke-WebRequest -UseBasicParsing -Uri $Url -OutFile $temporary -TimeoutSec 600
    if ((Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash -ine $Hash) {
        throw "Downloaded dependency hash mismatch: $Name"
    }
    Move-Item -LiteralPath $temporary -Destination $path
    return $path
}
$driver = Get-VerifiedAsset 'Windows.Packet.Filter.3.6.2.1.x64.msi' `
    'https://github.com/wiresock/ndisapi/releases/download/v3.6.2/Windows.Packet.Filter.3.6.2.1.x64.msi' `
    '9c388c0b7f189f7fa98720bae2caecf7d64f30910838b80b438ecf8956b8502c'
$source = Get-VerifiedAsset 'mihomo-v1.19.34-source.tar.gz' `
    'https://codeload.github.com/MetaCubeX/mihomo/tar.gz/refs/tags/v1.19.34' `
    'e3284a44f1052f1c454c90d418faa244516c2470bf232b455ea18fc4f5fa6028'
& (Join-Path $PSScriptRoot 'Prepare-UdpUpstream.ps1') -ProjectRoot $ProjectRoot -SourceArchive $source
Write-Output 'Verified UDP driver, patched upstream, and complete corresponding source are ready.'
