$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$localSdk = Join-Path (Split-Path -Parent $projectRoot) '.tools/dotnet/dotnet.exe'
if (Test-Path -LiteralPath $localSdk) {
    $env:DOTNET_ROOT = Split-Path -Parent $localSdk
    $env:DOTNET_CLI_HOME = Join-Path (Split-Path -Parent $projectRoot) '.tools/cli'
    $env:NUGET_PACKAGES = Join-Path (Split-Path -Parent $projectRoot) '.tools/packages'
    $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
    & $localSdk @args
} else {
    & dotnet @args
}
exit $LASTEXITCODE
