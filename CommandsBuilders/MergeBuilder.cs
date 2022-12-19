using System.Collections.Generic;

namespace PEPCliHelper
{
  public class MergeBuilder : ICommandBuilder
  {
    private string _pathFrontTfs;
    private string _pathSauTfs;
    private string _pathBackTfs;
    private string _changeSet;
    private string _version;
    private string _project;
    private readonly string _recursive = "/recursive";
    private readonly List<string> _pathsFront;
    private readonly List<string> _pathsBack;
    private readonly List<string> _pathsSau;
    private readonly string TFEXEPATH = @"""C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\TF.exe""";

    public ICommandExecutor Executor { get; private set; }

    public MergeBuilder()
    {
      Executor = new TfsCommandExecutor();
      _pathsFront = (new StructTfsServerPathFront() as IStructPath).GetAllVersions(); 
      _pathsBack = (new StructTfsServerPathBack() as IStructPath).GetAllVersions();
      _pathsSau = (new StructTfsServerPathSau() as IStructPath).GetAllVersions();
    }

    public void Build(string arguments)
    {
      PrepareCommand(arguments);
      GetVersionFiles();
      ExecuteCommands();
    }

    private void PrepareCommand(string command)
    {
      var commandSplit = command.Split(" ");
      _changeSet = commandSplit[3];
      _version = commandSplit[2];
      _project = commandSplit[1];

      _pathFrontTfs = (new StructTfsServerPathFront() as IStructPath).GetVersion(_version);
      _pathBackTfs = (new StructTfsServerPathBack() as IStructPath).GetVersion(_version);
      _pathSauTfs = (new StructTfsServerPathSau() as IStructPath).GetVersion(_version);
    }

    private void GetVersionFiles()
    {
      MergeFront();
      MergeBack();
      MergeSau();
    }

    private void MergeFront()
    {
      if (_project.Equals("front"))
      {
        foreach (var version in _pathsFront)
        {
          if (version.Equals(_pathFrontTfs)) continue;

          Executor.AddCommand(new Command($"cd", @"C:\Linha-RM")); 
          Executor.AddCommand(new Command($"{TFEXEPATH} workspaces /collection:https://totvstfs.visualstudio.com/DefaultCollection"));
          Executor.AddCommand(new Command($"{TFEXEPATH} merge /baseless /version:{_changeSet}~{_changeSet}", _pathFrontTfs + " " + version + " " + _recursive));
        }
      }
    }

    private void MergeBack()
    {
      if (_project.Equals("back"))
      {
        foreach (var version in _pathsBack)
        {
          if (version.Equals(_pathBackTfs)) continue;

          Executor.AddCommand(new Command($"cd", @"C:\RM"));
          Executor.AddCommand(new Command($"{TFEXEPATH} workspaces /collection:https://totvstfs.visualstudio.com/DefaultCollection"));
          Executor.AddCommand(new Command($"{TFEXEPATH} merge /baseless /version:{_changeSet}~{_changeSet}", _pathBackTfs + " " + version + " " + _recursive));
        }
      }
    }

    private void MergeSau()
    {
      if (_project.Equals("sau"))
      {
        foreach (var version in _pathsSau)
        {
          if (version.Equals(_pathSauTfs)) continue;

          Executor.AddCommand(new Command($"cd", @"C:\RM"));
          Executor.AddCommand(new Command($"{TFEXEPATH} workspaces /collection:https://totvstfs.visualstudio.com/DefaultCollection"));
          Executor.AddCommand(new Command($"{TFEXEPATH} merge /baseless /version:{_changeSet}~{_changeSet}", _pathSauTfs + " " + version + " " + _recursive));
        }
      }
    }

    private void ExecuteCommands()
    {
      Executor.Execute();
    }
  }
}