<#
.SYNOPSIS
  Captura saídas reais do tf.exe para validar os parsers do PEP CLI (docs/HOMOLOGACAO.md, H1/H2).

.DESCRIPTION
  Executa SOMENTE comandos de leitura ou preview:
    tf workspaces, tf workfold, tf status, tf resolve /preview, tf changeset /noprompt,
    tf merge /candidate, tf merge /preview, tf get /preview.
  Nunca executa merge, get, checkin, undo, resolve sem /preview ou alteração de workspace.

  Cada saída é gravada em UTF-8 em tests/fixtures/tf-ptbr/<nome>.txt, com cabeçalho contendo
  comando, exit code e code page usada. Por padrão usuário e computador são anonimizados.

.EXAMPLE
  .\scripts\capture-tf-fixtures.ps1 -Changeset 861799 `
    -SourceServer '$/Linha-RM/atual/release/Sau-PEP' `
    -TargetLocal 'C:\Linha-RM\Legado\12.1.2606\Sau-PEP' `
    -TargetServer '$/Linha-RM/Legado/12.1.2606/Sau-PEP'
#>
param(
  [Parameter(Mandatory)] [int]$Changeset,
  [Parameter(Mandatory)] [string]$SourceServer,
  [Parameter(Mandatory)] [string]$TargetLocal,
  [Parameter(Mandatory)] [string]$TargetServer,
  [string]$Collection = "https://totvstfs.visualstudio.com/DefaultCollection",
  [string]$TfExe,
  [string]$Output = (Join-Path $PSScriptRoot "..\tests\fixtures\tf-ptbr"),
  [switch]$NoAnonymize
)

$ErrorActionPreference = "Stop"

if (-not $SourceServer.StartsWith('$/') -or -not $TargetServer.StartsWith('$/')) {
  throw "SourceServer e TargetServer devem ser caminhos TFVC iniciando com `$/."
}
if (-not (Test-Path -LiteralPath $TargetLocal -PathType Container)) {
  throw "TargetLocal não existe: $TargetLocal"
}

if (-not $TfExe) {
  $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
  if (-not (Test-Path $vswhere)) { throw "vswhere.exe não encontrado; informe -TfExe." }
  $TfExe = & $vswhere -latest -products * -find "Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\TF.exe" | Select-Object -First 1
  if (-not $TfExe) { throw "TF.exe não encontrado via vswhere; informe -TfExe." }
}

Add-Type -TypeDefinition @"
using System.Runtime.InteropServices;
public static class PepAnsi { [DllImport("kernel32.dll")] public static extern int GetACP(); }
"@
# Com saída redirecionada e sem janela o tf.exe escreve na code page ANSI (verificado: 1252 em pt-BR).
$codePage = [PepAnsi]::GetACP()
$encoding = [System.Text.Encoding]::GetEncoding($codePage)

New-Item -ItemType Directory -Force -Path $Output | Out-Null
$utf8 = New-Object System.Text.UTF8Encoding $false

