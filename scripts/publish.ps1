param(
  [string]$Output = (Join-Path $PSScriptRoot "..\artifacts\pep"),
  [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")

Write-Host "PEP CLI - publicando ($Configuration, win-x64, framework-dependent)" -ForegroundColor Cyan

& dotnet test (Join-Path $root "PEPCliHelper.sln") --configuration $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw "Testes falharam. Publicação cancelada." }

& dotnet publish (Join-Path $root "src\PEPCliHelper\PEPCliHelper.csproj") `
  --configuration $Configuration `
  --runtime win-x64 `
  --self-contained false `
  -p:PublishSingleFile=true `
  -p:DebugType=none `
  --output $Output `
  --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falhou." }

Write-Host "Publicado em: $(Resolve-Path $Output)" -ForegroundColor Green
Write-Host "Requer .NET 9 Runtime na máquina de destino. Execute 'pep version' para validar."
