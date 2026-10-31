using System.Diagnostics;

namespace RecoWav;

public sealed class MainForm : Form
{
    private readonly NumericUpDown _hours = NewSpin(23);
    private readonly NumericUpDown _minutes = NewSpin(59);
    private readonly NumericUpDown _seconds = NewSpin(59);
    private readonly TextBox _folder = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly TextBox _fileName = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _bitrate = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
    private readonly Button _startButton = new() { Text = "● Grabar", AutoSize = true, Padding = new Padding(10, 4, 10, 4) };
    private readonly Button _stopButton = new() { Text = "■ Detener", AutoSize = true, Padding = new Padding(10, 4, 10, 4), Enabled = false };
    private readonly Button _openFolderButton = new() { Text = "Abrir carpeta", AutoSize = true, Padding = new Padding(10, 4, 10, 4) };
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Fill, Height = 22 };
    private readonly ProgressBar _level = new() { Dock = DockStyle.Fill, Height = 10, Maximum = 100 };
    private readonly Label _timeLabel = new() { AutoSize = true, Font = new Font("Segoe UI", 14f, FontStyle.Bold), Text = "00:00:00 / 00:00:00" };
    private readonly Label _status = new() { AutoSize = true, Text = "Listo. Elige la duración y pulsa Grabar." };

    private LoopbackRecorder? _recorder;
    private TimeSpan _duration;
    private string _outputPath = "";

    public MainForm()
    {
        Text = "RecoWav – Grabador de audio del sistema";
        Font = new Font("Segoe UI", 10f);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(14);

        _minutes.Value = 1;
        _bitrate.Items.AddRange(new object[] { "128 kbps", "192 kbps", "256 kbps", "320 kbps" });
        _bitrate.SelectedIndex = 1;
        _folder.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        _fileName.Text = DefaultFileName();

        BuildLayout();

        _startButton.Click += (_, _) => StartRecording();
        _stopButton.Click += (_, _) => { _status.Text = "Deteniendo..."; _recorder?.Stop(); };
        _openFolderButton.Click += (_, _) => OpenFolder();
        FormClosing += OnFormClosing;
    }

    private static NumericUpDown NewSpin(int max) => new()
    {
        Minimum = 0, Maximum = max, Width = 60, TextAlign = HorizontalAlignment.Center,
    };

    private static string DefaultFileName() => $"Grabacion_{DateTime.Now:yyyyMMdd_HHmmss}";

    private void BuildLayout()
    {
        var grid = new TableLayoutPanel
        {
            ColumnCount = 3, AutoSize = true, Dock = DockStyle.Fill, Width = 520,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        // Duración
        var durationPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        durationPanel.Controls.AddRange(new Control[]
        {
            _hours, LabelInline("h"), _minutes, LabelInline("min"), _seconds, LabelInline("s"),
        });
        AddRow(grid, "Duración:", durationPanel);

        // Atajos de duración
        var presets = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        foreach (var (label, span) in new[]
        {
            ("30 s", TimeSpan.FromSeconds(30)), ("1 min", TimeSpan.FromMinutes(1)),
            ("5 min", TimeSpan.FromMinutes(5)), ("15 min", TimeSpan.FromMinutes(15)),
            ("30 min", TimeSpan.FromMinutes(30)), ("1 h", TimeSpan.FromHours(1)),
        })
        {
            var b = new Button { Text = label, AutoSize = true, Margin = new Padding(0, 0, 4, 0) };
            b.Click += (_, _) => SetDuration(span);
            presets.Controls.Add(b);
        }
        AddRow(grid, "", presets);

        // Carpeta
        var browse = new Button { Text = "Examinar...", AutoSize = true };
        browse.Click += (_, _) => BrowseFolder();
        AddRow(grid, "Carpeta:", _folder, browse);

        // Nombre de archivo
        AddRow(grid, "Nombre:", _fileName, LabelInline(".mp3"));

        // Calidad
        AddRow(grid, "Calidad:", _bitrate);

        // Botones
        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 10, 0, 6) };
        buttons.Controls.AddRange(new Control[] { _startButton, _stopButton, _openFolderButton });
        grid.Controls.Add(buttons, 0, grid.RowCount);
        grid.SetColumnSpan(buttons, 3);
        grid.RowCount++;

        AddSpanning(grid, _timeLabel);
        AddSpanning(grid, _progress);
        AddSpanning(grid, LabelInline("Nivel de audio:"));
        AddSpanning(grid, _level);
        AddSpanning(grid, _status);

        Controls.Add(grid);
    }

    private static Label LabelInline(string text) => new()
    {
        Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 8, 0),
    };

    private static void AddRow(TableLayoutPanel grid, string caption, Control main, Control? extra = null)
    {
        int row = grid.RowCount++;
        grid.Controls.Add(new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 6) }, 0, row);
        grid.Controls.Add(main, 1, row);
        if (extra != null) grid.Controls.Add(extra, 2, row);
    }

    private static void AddSpanning(TableLayoutPanel grid, Control c)
    {
        int row = grid.RowCount++;
        c.Margin = new Padding(0, 4, 0, 4);
        grid.Controls.Add(c, 0, row);
        grid.SetColumnSpan(c, 3);
    }

    private void SetDuration(TimeSpan span)
    {
        _hours.Value = (int)span.TotalHours;
        _minutes.Value = span.Minutes;
        _seconds.Value = span.Seconds;
    }

    private void BrowseFolder()
    {
        using var dlg = new FolderBrowserDialog { SelectedPath = _folder.Text, Description = "Carpeta donde guardar los MP3" };
        if (dlg.ShowDialog(this) == DialogResult.OK) _folder.Text = dlg.SelectedPath;
    }

    private void OpenFolder()
    {
        if (Directory.Exists(_folder.Text))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_folder.Text}\"") { UseShellExecute = true });
    }

    private void StartRecording()
    {
        _duration = new TimeSpan((int)_hours.Value, (int)_minutes.Value, (int)_seconds.Value);
        if (_duration <= TimeSpan.Zero)
        {
            MessageBox.Show(this, "Indica una duración mayor que cero.", "Duración", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string name = _fileName.Text.Trim();
        if (name.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            MessageBox.Show(this, "El nombre de archivo no es válido.", "Nombre", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!Directory.Exists(_folder.Text))
        {
            MessageBox.Show(this, "La carpeta de destino no existe.", "Carpeta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _outputPath = Path.Combine(_folder.Text, name + ".mp3");
        if (File.Exists(_outputPath) &&
            MessageBox.Show(this, $"Ya existe \"{name}.mp3\". ¿Sobrescribir?", "Archivo existente",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _recorder = new LoopbackRecorder();
        _recorder.LevelChanged += level => BeginInvoke(() => _level.Value = (int)(level * 100));
        _recorder.ProgressChanged += t => BeginInvoke(() => UpdateProgress(t));
        _recorder.CaptureFinished += ex => BeginInvoke(() => OnCaptureFinished(ex));

        try
        {
            _recorder.Start(_duration);
        }
        catch (Exception ex)
        {
            _recorder.Dispose();
            _recorder = null;
            MessageBox.Show(this, "No se pudo iniciar la grabación:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        SetRecordingUi(true);
        UpdateProgress(TimeSpan.Zero);
        _status.Text = "Grabando el audio del sistema...";
    }

    private void UpdateProgress(TimeSpan recorded)
    {
        _timeLabel.Text = $"{recorded:hh\\:mm\\:ss} / {_duration:hh\\:mm\\:ss}";
        _progress.Value = (int)Math.Clamp(recorded.TotalMilliseconds / _duration.TotalMilliseconds * 100, 0, 100);
    }

    private async void OnCaptureFinished(Exception? captureError)
    {
        var recorder = _recorder;
        if (recorder == null) return;
        _level.Value = 0;

        if (captureError != null)
        {
            recorder.Dispose();
            _recorder = null;
            SetRecordingUi(false);
            _status.Text = "Error durante la grabación.";
            MessageBox.Show(this, "La grabación se interrumpió:\n" + captureError.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (recorder.Recorded <= TimeSpan.Zero)
        {
            recorder.Dispose();
            _recorder = null;
            SetRecordingUi(false);
            _status.Text = "No se grabó nada.";
            return;
        }

        _status.Text = "Convirtiendo a MP3...";
        _stopButton.Enabled = false;
        int bitrate = int.Parse(((string)_bitrate.SelectedItem!).Split(' ')[0]);
        string output = _outputPath;

        try
        {
            await Task.Run(() => recorder.SaveMp3(output, bitrate));
            _status.Text = $"Guardado: {output}  ({recorder.Recorded:hh\\:mm\\:ss})";
            _fileName.Text = DefaultFileName();
        }
        catch (Exception ex)
        {
            _status.Text = "Error al guardar el MP3.";
            MessageBox.Show(this, "No se pudo guardar el MP3:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            recorder.Dispose();
            _recorder = null;
            SetRecordingUi(false);
        }
    }

    private void SetRecordingUi(bool recording)
    {
        _startButton.Enabled = !recording;
        _stopButton.Enabled = recording;
        _hours.Enabled = _minutes.Enabled = _seconds.Enabled = !recording;
        _fileName.Enabled = _bitrate.Enabled = !recording;
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_recorder == null) return;
        if (MessageBox.Show(this, "Hay una grabación en curso. ¿Salir y descartarla?", "Salir",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            e.Cancel = true;
            return;
        }
        var r = _recorder;
        _recorder = null;
        r.Dispose();
    }
}
