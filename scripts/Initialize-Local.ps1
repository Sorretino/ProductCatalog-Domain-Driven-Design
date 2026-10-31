#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $projectRoot '.env'
if (Test-Path -LiteralPath $envFile) {
    Write-Host '.env já existe; as credenciais foram preservadas.'
    return
}

function New-LocalSecret {
    [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
}

$content = @(
    "POSTGRES_PASSWORD=$(New-LocalSecret)"
    "KEYCLOAK_ADMIN_PASSWORD=$(New-LocalSecret)"
    "CATALOG_CLIENT_SECRET=$(New-LocalSecret)"
)
[System.IO.File]::WriteAllLines($envFile, $content)
Write-Host 'Credenciais locais geradas em .env. Execute docker compose up --build -d.'
