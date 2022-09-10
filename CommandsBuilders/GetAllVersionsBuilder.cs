namespace TFSCliHelper
{
  public class GetAllVersionsBuilder : ICommandBuilder
  {
    private string TFEXEPATH = @"""C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\TF.exe""";
    private readonly string recursive = " /recursive";


    public ICommandExecutor Executor { get; private set; }

    public GetAllVersionsBuilder()
    {
      Executor = new TFSCommandExecutor();
    }

    public void Build()
    {
      GetFrontFiles();
      GetBackFiles();

      Executor.Execute();
    }

    private void GetFrontFiles()
    {
      Executor.AddCommand(new Command("cd", EnumVersionsFront._32));
      Executor.AddCommand(new Command($"{TFEXEPATH} get", TFSServerPathFront._32 + recursive));

      Executor.AddCommand(new Command("cd", EnumVersionsFront._33));
      Executor.AddCommand(new Command($"{TFEXEPATH} get", TFSServerPathFront._33 + recursive));

      Executor.AddCommand(new Command("cd", EnumVersionsFront._34));
      Executor.AddCommand(new Command($"{TFEXEPATH} get", TFSServerPathFront._34 + recursive));

      Executor.AddCommand(new Command("cd", EnumVersionsFront._2205));
      Executor.AddCommand(new Command($"{TFEXEPATH} get", TFSServerPathFront._2205 + recursive));

      Executor.AddCommand(new Command("cd", EnumVersionsFront._2209));
      Executor.AddCommand(new Command($"{TFEXEPATH} get", TFSServerPathFront._2209 + recursive));
    }

    private void GetBackFiles()
    {
      Executor.AddCommand(new Command("cd", EnumVersionsBack._32));
      Executor.AddCommand(new Command($"{TFEXEPATH} get", TFSServerPathFront._32 + recursive));

      Executor.AddCommand(new Command("cd", EnumVersionsBack._33));
      Executor.AddCommand(new Command($"{TFEXEPATH} get", TFSServerPathFront._33 + recursive));

      Executor.AddCommand(new Command("cd", EnumVersionsBack._34));
      Executor.AddCommand(new Command($"{TFEXEPATH} get", TFSServerPathFront._34 + recursive));

      Executor.AddCommand(new Command("cd", EnumVersionsBack._2205));
      Executor.AddCommand(new Command($"{TFEXEPATH} get", TFSServerPathFront._2205 + recursive));

      Executor.AddCommand(new Command("cd", EnumVersionsBack._2209));
      Executor.AddCommand(new Command($"{TFEXEPATH} get", TFSServerPathFront._2209 + recursive));
    }
  }
}
