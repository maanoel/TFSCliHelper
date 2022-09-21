using System.Collections.Generic;

namespace PEPCliHelper
{
  public class MergeBuilder : ICommandBuilder
  {
    private string TFEXEPATH = @"""C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\TF.exe""";
    private string _pathFrontTfs;
    private string _pathSauTfs;
    private string _pathBackTfs;
    private string _changeSet;
    private string _version;
    private string _project;
    private readonly string _recursive = "/recursive";
    private List<string> _versions;
    private List<string> _pathsFront;
    private List<string> _pathsBack;
    private List<string> _pathsSau;

    public ICommandExecutor Executor { get; private set; }

    public MergeBuilder()
    {
      Executor = new TfsCommandExecutor();

      _pathsFront = new List<string> {
        StructTFSServerPathFront._32,
        StructTFSServerPathFront._33,
        StructTFSServerPathFront._34,
        StructTFSServerPathFront._2205,
        StructTFSServerPathFront._2209,
        StructTFSServerPathFront._2209,
        StructTFSServerPathFront.atual,

      };

      _pathsBack = new List<string> {
        StructTFSServerPathBack._32,
        StructTFSServerPathBack._33,
        StructTFSServerPathBack._34,
        StructTFSServerPathBack._2205,
        StructTFSServerPathBack._2209,
        StructTFSServerPathBack.atual,
      };

      _pathsSau = new List<string> {
        StructTFSServerPathSau._32,
        StructTFSServerPathSau._33,
        StructTFSServerPathSau._34,
        StructTFSServerPathSau._2205,
        StructTFSServerPathSau._2209,
        StructTFSServerPathSau.atual,
      };
    }

    public void Build(string command)
    {
      PrepareCommand(command);
      GetVersionFiles();
      ExecuteCommands();
    }

    private void PrepareCommand(string command)
    {
      var commandSplit = command.Split(" ");
      _changeSet = commandSplit[3];
      _version = commandSplit[2];
      _project = commandSplit[1];

      switch (_version)
      {
        case string a when _version.Equals("32"):
          _pathFrontTfs = StructTFSServerPathFront._32;
          _pathBackTfs = StructTFSServerPathBack._32;
          _pathSauTfs = StructTFSServerPathSau._32;
          break;
        case string a when _version.Equals("33"):
          _pathFrontTfs = StructTFSServerPathFront._33;
          _pathBackTfs = StructTFSServerPathBack._33;
          _pathSauTfs = StructTFSServerPathSau._33;
          break;
        case string a when _version.Equals("34"):
          _pathFrontTfs = StructTFSServerPathFront._34;
          _pathBackTfs = StructTFSServerPathBack._34;
          _pathSauTfs = StructTFSServerPathSau._34;
          break;
        case string a when _version.Equals("2205"):
          _pathFrontTfs = StructTFSServerPathFront._2205;
          _pathBackTfs = StructTFSServerPathBack._2205;
          _pathSauTfs = StructTFSServerPathSau._2205;
          break;
        case string a when _version.Equals("2209"):
          _pathFrontTfs = StructTFSServerPathFront._2209;
          _pathBackTfs = StructTFSServerPathBack._2209;
          _pathSauTfs = StructTFSServerPathSau._2209;
          break;
        case string a when _version.Equals("atual"):
          _pathFrontTfs = StructTFSServerPathFront.atual;
          _pathBackTfs = StructTFSServerPathBack.atual;
          _pathSauTfs = StructTFSServerPathSau.atual;
          break;
        default:
          throw new InvalidCommandException("Command not implemented.");
      }
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

          Executor.AddCommand(new Command($"cd", @"C:\RM"));
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