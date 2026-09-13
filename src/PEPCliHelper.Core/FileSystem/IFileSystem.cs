namespace PEPCliHelper.Core.FileSystem;

public interface IFileSystem
{
  bool FileExists(string path);

  bool DirectoryExists(string path);

  IReadOnlyList<string> GetDirectories(string path);

  string ReadAllText(string path);

  /// <summary>Grava em arquivo temporário e substitui o destino.</summary>
  void WriteAllTextAtomic(string path, string content);

  void CopyFile(string source, string destination);

  void DeleteFile(string path);

  bool IsReadOnly(string path);

  /// <summary>True se o arquivo não pode ser aberto com acesso exclusivo.</summary>
  bool IsLocked(string path);

  IReadOnlyList<string> GetFiles(string directory, string pattern);

  long? GetAvailableFreeSpace(string path);

  void EnsureDirectory(string path);
}
