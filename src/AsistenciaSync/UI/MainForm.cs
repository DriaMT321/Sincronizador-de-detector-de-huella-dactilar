using AsistenciaSync.Configuration;
using AsistenciaSync.Services;
using System.Globalization;

namespace AsistenciaSync.UI;

internal sealed class MainForm : Form
{
    readonly AppSettings settings = SettingsStore.Load();
    readonly Panel content = new() { Dock = DockStyle.Fill, Padding = new Padding(28), BackColor = Color.FromArgb(245, 247, 250) };
    readonly Label status = new() { AutoSize = true, ForeColor = Color.DimGray };

    public MainForm()
    {
        Text = "AsistenciaSync";
        Width = 760; Height = 650; MinimumSize = new Size(680, 580);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(245, 247, 250);
        var iconPath = Path.Combine(AppContext.BaseDirectory, "LOGO.ico");
        if (File.Exists(iconPath)) Icon = new Icon(iconPath);
        Controls.Add(content);
        ShowHome();
        Shown += async (_, _) =>
        {
            if (!settings.IsConfigured) BeginInvoke(new Action(ShowSystemConfiguration));
            else await SynchronizeAutomatically();
        };
    }

    void ShowHome()
    {
        content.Controls.Clear();
        var card = new Panel { Width = 500, Height = 520, BackColor = Color.White, Location = new Point((content.ClientSize.Width - 500) / 2, 18), Padding = new Padding(34) }; card.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 5, BackColor = Color.FromArgb(35, 91, 151) });
        var title = new Label { Text = "AsistenciaSync", Font = new Font("Segoe UI", 26, FontStyle.Bold), AutoSize = true, ForeColor = Color.FromArgb(24, 57, 92), Location = new Point(34, 32) };
        var subtitle = new Label { Text = "Control de asistencia · iClock 680 · CSV", AutoSize = true, Location = new Point(38, 78), ForeColor = Color.DimGray };
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var monthLabel = new Label { Text = $"Periodo: {monthStart.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("es-ES"))} · {monthStart:dd/MM}–{DateTime.Today:dd/MM}", AutoSize = true, Location = new Point(38, 112), ForeColor = Color.FromArgb(70, 80, 95) };
        var menu = new FlowLayoutPanel { Location = new Point(38, 150), Width = 424, Height = 300, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = false };
        var report = MakeButton("Hacer Reporte", Color.FromArgb(35, 91, 151)); report.Width = 424; report.Height = 46;
        report.Click += async (_, _) =>
        {
            var earliest = CsvStore.EarliestPunchDate(settings) ?? DateTime.Today; settings.ReportFrom = new DateTime(earliest.Year, earliest.Month, 1); settings.ReportTo = DateTime.Today; SettingsStore.Save(settings);
            try
            {
                var reportDocument = await Task.Run(() => ReportService.Build(settings));
                using var viewer = new ReportForm(reportDocument, settings);
                viewer.ShowDialog(this);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "No se pudo generar el reporte", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };
        var day = MakeButton("Justificaciones/Faltas", Color.FromArgb(160, 112, 55)); day.Width = 424; day.Height = 46;
        day.Click += (_, _) => OpenConfigured(() => new DailyIncidentForm(settings));
        var customize = MakeButton("Personalización y mantenimiento", Color.FromArgb(105, 82, 130)); customize.Width = 424; customize.Height = 46;
        customize.Click += (_, _) => OpenConfigured(() => new CustomizationForm(settings));
        var config = MakeButton("Configuración del programa", Color.FromArgb(80, 112, 145)); config.Width = 424; config.Height = 46; config.Click += (_, _) => ShowSystemConfiguration();
        menu.Controls.AddRange(new Control[] { report, day, customize, config });
        status.Location = new Point(38, 475);
        status.Text = settings.IsConfigured ? "Sistema configurado." : "Configure el sistema para habilitar la sincronización automática.";
        card.Controls.AddRange(new Control[] { title, subtitle, monthLabel, menu, status }); content.Controls.Add(card);
    }

    void OpenConfigured(Func<Form> create)
    {
        if (!settings.IsConfigured) { MessageBox.Show(this, "Primero configure el dispositivo y la carpeta CSV en Configuración del programa.", "Configuración requerida", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        try { using var form = create(); form.ShowDialog(this); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "No se pudo abrir la configuración", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    void ShowSystemConfiguration()
    {
        content.Controls.Clear();
        var title = PageTitle("Configuración del programa");
        var fields = NewFields();
        var ip = new TextBox { Text = settings.DeviceIp, Width = 220 };
        var port = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = settings.DevicePort, Width = 220 };
        var folder = new TextBox { Text = settings.CsvFolder, Width = 220 }; var folderPicker = new FlowLayoutPanel { AutoSize = true, WrapContents = false }; var browse = MakeButton("Examinar", Color.FromArgb(105, 105, 105)); browse.Width = 100; browse.Height = 30; browse.Click += (_, _) => { using var dialog = new FolderBrowserDialog { SelectedPath = Directory.Exists(folder.Text) ? folder.Text : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Description = "Seleccione dónde guardar los archivos CSV" }; if (dialog.ShowDialog(this) == DialogResult.OK) folder.Text = dialog.SelectedPath; }; folderPicker.Controls.Add(folder); folderPicker.Controls.Add(browse);
        var setClock = new CheckBox { Text = "Sincronizar fecha y hora con este PC", AutoSize = true, Checked = settings.SyncClock };
        AddField(fields, "IP del dispositivo", ip); AddField(fields, "Puerto", port); AddField(fields, "Carpeta CSV", folderPicker); AddField(fields, "Fecha y hora", setClock);
        var accept = MakeButton("Aceptar y validar", Color.FromArgb(35, 91, 151)); accept.Location = new Point(30, 415); accept.Width = 220; accept.Height = 42;
        var back = MakeButton("Volver", Color.Gray); back.Location = new Point(270, 415); back.Width = 120; back.Height = 42;
        var sync = MakeButton("Sincronizar ahora", Color.FromArgb(80, 145, 112)); sync.Location = new Point(30, 475); sync.Width = 220; sync.Height = 42;
        accept.Click += (_, _) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ip.Text) || string.IsNullOrWhiteSpace(folder.Text)) throw new InvalidOperationException("Complete la IP del dispositivo y la carpeta CSV.");
                using var device = new ZkDeviceClient(ip.Text.Trim(), (int)port.Value);
                if (setClock.Checked) device.SyncClock(DateTime.Now);
                _ = device.ReadDeviceTime();
                Directory.CreateDirectory(folder.Text.Trim()); settings.DeviceIp = ip.Text.Trim(); settings.DevicePort = (int)port.Value; settings.CsvFolder = folder.Text.Trim(); settings.SyncClock = setClock.Checked;
                SettingsStore.Save(settings); ShowHome();
                MessageBox.Show(this, "Configuración validada y guardada correctamente.", "Configuración", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "No se pudo validar", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };
        sync.Click += async (_, _) => { await SynchronizeAutomatically(); MessageBox.Show(this, status.Text, "Sincronización", MessageBoxButtons.OK, status.Text.StartsWith("Sincronización CSV correcta", StringComparison.OrdinalIgnoreCase) ? MessageBoxIcon.Information : MessageBoxIcon.Error); };
        back.Click += (_, _) => ShowHome();
        content.Controls.AddRange(new Control[] { title, fields, accept, back, sync });
    }

    async Task SynchronizeAutomatically()
    {
        try
        {
            status.Text = "Sincronizando datos en CSV...";
            using var device = new ZkDeviceClient(settings.DeviceIp, settings.DevicePort);
            if (settings.SyncClock) device.SyncClock(DateTime.Now);
            var records = await Task.Run(() => device.DownloadAttendance());
            await Task.Run(() => CsvStore.SaveEmployees(settings, device.LastUsers));
            var inserted = await Task.Run(() => CsvStore.Save(settings, records));
            status.Text = $"Sincronización CSV correcta. Nuevos registros: {inserted:N0}.";
        }
        catch (Exception ex) { status.Text = "Error de sincronización: " + ex.Message; }
    }

    static TableLayoutPanel NewFields() { var p = new TableLayoutPanel { Location = new Point(30, 90), AutoSize = true, ColumnCount = 2 }; p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210)); p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); return p; }
    static void AddField(TableLayoutPanel panel, string label, Control control) { var row = panel.RowCount++; panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row); panel.Controls.Add(control, 1, row); }
    static Label PageTitle(string text) => new() { Text = text, Font = new Font("Segoe UI", 20, FontStyle.Bold), AutoSize = true, Location = new Point(30, 28) };

    static Button MakeButton(string text, Color color) => new() { Text = text, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold), FlatAppearance = { BorderSize = 0 } };
}
