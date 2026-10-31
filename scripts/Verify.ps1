#Requires -Version 7.0
param([switch]$Postgres)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    if ($Postgres) { $env:RUN_POSTGRES_TESTS = '1' }
    & "$PSScriptRoot/dotnet.ps1" restore ProductCatalog.slnx --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Falha no restore.' }
    & "$PSScriptRoot/dotnet.ps1" build ProductCatalog.slnx -c Release --no-restore --disable-build-servers
    if ($LASTEXITCODE -ne 0) { throw 'Falha no build.' }
    & "$PSScriptRoot/dotnet.ps1" test --solution ProductCatalog.slnx -c Release --no-build --no-restore --report-xunit-trx --results-directory artifacts/test-results
    if ($LASTEXITCODE -ne 0) { throw 'Falha nos testes.' }
} finally {
    Pop-Location
}
