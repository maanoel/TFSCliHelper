using System.Diagnostics;
using System.Reflection;

namespace PEPCliHelper.Installer;

/// <summary>Janela única do instalador: destino, duas opções e um botão.</summary>
internal sealed class SetupForm : Form
{
  private static readonly Color HeaderBack = ColorTranslator.FromHtml("#0C1418");
  private static readonly Color Accent = ColorTranslator.FromHtml("#3FC1C9");

  private readonly Installer _installer;
  private readonly TextBox _destination = new();
  private readonly Button _browse = new();
  private readonly CheckBox _addToPath = new();
  private readonly CheckBox _createShortcut = new();
  private readonly Label _updateNote = new();
  private readonly ProgressBar _progress = new();
  private readonly Label _status = new();
  private readonly Button _primary = new();
  private readonly Button _secondary = new();
  private string? _installedExecutable;

  public SetupForm(Installer installer)
  {
    _installer = installer;
    BuildLayout();
    _destination.Text = installer.DefaultDestination;
    RefreshExistingInstallation();
  }

  private void BuildLayout()
  {
    SuspendLayout();
    AutoScaleMode = AutoScaleMode.Dpi;
    AutoScaleDimensions = new SizeF(96F, 96F);
    Text = "Instalar PEP CLI";
    Font = new Font("Segoe UI", 9F);
    FormBorderStyle = FormBorderStyle.FixedDialog;
    MaximizeBox = false;
    MinimizeBox = false;
    StartPosition = FormStartPosition.CenterScreen;
    ClientSize = new Size(520, 360);
    BackColor = SystemColors.Window;
    Icon = LoadIcon();

    Controls.Add(BuildHeader());

    var intro = new Label
    {
      Text = "Instala o PEP CLI para o seu usuário. Não requer administrador nem .NET instalado.",
      Location = new Point(24, 96),
      Size = new Size(472, 20),
    };

    var destinationLabel = new Label { Text = "Pasta de instalação", Location = new Point(24, 128), AutoSize = true };
    _destination.Location = new Point(24, 150);
    _destination.Size = new Size(378, 23);
    _destination.TextChanged += (_, _) => RefreshExistingInstallation();

    _browse.Text = "Alterar...";
    _browse.Location = new Point(410, 149);
    _browse.Size = new Size(86, 26);
    _browse.Click += (_, _) => BrowseDestination();

    _addToPath.Text = "Adicionar ao PATH do usuário (use 'pep' em qualquer terminal)";
    _addToPath.Checked = true;
    _addToPath.Location = new Point(24, 190);
    _addToPath.AutoSize = true;

    _createShortcut.Text = "Criar atalho no Menu Iniciar";
    _createShortcut.Checked = true;
    _createShortcut.Location = new Point(24, 216);
    _createShortcut.AutoSize = true;

    _updateNote.Text = "Sua configuração em %APPDATA%\\PepCli e o histórico são preservados.";
    _updateNote.ForeColor = SystemColors.GrayText;
    _updateNote.Location = new Point(24, 246);
    _updateNote.Size = new Size(472, 20);

    _progress.Style = ProgressBarStyle.Marquee;
    _progress.MarqueeAnimationSpeed = 30;
    _progress.Location = new Point(24, 274);
    _progress.Size = new Size(472, 8);
    _progress.Visible = false;

    _status.Location = new Point(24, 288);
    _status.Size = new Size(472, 22);
    _status.AutoEllipsis = true;

    _primary.Location = new Point(300, 318);
    _primary.Size = new Size(96, 28);
    _primary.Click += async (_, _) => await OnPrimaryAsync();

    _secondary.Text = "Cancelar";
    _secondary.Location = new Point(404, 318);
    _secondary.Size = new Size(92, 28);
    _secondary.Click += (_, _) => Close();

    AcceptButton = _primary;
    CancelButton = _secondary;
    Controls.AddRange([intro, destinationLabel, _destination, _browse, _addToPath, _createShortcut, _updateNote, _progress, _status, _primary, _secondary]);
    ResumeLayout(performLayout: true);
  }

