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
            if (settings.IsConfigured)
                await SynchronizeAutomatically();
        };
    }

    void ShowHome()
    {
        content.Controls.Clear();
        var title = new Label { Text = "AsistenciaSync", Font = new Font("Segoe UI", 26, FontStyle.Bold), AutoSize = true, Location = new Point(30, 28) };
        var subtitle = new Label { Text = "Control de asistencia · iClock 680 · archivos CSV", AutoSize = true, Location = new Point(34, 78), ForeColor = Color.DimGray };
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var monthLabel = new Label { Text = $"Periodo del reporte: {monthStart.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("es-ES"))} (del {monthStart:dd/MM} al {DateTime.Today:dd/MM})", AutoSize = true, Location = new Point(34, 120), ForeColor = Color.FromArgb(70, 80, 95) };
        var report = MakeButton("Ver Reporte", Color.FromArgb(35, 91, 151));
        report.Location = new Point(30, 165); report.Width = 330; report.Height = 52;
        report.Click += async (_, _) =>
        {
            settings.ReportFrom = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); settings.ReportTo = DateTime.Today; SettingsStore.Save(settings);
            try
            {
                var reportDocument = await Task.Run(() => ReportService.Build(settings));
                using var viewer = new ReportForm(reportDocument, settings);
                viewer.ShowDialog(this);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "No se pudo generar el reporte", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };
        var users = MakeButton("Configuración por usuario", Color.FromArgb(116, 91, 145));
        users.Location = new Point(30, 235); users.Width = 330; users.Height = 48;
        users.Click += (_, _) => OpenConfigured(() => new UserScheduleForm(settings));
        var day = MakeButton("Configuración del día", Color.FromArgb(160, 112, 55));
        day.Location = new Point(30, 295); day.Width = 330; day.Height = 48;
        day.Click += (_, _) => OpenConfigured(() => new DailyIncidentForm(settings));
        var parameters = MakeButton("Parámetros de sincronización", Color.FromArgb(80, 145, 112));
        parameters.Location = new Point(30, 355); parameters.Width = 330; parameters.Height = 48;
        parameters.Click += (_, _) => ShowReportParameters();
        var config = MakeButton("Configuración del programa", Color.FromArgb(80, 112, 145));
        config.Location = new Point(30, 425); config.Width = 330; config.Height = 48;
        config.Click += (_, _) => ShowSystemConfiguration();
        var customize = MakeButton("Personalización y mantenimiento", Color.FromArgb(105, 82, 130));
        customize.Location = new Point(380, 425); customize.Width = 300; customize.Height = 48;
        customize.Click += (_, _) => OpenConfigured(() => new CustomizationForm(settings));
        status.Location = new Point(34, 500);
        status.Text = settings.IsConfigured ? "Sistema configurado." : "Configure el sistema para habilitar la sincronización automática.";
        content.Controls.AddRange(new Control[] { title, subtitle, monthLabel, report, users, day, parameters, config, customize, status });
    }

    void OpenConfigured(Func<Form> create)
    {
        if (!settings.IsConfigured) { MessageBox.Show(this, "Primero configure el dispositivo y la carpeta CSV en Configuración del sistema.", "Configuración requerida", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        try { using var form = create(); form.ShowDialog(this); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "No se pudo abrir la configuración", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    void ShowSystemConfiguration()
    {
        content.Controls.Clear();
        var title = PageTitle("Configuración del sistema");
        var fields = NewFields();
        var ip = new TextBox { Text = settings.DeviceIp, Width = 220 };
        var port = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = settings.DevicePort, Width = 220 };
        var folder = new TextBox { Text = settings.CsvFolder, Width = 220 };
        var setClock = new CheckBox { Text = "Sincronizar fecha y hora con este PC", AutoSize = true, Checked = settings.SyncClock };
        AddField(fields, "IP del dispositivo", ip); AddField(fields, "Puerto", port); AddField(fields, "Carpeta CSV", folder); AddField(fields, "Fecha y hora", setClock);
        var accept = MakeButton("Aceptar y validar", Color.FromArgb(35, 91, 151)); accept.Location = new Point(30, 415); accept.Width = 220; accept.Height = 42;
        var back = MakeButton("Volver", Color.Gray); back.Location = new Point(270, 415); back.Width = 120; back.Height = 42;
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
        back.Click += (_, _) => ShowHome();
        content.Controls.AddRange(new Control[] { title, fields, accept, back });
    }

    void ShowReportParameters()
    {
        content.Controls.Clear();
        var title = PageTitle("Parámetros de sincronización y reporte");
        var fields = NewFields();
        var entry = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 220, Value = DateTime.Today.Add(settings.EntryTime) };
        var exit = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 220, Value = DateTime.Today.Add(settings.ExitTime) };
        AddField(fields, "Hora de ingreso", entry); AddField(fields, "Hora de salida", exit);
        var save = MakeButton("Guardar parámetros", Color.FromArgb(80, 112, 145)); save.Location = new Point(30, 190); save.Width = 220; save.Height = 42;
        var back = MakeButton("Volver", Color.Gray); back.Location = new Point(270, 190); back.Width = 120; back.Height = 42;
        save.Click += async (_, _) =>
        {
            settings.EntryTime = entry.Value.TimeOfDay; settings.ExitTime = exit.Value.TimeOfDay; SettingsStore.Save(settings);
            await SynchronizeAutomatically(); ShowHome();
        };
        back.Click += (_, _) => ShowHome();
        content.Controls.AddRange(new Control[] { title, fields, save, back });
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
