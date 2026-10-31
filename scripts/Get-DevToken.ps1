#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$envFile = Join-Path (Split-Path -Parent $PSScriptRoot) '.env'
if (!(Test-Path -LiteralPath $envFile)) { throw 'Execute scripts/Initialize-Local.ps1 primeiro.' }
$entry = Get-Content -LiteralPath $envFile | Where-Object { $_.StartsWith('CATALOG_CLIENT_SECRET=') }
if (@($entry).Count -ne 1) { throw 'CATALOG_CLIENT_SECRET ausente ou duplicado em .env.' }
$secret = $entry.Substring('CATALOG_CLIENT_SECRET='.Length)
$response = Invoke-RestMethod -Method Post -Uri 'http://localhost:8080/realms/product-catalog/protocol/openid-connect/token' -Body @{
    grant_type = 'client_credentials'
    client_id = 'catalog-local-client'
    client_secret = $secret
    scope = 'catalog.read catalog.write'
}
$response.access_token
