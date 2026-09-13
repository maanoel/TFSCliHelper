namespace PEPCliHelper.Infrastructure;

/// <summary>
/// Ctrl+C cancela a operação em andamento (o processo filho é encerrado e o estado é inspecionado).
/// Um segundo Ctrl+C durante o cancelamento encerra o CLI.
/// </summary>
public sealed class ConsoleCancellation : IDisposable
{
  private readonly bool _listen;
  private CancellationTokenSource _source = new();

  public ConsoleCancellation(bool listen)
  {
    _listen = listen;
    if (_listen)
      Console.CancelKeyPress += OnCancelKeyPress;
  }

  public CancellationToken Token => _source.Token;

  /// <summary>Usado pelo menu para seguir após uma operação cancelada.</summary>
  public void Reset()
  {
    if (!_source.IsCancellationRequested)
      return;

    var previous = _source;
    _source = new CancellationTokenSource();
    previous.Dispose();
  }

  public void Dispose()
  {
    if (_listen)
      Console.CancelKeyPress -= OnCancelKeyPress;
    _source.Dispose();
  }

  private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
  {
    if (_source.IsCancellationRequested)
    {
      e.Cancel = false;
      return;
    }

    e.Cancel = true;
    _source.Cancel();
  }
}
