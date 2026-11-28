param(
    [Parameter(Mandatory)][string]$AppFolder,
    [switch]$TurnOnTestSigning
)

$ErrorActionPreference = 'Stop'
# Setup starts the script without a console: Windows PowerShell then fails on any output until the encoding is set.
[Console]::OutputEncoding = [Text.Encoding]::Default
$restartNeeded = 3010
$noDeviceUpdated = 259

function Invoke-Native([string]$File, [string[]]$Arguments, [int[]]$Success = @(0)) {
    & $File @Arguments 2>&1 | Out-Host
    if ($LASTEXITCODE -notin $Success) {
        throw "$File $Arguments ended with exit code $LASTEXITCODE"
    }
    $LASTEXITCODE
}

Start-Transcript (Join-Path $AppFolder 'setup\install.log') -Force | Out-Null
try {
    $restart = $false
    if ($TurnOnTestSigning) {
        Invoke-Native bcdedit '/set', '{current}', 'testsigning', 'on' | Out-Null
        $restart = $true
    }

    & (Join-Path $PSScriptRoot 'sign.ps1') -DriverFolder (Join-Path $AppFolder 'driver') -AgentPath (Join-Path $AppFolder '4MagicKeysPlusAgent.exe')

    $inf = Join-Path $AppFolder 'driver\4MagicKeysPlus.inf'
    $code = Invoke-Native pnputil '/add-driver', $inf, '/install' -Success 0, $noDeviceUpdated, $restartNeeded
    $restart = $restart -or $code -eq $restartNeeded

    Invoke-Native (Join-Path $AppFolder '4MagicKeysPlus.exe') '--install-defaults' | Out-Null

    if ($restart) { exit $restartNeeded } else { exit 0 }
} catch {
    Write-Host $_
    exit 1
} finally {
    Stop-Transcript | Out-Null
}
