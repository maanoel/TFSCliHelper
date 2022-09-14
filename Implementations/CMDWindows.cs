using System.Diagnostics;
using System.Text;

namespace PEPCliHelper
{
  public class CMDWindows : IPrompt
  {
    private Process _cmd;
    private ProcessStartInfo _info;

    public CMDWindows()
    {
      _info = new ProcessStartInfo();
      _info.FileName = "cmd.exe";
      _info.RedirectStandardInput = true;
      _info.Verb = "runas";

      _cmd = new Process();
      _cmd.StartInfo = _info;
      _cmd.Start();
    }

    public void Finish()
    {
      _cmd.Close();
    }

    public void Wait()
    {
      _cmd.WaitForExit();
    }

    public void Write(string command)
    {
      _cmd.StandardInput.WriteLine(command);
    }

    public void Execute()
    {
      _cmd.StandardInput.Flush();
      _cmd.StandardInput.Close();
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
  }
}
