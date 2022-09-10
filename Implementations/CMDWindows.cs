using System.Diagnostics;
using System.Text;

namespace TFSCliHelper
{
  public class CMDWindows : IPrompt
  {
    private Process _cmd;

    public CMDWindows()
    {
      _cmd = new Process();
      _cmd.StartInfo.FileName = "cmd.exe";
      _cmd.StartInfo.WorkingDirectory = @"C:\";
      _cmd.StartInfo.RedirectStandardError = true;
      _cmd.StartInfo.RedirectStandardInput = true;
      _cmd.StartInfo.RedirectStandardOutput = true;
      _cmd.StartInfo.CreateNoWindow = true;
      _cmd.StartInfo.UseShellExecute = false;
      _cmd.Start();
    }

    public void Finish()
    {
      _cmd.Close();
    }

    public string Read()
    {
      StringBuilder stringBuilder = new StringBuilder();
      string promptLine = null;

      do
      {
        promptLine = _cmd.StandardOutput.ReadLine();
        stringBuilder.AppendLine(promptLine);
      } while (!string.IsNullOrEmpty(promptLine));

      return stringBuilder.ToString();
    }

    public void Write(string command)
    {
      _cmd.StandardInput.WriteLine(command);
      _cmd.StandardInput.Flush();
    }
  }
}
