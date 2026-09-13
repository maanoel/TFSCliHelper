namespace PEPCliHelper.Core.FileSystem;

public sealed class PhysicalFileSystem : IFileSystem
{
  public bool FileExists(string path) => File.Exists(path);

  public bool DirectoryExists(string path) => Directory.Exists(path);

  public IReadOnlyList<string> GetDirectories(string path) =>
    Directory.Exists(path) ? Directory.GetDirectories(path) : [];

  public string ReadAllText(string path) => File.ReadAllText(path);

  public void WriteAllTextAtomic(string path, string content)
  {
    var directory = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(directory))
      Directory.CreateDirectory(directory);

    var temp = path + ".tmp";
    File.WriteAllText(temp, content);
    File.Move(temp, path, overwrite: true);
  }

  public void CopyFile(string source, string destination) => File.Copy(source, destination, overwrite: false);

  public void DeleteFile(string path) => File.Delete(path);

  public bool IsReadOnly(string path) => new FileInfo(path).IsReadOnly;

  public bool IsLocked(string path)
  {
    try
    {
      using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
      return false;
    }
    catch (IOException)
    {
      return true;
    }
    catch (UnauthorizedAccessException)
    {
      return true;
    }
  }

  public IReadOnlyList<string> GetFiles(string directory, string pattern) =>
    Directory.Exists(directory) ? Directory.GetFiles(directory, pattern) : [];

  public long? GetAvailableFreeSpace(string path)
  {
    try
    {
      var root = Path.GetPathRoot(Path.GetFullPath(path));
      return string.IsNullOrEmpty(root) ? null : new DriveInfo(root).AvailableFreeSpace;
    }
    catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
    {
      return null;
    }
  }

  public void EnsureDirectory(string path) => Directory.CreateDirectory(path);
}
