param(
    [switch]$TurnOffTestSigning,
    [switch]$AllUsers
)

$ErrorActionPreference = 'Stop'
# The uninstaller starts the script without a console: Windows PowerShell then fails on any output until the encoding is set.
[Console]::OutputEncoding = [Text.Encoding]::Default
$restartNeeded = 3010
$serviceName = '4MagicKeysPlus'
$serviceKey = "HKLM:\SYSTEM\CurrentControlSet\Services\$serviceName"
$driverFile = Join-Path $env:SystemRoot 'System32\drivers\4MagicKeysPlus.sys'
$certificateSubject = 'CN=4MagicKeysPlus local signing'
$keyFolder = Join-Path $env:ProgramData '4MagicKeysPlusSigning'
$userFolderName = '4MagicKeysPlus'
$autostartValue = '4MagicKeysPlusAgent'
$temporaryHive = 'HKEY_USERS\4MagicKeysPlusUninstall'

$script:failed = $false
$script:restart = $false

function Invoke-Step([string]$Name, [scriptblock]$Action) {
    Write-Host "== $Name"
    try {
        & $Action
    } catch {
        Write-Host "FAILED: $_"
        $script:failed = $true
    }
}

function Invoke-Native([string]$File, [string[]]$Arguments, [int[]]$Success = @(0)) {
    & $File @Arguments 2>&1 | Out-Host
    if ($LASTEXITCODE -notin $Success) {
        throw "$File $Arguments ended with exit code $LASTEXITCODE"
    }
    if ($LASTEXITCODE -eq $restartNeeded) {
        $script:restart = $true
    }
}

function Remove-DriverFromDevices {
    $roots = 'HKLM:\SYSTEM\CurrentControlSet\Enum\HID', 'HKLM:\SYSTEM\CurrentControlSet\Enum\BTHENUM', 'HKLM:\SYSTEM\CurrentControlSet\Enum\USB'
    Get-ChildItem $roots -Recurse -ErrorAction Ignore | ForEach-Object {
        $filters = (Get-ItemProperty -LiteralPath $_.PSPath -ErrorAction Ignore).LowerFilters
        if ($filters -contains $serviceName) {
            $others = @($filters | Where-Object { $_ -notin 'vhf', $serviceName })
            if ($others) {
                Set-ItemProperty -LiteralPath $_.PSPath -Name LowerFilters -Value ([string[]]$others)
            } else {
                Remove-ItemProperty -LiteralPath $_.PSPath -Name LowerFilters
            }
            Write-Host $_.Name
        }
    }
}

function Remove-DriverPackages {
    Get-WindowsDriver -Online | Where-Object { $_.OriginalFileName -like '*\4magickeysplus.inf' } | ForEach-Object {
        Invoke-Native pnputil '/delete-driver', $_.Driver, '/uninstall', '/force' -Success 0, $restartNeeded
    }
}

function Remove-DriverService {
    & sc.exe delete $serviceName | Out-Host
    & sc.exe query $serviceName | Out-Null
    if ($LASTEXITCODE -eq 0) {
        $script:restart = $true
    }
    Remove-Item $serviceKey -Recurse -Force -ErrorAction Ignore
}

function Remove-DriverFile {
    if (-not (Test-Path $driverFile)) {
        return
    }
    try {
        Remove-Item $driverFile -Force
    } catch {
        Add-Type -Namespace Native -Name Kernel32 -MemberDefinition '[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool MoveFileEx(string existing, string replacement, int flags);'
        $delayUntilReboot = 4
        if (-not [Native.Kernel32]::MoveFileEx($driverFile, $null, $delayUntilReboot)) {
            throw "$driverFile could not be deleted"
        }
        Write-Host "$driverFile is deleted at the next restart"
        $script:restart = $true
    }
}

function Remove-UserData([string]$RegistryRoot, [string]$LocalAppData) {
    Remove-Item "$RegistryRoot\Software\$userFolderName" -Recurse -Force -ErrorAction Ignore
    foreach ($runKey in 'Software\Microsoft\Windows\CurrentVersion\Run', 'Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run') {
        Remove-ItemProperty "$RegistryRoot\$runKey" -Name $autostartValue -ErrorAction Ignore
    }
    if ($LocalAppData) {
        Remove-Item (Join-Path $LocalAppData $userFolderName) -Recurse -Force -ErrorAction Ignore
    }
}

function Remove-AllUsersData {
    $profiles = Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList' | Where-Object { $_.PSChildName -like 'S-1-5-21-*' }
    foreach ($profile in $profiles) {
        $sid = $profile.PSChildName
        $profileFolder = $profile.GetValue('ProfileImagePath')
        $localAppData = if ($profileFolder) { Join-Path $profileFolder 'AppData\Local' }
        Write-Host "$sid $profileFolder"
        if (Test-Path "Registry::HKEY_USERS\$sid") {
            Remove-UserData "Registry::HKEY_USERS\$sid" $localAppData
            continue
        }
        $hive = Join-Path "$profileFolder" 'NTUSER.DAT'
        if (-not (Test-Path $hive)) {
            continue
        }
        Invoke-Native reg 'load', $temporaryHive, $hive
        try {
            Remove-UserData "Registry::$temporaryHive" $localAppData
        } finally {
            [GC]::Collect()
            [GC]::WaitForPendingFinalizers()
            Invoke-Native reg 'unload', $temporaryHive
        }
    }
}

$log = Join-Path $env:TEMP '4MagicKeysPlus-uninstall.log'
Start-Transcript $log -Force | Out-Null

Invoke-Step 'Stop the agent and the app' { Get-Process '4MagicKeysPlusAgent', '4MagicKeysPlus' -ErrorAction Ignore | Stop-Process -Force }
Invoke-Step 'Take the driver off the keyboards' { Remove-DriverFromDevices }
Invoke-Step 'Remove the driver packages' { Remove-DriverPackages }
Invoke-Step 'Remove the driver service and the keyboard settings' { Remove-DriverService }
Invoke-Step 'Remove the driver file' { Remove-DriverFile }
Invoke-Step 'Remove the signing certificate' {
    Get-ChildItem Cert:\LocalMachine\Root, Cert:\LocalMachine\TrustedPublisher | Where-Object Subject -eq $certificateSubject | Remove-Item
}
Invoke-Step 'Remove the signing key' { Remove-Item $keyFolder -Recurse -Force -ErrorAction Ignore }
Invoke-Step 'Remove the data of this user' { Remove-UserData 'HKCU:' $env:LOCALAPPDATA }
if ($AllUsers) {
    Invoke-Step 'Remove the data of all users' { Remove-AllUsersData }
}
if ($TurnOffTestSigning) {
    Invoke-Step 'Turn TESTSIGNING off' {
        Invoke-Native bcdedit '/set', '{current}', 'testsigning', 'off'
        $script:restart = $true
    }
}

Stop-Transcript | Out-Null
if ($script:failed) {
    exit 1
}
Remove-Item $log -ErrorAction Ignore
if ($script:restart) { exit $restartNeeded } else { exit 0 }