  private Panel BuildHeader()
  {
    var header = new Panel { BackColor = HeaderBack, Location = new Point(0, 0), Size = new Size(520, 76) };
    var picture = new PictureBox
    {
      Location = new Point(20, 14),
      Size = new Size(48, 48),
      SizeMode = PictureBoxSizeMode.Zoom,
      Image = LoadIcon() is { } icon ? new Icon(icon, 48, 48).ToBitmap() : null,
    };
    var title = new Label
    {
      Text = "PEP CLI",
      ForeColor = Color.White,
      Font = new Font("Segoe UI Semibold", 16F),
      Location = new Point(80, 12),
      AutoSize = true,
    };
    var version = new Label
    {
      Text = $"Versão {Program.Version} · Equipe PEP RM",
      ForeColor = Accent,
      Location = new Point(82, 44),
      AutoSize = true,
    };
    header.Controls.AddRange([picture, title, version]);
    return header;
  }

  private static Icon? LoadIcon()
  {
    using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("assets.pep-cli.ico");
    return stream is null ? null : new Icon(stream);
  }

  private void BrowseDestination()
  {
    using var dialog = new FolderBrowserDialog
    {
      Description = "Escolha a pasta de instalação do PEP CLI",
      UseDescriptionForTitle = true,
      ShowNewFolderButton = true,
    };
    var current = _destination.Text.Trim();
    if (Directory.Exists(current)) dialog.InitialDirectory = current;
    else if (Directory.Exists(Path.GetDirectoryName(current))) dialog.InitialDirectory = Path.GetDirectoryName(current)!;
    if (dialog.ShowDialog(this) == DialogResult.OK) _destination.Text = dialog.SelectedPath;
  }

  private void RefreshExistingInstallation()
  {
    if (_installedExecutable is not null) return;
    var existing = Installer.IsExistingInstallation(_destination.Text.Trim());
    _primary.Text = existing ? "Atualizar" : "Instalar";
    _updateNote.Visible = existing;
  }

  private async Task OnPrimaryAsync()
  {
    if (_installedExecutable is not null)
    {
      OpenTerminal(_installedExecutable);
      return;
    }

    var options = new InstallOptions(_destination.Text, _addToPath.Checked, _createShortcut.Checked,
      _addToPath.Checked || _createShortcut.Checked, Program.Version);

    SetBusy(true);
    _status.ForeColor = SystemColors.ControlText;
    _status.Text = _primary.Text == "Atualizar" ? "Atualizando o PEP CLI..." : "Instalando o PEP CLI...";
    var result = await Task.Run(() => _installer.Install(options));
    SetBusy(false);

    if (!result.Success)
    {
      _status.ForeColor = Color.Firebrick;
      _status.Text = "Não foi possível concluir. Veja os detalhes.";
      MessageBox.Show(this, result.Message, "PEP CLI - erro na instalação", MessageBoxButtons.OK, MessageBoxIcon.Error);
      return;
    }

    _installedExecutable = result.ExecutablePath;
    _status.ForeColor = ColorTranslator.FromHtml("#1E7F5C");
    _status.Text = "Instalado. Abra um novo terminal e digite: pep";
    _destination.Enabled = _browse.Enabled = _addToPath.Enabled = _createShortcut.Enabled = false;
    _primary.Text = "Abrir terminal";
    _primary.Width = 110;
    _primary.Left = _secondary.Left - _primary.Width - 8;
    _secondary.Text = "Concluir";
    _secondary.Focus();
  }

  private void SetBusy(bool busy)
  {
    _progress.Visible = busy;
    _destination.Enabled = _browse.Enabled = _addToPath.Enabled = _createShortcut.Enabled = !busy;
    _primary.Enabled = _secondary.Enabled = !busy;
    ControlBox = !busy;
    UseWaitCursor = busy;
  }

  private void OpenTerminal(string executable)
  {
    var startInfo = new ProcessStartInfo
    {
      FileName = "powershell.exe",
      Arguments = PowerShellArguments.ForPep(executable, "version"),
      WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
      UseShellExecute = true,
    };
    try
    {
      Process.Start(startInfo)?.Dispose();
    }
    catch (System.ComponentModel.Win32Exception ex)
    {
      MessageBox.Show(this, $"Não foi possível abrir o PowerShell: {ex.Message}\nAbra um terminal e digite: pep",
        "PEP CLI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
  }

  protected override void OnFormClosing(FormClosingEventArgs e)
  {
    if (_progress.Visible) e.Cancel = true; // Não fecha no meio da cópia.
    base.OnFormClosing(e);
  }
}
