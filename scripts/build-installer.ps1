param(
  [string]$Configuration = "Release"
)

# Gera artifacts\installer\PEPCLI-Setup-<versao>.exe: instalador de janela única com o pep.exe self-contained embutido.
$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$installerRoot = Join-Path $root "artifacts\installer"
$payloadDir = Join-Path $installerRoot "payload"
$outDir = Join-Path $installerRoot "out"

[xml]$props = Get-Content (Join-Path $root "Directory.Build.props")
$version = ($props.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1)
if (-not $version) { throw "Version não encontrada em Directory.Build.props." }

Write-Host "PEP CLI $version - gerando instalador ($Configuration, win-x64, self-contained)" -ForegroundColor Cyan

& dotnet test (Join-Path $root "PEPCliHelper.sln") --configuration $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw "Testes falharam. Instalador não gerado." }

foreach ($dir in @($payloadDir, $outDir)) {
  if (Test-Path $dir) { Remove-Item -Recurse -Force $dir }
}

$singleFile = @(
  "--configuration", $Configuration,
  "--runtime", "win-x64",
  "--self-contained", "true",
  "-p:PublishSingleFile=true",
  "-p:EnableCompressionInSingleFile=true",
  "-p:IncludeNativeLibrariesForSelfExtract=true",
  "-p:DebugType=none",
  "--nologo"
)

& dotnet publish (Join-Path $root "src\PEPCliHelper\PEPCliHelper.csproj") @singleFile --output $payloadDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish do pep falhou." }

$payload = Join-Path $payloadDir "pep.exe"
if (-not (Test-Path $payload)) { throw "pep.exe não encontrado em $payloadDir." }

& dotnet publish (Join-Path $root "src\PEPCliHelper.Installer\PEPCliHelper.Installer.csproj") @singleFile "-p:PepPayload=$payload" --output $outDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish do instalador falhou." }

$setup = Join-Path $installerRoot "PEPCLI-Setup-$version.exe"
Copy-Item (Join-Path $outDir "PEPCLI-Setup.exe") $setup -Force

$sizeMb = [Math]::Round((Get-Item $setup).Length / 1MB, 1)
Write-Host "Instalador: $setup ($sizeMb MB)" -ForegroundColor Green
Write-Host "Teste sem alterar o perfil: PEPCLI-Setup-$version.exe /silent /dir:<pasta> /nopath /noshortcut /noregistry"