# Regras de linha de comando do Windows (compatível com Windows PowerShell 5.1, sem ArgumentList).
function ConvertTo-WindowsArgument([string]$Value) {
  if ($Value.Length -gt 0 -and $Value -notmatch '[\s"]') { return $Value }
  $builder = New-Object System.Text.StringBuilder '"'
  $slashes = 0
  foreach ($char in $Value.ToCharArray()) {
    if ($char -eq '\') { $slashes++; continue }
    if ($char -eq '"') { [void]$builder.Append('\' * ($slashes * 2 + 1)).Append('"'); $slashes = 0; continue }
    [void]$builder.Append('\' * $slashes).Append($char); $slashes = 0
  }
  [void]$builder.Append('\' * ($slashes * 2)).Append('"')
  return $builder.ToString()
}

function Invoke-TfCapture([string]$Name, [string[]]$Arguments, [string]$WorkingDirectory) {
  # Lista de permissão de verbos: qualquer outro verbo (add, branch, rename, label, lock, workspace...) é recusado.
  $allowedVerbs = @('workspaces', 'workfold', 'status', 'resolve', 'changeset', 'merge', 'get')
  if (-not $Arguments -or ($allowedVerbs -notcontains $Arguments[0])) {
    throw "Verbo não permitido no script de captura: '$($Arguments[0])'."
  }
  foreach ($argument in $Arguments) {
    # Prefixo: cobre formas com valor, ex.: /auto:AcceptTheirs, /comment:x, /notes:x.
    foreach ($forbidden in @('checkin', 'undo', 'shelve', 'unshelve', 'rollback', 'destroy', 'delete', '/baseless', '/force', '/overwrite', '/auto', '/map', '/unmap', '/cloak', '/decloak', '/new', '/remove', '/delete', '/comment', '/notes', '/associate')) {
      if (($argument -ieq $forbidden) -or ($forbidden.StartsWith('/') -and $argument.StartsWith("${forbidden}:", [System.StringComparison]::OrdinalIgnoreCase))) {
        throw "Argumento proibido no script de captura: $argument"
      }
    }
  }
  if (($Arguments[0] -in @('merge', 'get')) -and -not (($Arguments -contains '/preview') -or ($Arguments -contains '/candidate'))) {
    throw "'$($Arguments[0])' só é permitido com /preview ou /candidate."
  }
  if (($Arguments[0] -eq 'resolve') -and -not ($Arguments -contains '/preview')) {
    throw "'resolve' só é permitido com /preview."
  }

  $psi = New-Object System.Diagnostics.ProcessStartInfo $TfExe
  $psi.Arguments = ($Arguments | ForEach-Object { ConvertTo-WindowsArgument $_ }) -join ' '
  $psi.UseShellExecute = $false
  $psi.RedirectStandardOutput = $true
  $psi.RedirectStandardError = $true
  $psi.RedirectStandardInput = $true
  $psi.CreateNoWindow = $true
  $psi.StandardOutputEncoding = $encoding
  $psi.StandardErrorEncoding = $encoding
  if ($WorkingDirectory) { $psi.WorkingDirectory = $WorkingDirectory }

  Write-Host "  tf $($Arguments -join ' ')" -ForegroundColor DarkGray
  $process = [System.Diagnostics.Process]::Start($psi)
  $process.StandardInput.Close()
  $stdoutTask = $process.StandardOutput.ReadToEndAsync()
  $stderrTask = $process.StandardError.ReadToEndAsync()
  if (-not $process.WaitForExit(180000)) {
    $process.Kill()
    throw "Tempo esgotado em: tf $($Arguments -join ' ')"
  }
  $text = $stdoutTask.Result + $(if ($stderrTask.Result) { "`n--- stderr ---`n" + $stderrTask.Result } else { "" })

  if (-not $NoAnonymize) {
    foreach ($pair in @(@($env:USERNAME, 'usuario'), @($env:COMPUTERNAME, 'COMPUTADOR'), @($env:USERDOMAIN, 'DOMINIO'))) {
      if ($pair[0]) { $text = $text -replace [regex]::Escape($pair[0]), $pair[1] }
    }
  }

  $header = "# tf $($Arguments -join ' ')`n# exitCode: $($process.ExitCode)`n# codePage: $codePage`n# capturado: $(Get-Date -Format s)`n"
  [System.IO.File]::WriteAllText((Join-Path $Output "$Name.txt"), $header + $text, $utf8)
  $color = if ($process.ExitCode -eq 0) { 'Green' } else { 'Yellow' }
  Write-Host "    -> $Name.txt (exit $($process.ExitCode))" -ForegroundColor $color
}

$version = "C$Changeset~C$Changeset"
Write-Host "Capturando saídas do tf.exe (somente leitura) em $Output" -ForegroundColor Cyan
Write-Host "tf.exe: $TfExe | code page: $codePage"

Invoke-TfCapture "workspaces" @("workspaces", "/collection:$Collection") $null
Invoke-TfCapture "workfold" @("workfold", $TargetLocal) $TargetLocal
Invoke-TfCapture "status-detailed" @("status", $TargetLocal, "/recursive", "/format:detailed") $TargetLocal
Invoke-TfCapture "resolve-preview" @("resolve", $TargetLocal, "/recursive", "/preview", "/noprompt") $TargetLocal
Invoke-TfCapture "changeset" @("changeset", "$Changeset", "/collection:$Collection", "/noprompt") $null
Invoke-TfCapture "merge-candidate" @("merge", "/candidate", "/recursive", "/version:$version", $SourceServer, $TargetServer) $TargetLocal
Invoke-TfCapture "merge-preview" @("merge", "/preview", "/recursive", "/noimplicitbaseless", "/version:$version", $SourceServer, $TargetServer, "/noprompt") $TargetLocal
Invoke-TfCapture "get-preview" @("get", $TargetLocal, "/recursive", "/preview", "/noprompt") $TargetLocal

Write-Host ""
Write-Host "Concluído. Revise os arquivos antes de compartilhar (caminhos e nomes de projeto podem aparecer)." -ForegroundColor Green
Write-Host "Nenhum comando de alteração foi executado."
