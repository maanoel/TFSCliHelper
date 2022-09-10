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
      Executor.AddCommand(new Command("cd", EnumVersions._32));
      Executor.AddCommand(new Command($"{TFEXEPATH} get", TFSServerPath._32 + recursive));

      Executor.AddCommand(new Command("cd", EnumVersions._33));
      Executor.AddCommand(new Command($"{TFEXEPATH} get", TFSServerPath._33 + recursive));

      Executor.AddCommand(new Command("cd", EnumVersions._34));
      Executor.AddCommand(new Command($"{TFEXEPATH} get", TFSServerPath._34 + recursive));

      Executor.AddCommand(new Command("cd", EnumVersions._2205));
      Executor.AddCommand(new Command($"{TFEXEPATH} get", TFSServerPath._2205 + recursive));

      Executor.AddCommand(new Command("cd", EnumVersions._2209));
      Executor.AddCommand(new Command($"{TFEXEPATH} get", TFSServerPath._2209 + recursive));

      Executor.Execute();
    }
  }
}
