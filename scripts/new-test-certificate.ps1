param([string]$OutFile = (Join-Path $PSScriptRoot '..\4MagicKeysPlusDriver\4MagicKeysPlus.pfx'))

if (Test-Path $OutFile) {
    throw "$OutFile already exists, not overwriting it"
}

# A CNG key (the default provider) makes the WDK SignTask fail with "Invalid certificate or password".
$cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=4MagicKeysPlus' -CertStoreLocation Cert:\CurrentUser\My -HashAlgorithm SHA256 -KeyExportPolicy Exportable -Provider 'Microsoft Strong Cryptographic Provider' -NotAfter (Get-Date '2040-01-01')
[System.IO.File]::WriteAllBytes($OutFile, $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, ''))
Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)" -Force
Write-Host "Created $OutFile"
