$ErrorActionPreference = 'Stop'
$env:PORT = '5001'

Write-Host 'TUSA backend: http://localhost:5001'
Write-Host 'Swagger: http://localhost:5001/swagger'
dotnet run --project .\TusaMap.Api\TusaMap.Api.csproj