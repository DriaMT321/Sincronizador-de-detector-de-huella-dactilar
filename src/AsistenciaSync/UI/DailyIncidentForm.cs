using AsistenciaSync.Configuration;
using AsistenciaSync.Models;
using AsistenciaSync.Services;

namespace AsistenciaSync.UI;

internal sealed partial class DailyIncidentForm : Form
{
    static readonly Color Primary = Color.FromArgb(35, 91, 151);
    static readonly Color Ink = Color.FromArgb(33, 37, 41);
    static readonly Color Muted = Color.FromArgb(107, 116, 128);

    readonly AppSettings settings;
    readonly ComboBox employee = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320, Font = new Font("Segoe UI", 10) };
    readonly TabControl tabs = new() { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10) };

    DateTime periodFrom;
    DateTime periodTo;

    public DailyIncidentForm(AppSettings settings)
    {
        this.settings = settings;
        Text = "Justificaciones / Faltas";
        Width = 980; Height = 740; MinimumSize = new Size(820, 640);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = true;
        BackColor = Color.White;
        var iconPath = Path.Combine(AppContext.BaseDirectory, "LOGO.ico");
        if (File.Exists(iconPath)) Icon = new Icon(iconPath);

        var earliest = CsvStore.EarliestPunchDate(settings) ?? DateTime.Today;
        periodFrom = new DateTime(earliest.Year, earliest.Month, 1);
        periodTo = DateTime.Today;

        var header = new Panel { Dock = DockStyle.Top, Height = 118, Padding = new Padding(28, 18, 28, 10), BackColor = Color.White };
        header.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 3, BackColor = Primary });
        var title = new Label { Text = "JUSTIFICACIONES / FALTAS", AutoSize = true, Font = new Font("Segoe UI", 19, FontStyle.Bold), ForeColor = Ink, Location = new Point(28, 14) };
        var subtitle = new Label { Text = $"Periodo analizado: {periodFrom:dd/MM/yyyy} – {periodTo:dd/MM/yyyy}", AutoSize = true, Font = new Font("Segoe UI", 9), ForeColor = Muted, Location = new Point(30, 50) };
        var employeeRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Location = new Point(28, 78) };
        employeeRow.Controls.Add(new Label { Text = "Empleado", AutoSize = true, ForeColor = Ink, Font = new Font("Segoe UI", 10, FontStyle.Bold), Margin = new Padding(0, 6, 8, 0) });
        employeeRow.Controls.Add(employee);
        header.Controls.AddRange(new Control[] { title, subtitle, employeeRow });

        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 62, Padding = new Padding(28, 12, 28, 12), BackColor = Color.White, FlowDirection = FlowDirection.RightToLeft };
        var close = MakeButton("Cerrar", Muted); close.Click += (_, _) => Close();
        actions.Controls.Add(close);

        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(28, 10, 28, 10), BackColor = Color.White };
        body.Controls.Add(tabs);
        tabs.TabPages.Add(BuildPendingTab());
        tabs.TabPages.Add(BuildFutureTab());
        tabs.TabPages.Add(BuildRegisteredTab());

        Controls.Add(body);
        Controls.Add(actions);
        Controls.Add(header);

        employee.SelectedIndexChanged += (_, _) => ReloadForEmployee();
        LoadEmployees();
    }

    void LoadEmployees()
    {
        employee.DataSource = AttendanceConfigurationStore.ReadEmployees(settings);
        employee.DisplayMember = nameof(EmployeeOption.Name);
        employee.ValueMember = nameof(EmployeeOption.Id);
        ReloadForEmployee();
    }

    EmployeeOption? Selected => employee.SelectedItem as EmployeeOption;

    void ReloadForEmployee()
    {
        LoadPending();
        LoadRegistered();
        RefreshSegmentChoices();
    }

    int SegmentCount(string employeeId)
    {
        var schedules = AttendanceConfigurationStore.ReadSchedules(settings);
        if (!schedules.TryGetValue(employeeId, out var schedule)) return 1;
        var type = AttendanceConfigurationStore.ReadWorkdayTypes(settings)
            .FirstOrDefault(t => t.Id.Equals(string.IsNullOrWhiteSpace(schedule.WorkdayTypeId) ? (schedule.Discontinuous ? "discontinua" : "continua") : schedule.WorkdayTypeId, StringComparison.OrdinalIgnoreCase));
        return type?.Segments.Count ?? (schedule.Discontinuous ? 2 : 1);
    }

    static string SegmentLabel(int? segment) => segment is null ? "Día completo" : $"Tramo {segment}";

    static string Hours(int minutes) => $"{minutes / 60}:{minutes % 60:00}";

    static Button MakeButton(string text, Color color) => new()
    {
        Text = text,
        BackColor = color,
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat,
        AutoSize = true,
        Height = 34,
        Font = new Font("Segoe UI", 9, FontStyle.Bold),
        FlatAppearance = { BorderSize = 0 },
        Margin = new Padding(6, 0, 0, 0),
        Padding = new Padding(14, 6, 14, 6)
    };

    static Label SectionLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI", 10, FontStyle.Bold),
        ForeColor = Primary,
        Margin = new Padding(0, 4, 0, 6)
    };

    static DataGridView MakeGrid() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToResizeColumns = false,
        AllowUserToResizeRows = false,
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
        RowHeadersVisible = false,
        MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle,
        EnableHeadersVisualStyles = false,
        Font = new Font("Segoe UI", 9),
        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(232, 238, 245), ForeColor = Ink, Font = new Font("Segoe UI", 9, FontStyle.Bold), Alignment = DataGridViewContentAlignment.MiddleLeft },
        DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Ink, SelectionBackColor = Color.FromArgb(220, 230, 240), SelectionForeColor = Ink },
        AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(248, 250, 252) }
    };
}
