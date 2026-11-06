$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$output = Join-Path $root 'bin\publish'
$work = Join-Path $root 'obj\release'

function Invoke-Native([scriptblock]$Command) {
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $Command"
    }
}

$vs = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -property installationPath
$msbuild = "$vs\MSBuild\Current\Bin\amd64\MSBuild.exe"

New-Item -ItemType Directory $output -Force | Out-Null
Get-ChildItem $output -Force | Remove-Item -Recurse -Force
New-Item -ItemType Directory "$output\driver", "$output\app" | Out-Null

# Own output and intermediate folders keep the signed bin\Release\driver of a local build untouched.
$driverOut = "$work\driver\"
Invoke-Native { & $msbuild "$root\4MagicKeysPlusDriver\4MagicKeysPlusDriver.vcxproj" -nologo -v:m -p:Configuration=Release -p:Platform=x64 -p:SignMode=Off "-p:OutDir=$driverOut" "-p:IntDir=$work\driver-int\" }
Copy-Item "$driverOut\4MagicKeysPlusDriver\*" "$output\driver"

foreach ($project in '4MagicKeysPlus', '4MagicKeysPlusAgent') {
    Invoke-Native { dotnet publish "$root\$project" -c Release -r win-x64 --self-contained -o "$output\app" --nologo -v:m "-p:OutputPath=$work\build\" -p:MkpSignAgent=false -p:SatelliteResourceLanguages=en%3Bru }
}
Remove-Item "$output\app\*.pdb"

Copy-Item "$root\packaging\*" $output
Write-Host "Release files are in $output"
