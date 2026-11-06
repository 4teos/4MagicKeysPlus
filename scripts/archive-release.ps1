$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$version = (Select-Xml -Path "$root\Directory.Build.props" -XPath '//MkpAppVersion').Node.InnerText
$zip = "$root\bin\4MagicKeysPlus-$version-win-x64.zip"

Remove-Item $zip, "$zip.sha256" -ErrorAction Ignore
[IO.Compression.ZipFile]::CreateFromDirectory("$root\bin\publish", $zip, 'Optimal', $false)

$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$zip.sha256", "$hash  $(Split-Path $zip -Leaf)`n")

if ($env:GITHUB_OUTPUT) {
    "version=$version" | Out-File $env:GITHUB_OUTPUT -Append -Encoding utf8
}
Write-Host "Archive is $zip"
