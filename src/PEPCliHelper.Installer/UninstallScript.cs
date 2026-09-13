namespace PEPCliHelper.Installer;

/// <summary>Gera o uninstall.cmd gravado na pasta de instalação.</summary>
internal static class UninstallScript
{
  public const string FileName = "uninstall.cmd";

  // Remove do PATH do usuário apenas a entrada igual a PEPCLI_DIR (sem diferenciar maiúsculas e barra final),
  // preservando o tipo do valor (REG_EXPAND_SZ) e as demais entradas. A variável temporária força o aviso WM_SETTINGCHANGE.
  private const string RemoveFromPathScript =
    "$k=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment',$true); " +
    "if($k){ $v=$k.GetValue('Path',$null,[Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames); " +
    "if($v){ $d=$env:PEPCLI_DIR.TrimEnd('\\'); $p=$v -split ';'; " +
    "$n=@($p | Where-Object { $_ -eq '' -or $_.Trim().TrimEnd('\\') -ne $d }); " +
    "if($n.Count -ne $p.Count){ $k.SetValue('Path',($n -join ';'),$k.GetValueKind('Path')); " +
    "[Environment]::SetEnvironmentVariable('PEPCLI_UNINSTALL','1','User'); " +
    "[Environment]::SetEnvironmentVariable('PEPCLI_UNINSTALL',$null,'User') } }; $k.Close() }";

  public static string Build(string destination, string shortcutPath)
  {
    var lines = new[]
    {
      "@echo off",
      "chcp 65001 >nul",
      "setlocal",
      "cd /d \"%TEMP%\"",
      $"echo Removendo o PEP CLI de \"{destination}\"...",
      $"set \"PEPCLI_DIR={destination}\"",
      $"powershell.exe -NoProfile -ExecutionPolicy Bypass -Command \"{RemoveFromPathScript}\"",
      $"if exist \"{shortcutPath}\" del /f /q \"{shortcutPath}\"",
      $"reg delete \"HKCU\\{Installer.UninstallKeyPath}\" /f >nul 2>nul",
      "echo PEP CLI removido. A configuração em %%APPDATA%%\\PepCli e o histórico foram preservados.",
      // (goto) encerra o contexto do lote antes de apagar a pasta que contém este próprio arquivo.
      $"(goto) 2>nul & rd /s /q \"{destination}\"",
    };
    return string.Join("\r\n", lines) + "\r\n";
  }
}
