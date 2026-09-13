using PEPCliHelper.Core.FileSystem;

namespace PEPCliHelper.Tests.Fakes;

public sealed class InMemoryFileSystem : IFileSystem
{
  private readonly Dictionary<string, (string Content, bool ReadOnly)> _files = new(StringComparer.OrdinalIgnoreCase);
  private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

  public HashSet<string> LockedFiles { get; } = new(StringComparer.OrdinalIgnoreCase);

  public List<string> Deleted { get; } = [];

  public InMemoryFileSystem AddDirectory(string path)
  {
    var current = Path.GetFullPath(path).TrimEnd('\\');
    while (!string.IsNullOrEmpty(current))
    {
      _directories.Add(current);
      var parent = Path.GetDirectoryName(current);
      if (parent is null || parent == current)
        break;
      current = parent.TrimEnd('\\');
    }

    return this;
  }

  public InMemoryFileSystem AddFile(string path, string content = "", bool readOnly = true)
  {
    AddDirectory(Path.GetDirectoryName(path)!);
    _files[Path.GetFullPath(path)] = (content, readOnly);
    return this;
  }

  public bool FileExists(string path) => _files.ContainsKey(Path.GetFullPath(path));

  public bool DirectoryExists(string path) => _directories.Contains(Path.GetFullPath(path).TrimEnd('\\'));

  public IReadOnlyList<string> GetDirectories(string path)
  {
    var root = Path.GetFullPath(path).TrimEnd('\\') + "\\";
    return _directories.Where(d => d.StartsWith(root, StringComparison.OrdinalIgnoreCase) && !d[root.Length..].Contains('\\')).ToList();
  }

  public string ReadAllText(string path) => _files[Path.GetFullPath(path)].Content;

  public void WriteAllTextAtomic(string path, string content) => AddFile(path, content, readOnly: false);

  public void CopyFile(string source, string destination)
  {
    if (FileExists(destination))
      throw new IOException("Destino já existe.");
    AddFile(destination, ReadAllText(source), readOnly: false);
  }

  public void DeleteFile(string path)
  {
    _files.Remove(Path.GetFullPath(path));
    Deleted.Add(path);
  }

  public bool IsReadOnly(string path) => _files[Path.GetFullPath(path)].ReadOnly;

  public bool IsLocked(string path) => LockedFiles.Contains(Path.GetFullPath(path));

  public IReadOnlyList<string> GetFiles(string directory, string pattern)
  {
    var root = Path.GetFullPath(directory).TrimEnd('\\') + "\\";
    var extension = pattern.TrimStart('*');
    return _files.Keys
      .Where(f => f.StartsWith(root, StringComparison.OrdinalIgnoreCase) && !f[root.Length..].Contains('\\') && f.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
      .ToList();
  }

  public long? GetAvailableFreeSpace(string path) => 100L * 1024 * 1024 * 1024;

  public void EnsureDirectory(string path) => AddDirectory(path);
}
