param([Parameter(Mandatory)][scriptblock]$Command)

$ErrorActionPreference = 'Continue'
$log = New-TemporaryFile
& $Command 2>&1 | Tee-Object -FilePath $log
$code = $LASTEXITCODE

# Job logs need a sign-in even in a public repo, annotations on the run page do not.
# "dotnet test" exits with 0 when it finds no tests, so the totals are worth seeing.
Select-String -Path $log -Pattern '^(Passed|Failed)!\s+- Failed:', '^\s*Total tests:', '^\s+Passed:' |
    ForEach-Object { '::notice::' + $_.Line.Trim() }

if ($code -ne 0) {
    Select-String -Path $log -Pattern '\berror [A-Za-z]+\d+:', '^\s*Failed\s', 'Failed:\s*[1-9]' |
        Select-Object -First 10 |
        ForEach-Object { '::error::' + $_.Line.Trim().Replace('%', '%25') }
}
exit $code
