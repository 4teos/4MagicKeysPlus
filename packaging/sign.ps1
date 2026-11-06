$ErrorActionPreference = 'Stop'

$subject = 'CN=4MagicKeysPlus local signing'
$keyFolder = Join-Path $env:ProgramData '4MagicKeysPlusSigning'
$pfxPath = Join-Path $keyFolder 'signing-key.pfx'
$administrators = 'S-1-5-32-544'
$system = 'S-1-5-18'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not ([Security.Principal.WindowsPrincipal]$identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Administrator rights are needed, start sign.cmd instead of this script.'
}

$files = @(Get-ChildItem (Join-Path $PSScriptRoot 'driver') -Include '*.sys', '*.cat' -Recurse -ErrorAction Ignore) +
    @(Get-Item (Join-Path $PSScriptRoot 'app\4MagicKeysPlusAgent.exe') -ErrorAction Ignore)
if ($files.Count -ne 3) {
    throw 'driver\*.sys, driver\*.cat and app\4MagicKeysPlusAgent.exe must be next to sign.ps1. Start sign.cmd from the folder where you unpacked the release archive and keep its folders as they are.'
}

Get-ChildItem $PSScriptRoot -Recurse -File | Unblock-File

# Any user may create folders in ProgramData, so a folder that is already there must belong to the administrators.
if (Test-Path $keyFolder) {
    $owner = (Get-Acl $keyFolder).GetOwner([Security.Principal.SecurityIdentifier]).Value
    if ($owner -notin $administrators, $system) {
        throw "$keyFolder was not created by an administrator. Delete it and start sign.cmd again."
    }
} else {
    New-Item -ItemType Directory $keyFolder | Out-Null
    icacls $keyFolder /inheritance:r /grant:r "*${administrators}:(OI)(CI)F" "*${system}:(OI)(CI)F" | Out-Null
}

$reused = Test-Path $pfxPath
if (-not $reused) {
    $created = New-SelfSignedCertificate -Type CodeSigningCert -Subject $subject -FriendlyName '4MagicKeysPlus local signing' -CertStoreLocation Cert:\LocalMachine\My -HashAlgorithm SHA256 -KeyExportPolicy Exportable -Provider 'Microsoft Strong Cryptographic Provider' -NotAfter (Get-Date '2040-01-01')
    [IO.File]::WriteAllBytes($pfxPath, $created.Export('Pfx', ''))
    Remove-Item "Cert:\LocalMachine\My\$($created.Thumbprint)" -DeleteKey
}
Copy-Item (Join-Path $PSScriptRoot 'signing-key-readme.txt') (Join-Path $keyFolder 'README.txt') -Force

$certificate = New-Object Security.Cryptography.X509Certificates.X509Certificate2($pfxPath, '')
if ($reused -and -not (Test-Path "Cert:\LocalMachine\Root\$($certificate.Thumbprint)")) {
    throw "The certificate in $pfxPath is not trusted by this computer, so sign.cmd did not make it here. Delete $keyFolder and start sign.cmd again."
}

$publicPart = New-Object Security.Cryptography.X509Certificates.X509Certificate2(, $certificate.RawData)
foreach ($storeName in 'Root', 'TrustedPublisher') {
    $store = New-Object Security.Cryptography.X509Certificates.X509Store($storeName, 'LocalMachine')
    $store.Open('ReadWrite')
    $store.Add($publicPart)
    $store.Close()
}

foreach ($file in $files) {
    $result = Set-AuthenticodeSignature -FilePath $file.FullName -Certificate $certificate -HashAlgorithm SHA256
    if ($result.Status -ne 'Valid') {
        throw "Signing $($file.Name) failed: $($result.StatusMessage)"
    }
    Write-Host "Signed $($file.FullName.Substring($PSScriptRoot.Length + 1))"
}

if (-not (bcdedit /enum '{current}' | Select-String -Quiet 'testsigning\s+Yes')) {
    Write-Warning 'TESTSIGNING is off, Windows will not load the driver (see the Installation section of the README).'
}

Write-Host ''
Write-Host "The signing key stays in $keyFolder"
Write-Host 'README.txt there says what it is and how to delete it.'
