# Installs or updates lazytsql from its GitHub releases on Windows:
#
#   irm https://raw.githubusercontent.com/carandev/lazytsql/main/install.ps1 | iex
#
# Options, as environment variables:
#   LAZYTSQL_INSTALL_DIR   where to put lazytsql.exe (default: %LOCALAPPDATA%\lazytsql\bin)
#   LAZYTSQL_VERSION       a version such as 0.1.3 (default: the latest release)
#   LAZYTSQL_DOWNLOAD_URL  a mirror of the release downloads (default: GitHub)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repo = 'carandev/lazytsql'
$installDir = if ($env:LAZYTSQL_INSTALL_DIR) { $env:LAZYTSQL_INSTALL_DIR } else { Join-Path $env:LOCALAPPDATA 'lazytsql\bin' }
$version = if ($env:LAZYTSQL_VERSION) { $env:LAZYTSQL_VERSION.TrimStart('v') } else { 'latest' }

if (-not [Environment]::Is64BitOperatingSystem) {
    throw 'lazytsql install: only 64-bit Windows is supported.'
}

# Windows on ARM runs the x64 build through emulation.
$asset = 'lazytsql-win-x64.zip'
$base = if ($env:LAZYTSQL_DOWNLOAD_URL) { $env:LAZYTSQL_DOWNLOAD_URL }
        elseif ($version -eq 'latest') { "https://github.com/$repo/releases/latest/download" }
        else { "https://github.com/$repo/releases/download/v$version" }

$tmp = Join-Path ([IO.Path]::GetTempPath()) ("lazytsql-" + [Guid]::NewGuid())
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
    Write-Host "Downloading $asset ($version)..."
    $zip = Join-Path $tmp $asset
    Invoke-WebRequest -UseBasicParsing -Uri "$base/$asset" -OutFile $zip

    # Releases from 0.1.3 on publish SHA256SUMS; an older release installs without the check.
    $sums = Join-Path $tmp 'SHA256SUMS'
    $hasSums = $true
    try { Invoke-WebRequest -UseBasicParsing -Uri "$base/SHA256SUMS" -OutFile $sums } catch { $hasSums = $false }
    if ($hasSums) {
        $line = Get-Content $sums | Where-Object { $_ -match "\s$([regex]::Escape($asset))$" } | Select-Object -First 1
        if (-not $line) { throw "lazytsql install: $asset is not listed in SHA256SUMS." }
        $expected = ($line -split '\s+')[0].ToLowerInvariant()
        $actual = (Get-FileHash -Algorithm SHA256 $zip).Hash.ToLowerInvariant()
        if ($expected -ne $actual) { throw "lazytsql install: checksum mismatch for ${asset}: expected $expected, got $actual." }
        Write-Host 'Checksum verified.'
    } else {
        Write-Host 'This release has no SHA256SUMS; skipping the checksum check.'
    }

    Expand-Archive -Path $zip -DestinationPath $tmp -Force
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
    $exe = Join-Path $installDir 'lazytsql.exe'
    try {
        Copy-Item (Join-Path $tmp 'lazytsql.exe') $exe -Force
    } catch {
        throw "lazytsql install: could not replace $exe. Close every running lazytsql and run this again."
    }
} finally {
    Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
}

$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
$entries = if ($userPath) { $userPath -split ';' } else { @() }
if ($entries -notcontains $installDir) {
    [Environment]::SetEnvironmentVariable('Path', (($entries + $installDir) | Where-Object { $_ }) -join ';', 'User')
    Write-Host "Added $installDir to your PATH; new terminals will find lazytsql."
}
if (($env:Path -split ';') -notcontains $installDir) { $env:Path = "$env:Path;$installDir" }

Write-Host "Installed lazytsql $(& $exe --version) to $exe"
