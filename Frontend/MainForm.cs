using AsistenciaSync.Backend;

namespace AsistenciaSync;

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
        var subtitle = new Label { Text = "Control de asistencia · iClock 680 · SQL Server", AutoSize = true, Location = new Point(34, 78), ForeColor = Color.DimGray };
        var from = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 150, Value = settings.ReportFrom, Location = new Point(30, 135) };
        var to = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 150, Value = settings.ReportTo, Location = new Point(210, 135) };
        var fromLabel = new Label { Text = "Reporte desde", AutoSize = true, Location = new Point(30, 115) };
        var toLabel = new Label { Text = "Reporte hasta", AutoSize = true, Location = new Point(210, 115) };
        var report = MakeButton("Generar Reporte", Color.FromArgb(35, 91, 151));
        report.Location = new Point(30, 195); report.Width = 330; report.Height = 52;
        report.Click += async (_, _) =>
        {
            if (from.Value.Date > to.Value.Date) { MessageBox.Show(this, "La fecha inicial no puede ser posterior a la fecha final."); return; }
            settings.ReportFrom = from.Value.Date; settings.ReportTo = to.Value.Date; SettingsStore.Save(settings);
            try
            {
                var result = await Task.Run(() => ReportService.Generate(settings));
                using var viewer = new ReportForm(result.DetailPath, result.SummaryPath);
                viewer.ShowDialog(this);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "No se pudo generar el reporte", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };
        var config = MakeButton("Configuración del sistema", Color.FromArgb(80, 112, 145));
        config.Location = new Point(30, 275); config.Width = 330; config.Height = 48;
        config.Click += (_, _) => ShowSystemConfiguration();
        var parameters = MakeButton("Parámetros de sincronización", Color.FromArgb(80, 145, 112));
        parameters.Location = new Point(30, 335); parameters.Width = 330; parameters.Height = 48;
        parameters.Click += (_, _) => ShowReportParameters();
        var users = MakeButton("Configuración por usuario", Color.FromArgb(116, 91, 145));
        users.Location = new Point(30, 395); users.Width = 330; users.Height = 48;
        users.Click += (_, _) => OpenConfigured(() => new UserScheduleForm(settings));
        var day = MakeButton("Configuración del día", Color.FromArgb(160, 112, 55));
        day.Location = new Point(30, 455); day.Width = 330; day.Height = 48;
        day.Click += (_, _) => OpenConfigured(() => new DailyIncidentForm(settings));
        status.Location = new Point(34, 530);
        status.Text = settings.IsConfigured ? "Sistema configurado." : "Configure el sistema para habilitar la sincronización automática.";
        content.Controls.AddRange(new Control[] { title, subtitle, fromLabel, toLabel, from, to, report, config, parameters, users, day, status });
    }

    void OpenConfigured(Func<Form> create)
    {
        if (!settings.IsConfigured) { MessageBox.Show(this, "Primero configure y valide el servidor SQL en Configuración del sistema.", "Configuración requerida", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        try { using var form = create(); form.ShowDialog(this); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "No se pudo abrir la configuración", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    void ShowSystemConfiguration()
    {
        content.Controls.Clear();
        var title = PageTitle("Configuración del sistema");
        var fields = NewFields();
        var server = new TextBox { Text = settings.Server, Width = 220 };
        var database = new TextBox { Text = settings.Database, Width = 220 };
        var auth = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        auth.Items.AddRange(new object[] { "Windows Authentication", "SQL Server Authentication" });
        auth.SelectedIndex = settings.SqlAuthentication ? 1 : 0;
        var user = new TextBox { Text = settings.SqlUser, Width = 220 };
        var password = new TextBox { Text = settings.SqlPassword, Width = 220, UseSystemPasswordChar = true };
        var ip = new TextBox { Text = settings.DeviceIp, Width = 220 };
        var port = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = settings.DevicePort, Width = 220 };
        var setClock = new CheckBox { Text = "Sincronizar fecha y hora con este PC", AutoSize = true, Checked = settings.SyncClock };
        AddField(fields, "Servidor SQL", server); AddField(fields, "Base de datos", database); AddField(fields, "Autenticación", auth);
        AddField(fields, "Usuario SQL", user); AddField(fields, "Contraseña SQL", password); AddField(fields, "IP del dispositivo", ip); AddField(fields, "Puerto", port); AddField(fields, "Fecha y hora", setClock);
        void ToggleAuth() { var enabled = auth.SelectedIndex == 1; user.Enabled = enabled; password.Enabled = enabled; }
        auth.SelectedIndexChanged += (_, _) => ToggleAuth(); ToggleAuth();
        var accept = MakeButton("Aceptar y validar", Color.FromArgb(35, 91, 151)); accept.Location = new Point(30, 415); accept.Width = 220; accept.Height = 42;
        var back = MakeButton("Volver", Color.Gray); back.Location = new Point(270, 415); back.Width = 120; back.Height = 42;
        accept.Click += (_, _) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(server.Text) || string.IsNullOrWhiteSpace(database.Text) || string.IsNullOrWhiteSpace(ip.Text))
                    throw new InvalidOperationException("Complete servidor SQL, base de datos e IP del dispositivo.");
                if (auth.SelectedIndex == 1 && (string.IsNullOrWhiteSpace(user.Text) || string.IsNullOrWhiteSpace(password.Text)))
                    throw new InvalidOperationException("Complete el usuario y la contraseña de SQL Server.");
                using var device = new ZkDeviceClient(ip.Text.Trim(), (int)port.Value);
                if (setClock.Checked) device.SyncClock(DateTime.Now);
                _ = device.ReadDeviceTime();
                SqlStore.TestConnection(server.Text.Trim(), database.Text.Trim(), auth.SelectedIndex == 1, user.Text, password.Text);
                settings.Server = server.Text.Trim(); settings.Database = database.Text.Trim(); settings.DeviceIp = ip.Text.Trim(); settings.DevicePort = (int)port.Value;
                settings.SqlAuthentication = auth.SelectedIndex == 1; settings.SqlUser = user.Text.Trim(); settings.SqlPassword = password.Text; settings.SyncClock = setClock.Checked;
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
            status.Text = "Sincronizando datos...";
            using var device = new ZkDeviceClient(settings.DeviceIp, settings.DevicePort);
            if (settings.SyncClock) device.SyncClock(DateTime.Now);
            var records = await Task.Run(() => device.DownloadAttendance());
            await Task.Run(() => SqlStore.SaveEmployees(settings.Server, settings.Database, device.LastUsers, settings.SqlAuthentication, settings.SqlUser, settings.SqlPassword));
            var inserted = await Task.Run(() => SqlStore.Save(settings.Server, settings.Database, records, settings.SqlAuthentication, settings.SqlUser, settings.SqlPassword));
            status.Text = $"Sincronización correcta. Nuevos registros: {inserted:N0}.";
        }
        catch (Exception ex) { status.Text = "Error de sincronización: " + ex.Message; }
    }

    static TableLayoutPanel NewFields() { var p = new TableLayoutPanel { Location = new Point(30, 90), AutoSize = true, ColumnCount = 2 }; p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210)); p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); return p; }
    static void AddField(TableLayoutPanel panel, string label, Control control) { var row = panel.RowCount++; panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row); panel.Controls.Add(control, 1, row); }
    static Label PageTitle(string text) => new() { Text = text, Font = new Font("Segoe UI", 20, FontStyle.Bold), AutoSize = true, Location = new Point(30, 28) };

    private void InitializeComponent()
    {

    }

    static Button MakeButton(string text, Color color) => new() { Text = text, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold), FlatAppearance = { BorderSize = 0 } };
}
