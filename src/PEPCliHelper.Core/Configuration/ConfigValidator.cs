using System.Text.RegularExpressions;
using PEPCliHelper.Core.Common;

namespace PEPCliHelper.Core.Configuration;

public static partial class ConfigValidator
{
  public const int MaxActiveLegacy = 4;

  public static IReadOnlyList<string> Validate(PepConfig config)
  {
    var errors = new List<string>();

    if (string.IsNullOrWhiteSpace(config.LocalRoot) || !Path.IsPathFullyQualified(config.LocalRoot))
      errors.Add("raizLocal: informe um caminho local absoluto (ex.: C:\\Linha-RM).");
    else if (TfvcPath.IsServerPath(config.LocalRoot))
      errors.Add("raizLocal: contém caminho de servidor TFVC; use um diretório local.");

    if (config.Collection is not null
      && (!Uri.TryCreate(config.Collection, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)))
      errors.Add("colecao: informe a URL completa da coleção TFVC (https://...).");

    if (!TfvcPath.IsServerPath(config.ServerRoot) || config.ServerRoot.Contains('\\') || config.ServerRoot.Contains(':'))
      errors.Add("raizServidor: informe o caminho TFVC iniciando com '$/' (ex.: $/Linha-RM).");

    ValidateProjects(config, errors);
    ValidateVersions(config, errors);
    ValidateLocalFiles(config.LocalFiles, errors);

    if (config.Tools is null)
      errors.Add("ferramentas: seção obrigatória.");

    return errors;
  }

  private static void ValidateProjects(PepConfig config, List<string> errors)
  {
    if (config.Projects.Count == 0)
    {
      errors.Add("projetos: cadastre ao menos um projeto (ex.: back → Sau-PEP).");
      return;
    }

    var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var (project, index) in config.Projects.Select((p, i) => (p, i)))
    {
      var where = $"projetos[{index}]";
      if (!AliasRegex().IsMatch(project.Alias ?? string.Empty))
        errors.Add($"{where}.alias: use letras minúsculas, números ou hífen (ex.: back).");
      else if (!aliases.Add(project.Alias!))
        errors.Add($"{where}.alias: '{project.Alias}' está duplicado.");

      if (string.IsNullOrWhiteSpace(project.Name))
        errors.Add($"{where}.nome: obrigatório.");

      if (!IsSafeRelative(project.LocalFolder))
        errors.Add($"{where}.pastaLocal: informe uma pasta relativa à versão, sem '..' (ex.: Sau-PEP).");

      if (string.IsNullOrWhiteSpace(project.ServerFolder) || project.ServerFolder.Contains('\\')
        || TfvcPath.IsServerPath(project.ServerFolder) || project.ServerFolder.Contains(':'))
        errors.Add($"{where}.pastaServidor: informe o trecho relativo ao caminho de servidor da versão, com '/' (ex.: Sau-PEP).");

      if (project.Solution is not null && !IsSafeRelative(project.Solution))
        errors.Add($"{where}.solucao: informe caminho relativo à pasta do projeto, sem '..'.");
    }

    var principals = config.Projects.Where(p => p.Principal).ToList();
    if (principals.Count > 1)
      errors.Add($"projetos: {principals.Count} projetos marcados como principal ({string.Join(", ", principals.Select(p => p.Alias))}); apenas um é permitido.");
  }

  private static void ValidateVersions(PepConfig config, List<string> errors)
  {
    var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    foreach (var (version, index) in config.Versions.Select((v, i) => (v, i)))
    {
      var where = $"versoes[{index}]";
      if (string.IsNullOrWhiteSpace(version.Id))
      {
        errors.Add($"{where}.id: obrigatório (ex.: 12.1.2606).");
        continue;
      }

      if (!ids.Add(version.Id))
        errors.Add($"{where}.id: '{version.Id}' está duplicado.");

      if (string.IsNullOrWhiteSpace(version.LocalPath))
        errors.Add($"{where}.caminhoLocal: obrigatório.");
      else if (TfvcPath.IsServerPath(version.LocalPath))
        errors.Add($"{where}.caminhoLocal: contém caminho de servidor TFVC ('{version.LocalPath}'); use o diretório local.");
      else if (!Path.IsPathFullyQualified(version.LocalPath) && version.LocalPath.Split('\\', '/').Contains(".."))
        errors.Add($"{where}.caminhoLocal: não use '..'.");

      if (!TfvcPath.IsServerPath(version.ServerPath) || version.ServerPath.Contains('\\') || version.ServerPath.Contains(':'))
        errors.Add($"{where}.caminhoServidor: informe o caminho TFVC iniciando com '$/' (ex.: $/Linha-RM/Legado/12.1.2606).");

      foreach (var name in version.Aliases.Prepend(version.Id))
      {
        if (string.IsNullOrWhiteSpace(name))
        {
          errors.Add($"{where}.aliases: alias vazio.");
          continue;
        }

        if (names.TryGetValue(name, out var owner) && !owner.Equals(version.Id, StringComparison.OrdinalIgnoreCase))
          errors.Add($"{where}.aliases: '{name}' já identifica a versão '{owner}'.");
        else
          names[name] = version.Id;
      }
    }

    if (config.Versions.Count == 0)
      return;

    var currents = config.Versions.Where(v => v.IsCurrent).ToList();
    if (currents.Count == 0)
      errors.Add("versoes: marque exatamente uma versão como atual.");
    else if (currents.Count > 1)
      errors.Add($"versoes: {currents.Count} versões marcadas como atual ({string.Join(", ", currents.Select(v => v.Id))}); apenas uma é permitida.");
    else if (!currents[0].Active)
      errors.Add($"versoes: a versão atual '{currents[0].Id}' não pode estar desativada.");

    var activeLegacy = config.Versions.Count(v => v.Active && !v.IsCurrent);
    if (activeLegacy > MaxActiveLegacy)
      errors.Add($"versoes: {activeLegacy} legadas ativas; o máximo é {MaxActiveLegacy}. Desative as que não estão em uso (\"ativa\": false).");
  }

  private static void ValidateLocalFiles(LocalFilesConfig files, List<string> errors)
  {
    if (files is null)
    {
      errors.Add("arquivosLocais: seção obrigatória.");
      return;
    }

    foreach (var (name, value) in new[]
    {
      ("broker", files.Broker), ("host", files.Host), ("rm", files.Rm), ("alias", files.Alias), ("hostConfig", files.HostConfig),
    })
    {
      if (!IsSafeRelative(value))
        errors.Add($"arquivosLocais.{name}: informe caminho relativo à pasta da versão, sem '..'.");
    }
  }

  private static bool IsSafeRelative(string? value) =>
    !string.IsNullOrWhiteSpace(value)
    && !Path.IsPathRooted(value)
    && !TfvcPath.IsServerPath(value)
    && !value.Split('\\', '/').Contains("..");

  [GeneratedRegex("^[a-z0-9][a-z0-9-]*$")]
  private static partial Regex AliasRegex();
}
