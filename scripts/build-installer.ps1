$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$version = (Select-Xml -Path "$root\Directory.Build.props" -XPath '//MkpAppVersion').Node.InnerText
$setup = "$root\bin\4MagicKeysPlus-$version-setup.exe"
$iscc = if ($env:ISCC) { $env:ISCC } else { "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" }

Remove-Item $setup, "$setup.sha256" -ErrorAction Ignore
& $iscc /Q "/DAppVersion=$version" "/DPublishDir=$root\bin\publish" "/O$root\bin" "$root\installer\4MagicKeysPlus.iss"
if ($LASTEXITCODE -ne 0) {
    throw "ISCC failed with exit code $LASTEXITCODE"
}

$hash = (Get-FileHash $setup -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$setup.sha256", "$hash  $(Split-Path $setup -Leaf)`n")
Write-Host "Installer is $setup"
