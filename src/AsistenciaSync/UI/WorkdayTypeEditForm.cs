using AsistenciaSync.Models;

namespace AsistenciaSync.UI;

internal sealed class WorkdayTypeEditForm : Form
{
    readonly TextBox name = new() { Dock = DockStyle.Fill };
    readonly DataGridView segments = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeColumns = false,
        AllowUserToResizeRows = false,
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        BackgroundColor = Color.White
    };
    readonly CheckBox lunchEnabled = new() { Text = "Permitir salida opcional para almorzar", AutoSize = true };
    readonly TextBox lunchStart = ClockBox();
    readonly TextBox lunchEnd = ClockBox();
    readonly string id;

    public WorkdayType? Result { get; private set; }

    public WorkdayTypeEditForm(string id, WorkdayType? current = null)
    {
        this.id = id;
        Text = current is null ? "Añadir tipo de jornada" : "Editar tipo de jornada";
        Width = 680; Height = 620; MinimumSize = new Size(600, 520); StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = true; MinimizeBox = false;
        var iconPath = Path.Combine(AppContext.BaseDirectory, "LOGO.ico"); if (File.Exists(iconPath)) Icon = new Icon(iconPath);

        segments.Columns.Add("Entry", "Ingreso"); segments.Columns.Add("Exit", "Salida");
        segments.EditingControlShowing += (_, e) => { if (e.Control is TextBox box) { box.KeyPress -= DigitsOnly; box.KeyPress += DigitsOnly; } };
        name.Text = current?.Name ?? "";
        foreach (var segment in current?.Segments ?? new[] { new WorkSegment(new TimeSpan(8, 30, 0), new TimeSpan(17, 0, 0)) }) segments.Rows.Add(Clock(segment.Entry), Clock(segment.Exit));
        lunchEnabled.Checked = current?.Lunch is not null; lunchStart.Text = current?.Lunch is null ? "" : Clock(current.Lunch.Start); lunchEnd.Text = current?.Lunch is null ? "" : Clock(current.Lunch.End);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 6 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        var nameRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 }; nameRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135)); nameRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); nameRow.Controls.Add(new Label { Text = "Nombre", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 10, FontStyle.Bold) }, 0, 0); nameRow.Controls.Add(name, 1, 0);
        var segmentHost = new GroupBox { Text = "Tramos obligatorios de trabajo", Dock = DockStyle.Fill, Padding = new Padding(10) }; segmentHost.Controls.Add(segments);
        var segmentBar = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 0) }; var add = Button("Añadir tramo", Color.FromArgb(80, 145, 112)); add.Click += (_, _) => AddSegment(); var remove = Button("Quitar tramo", Color.FromArgb(170, 75, 75)); remove.Click += (_, _) => RemoveSegment(); segmentBar.Controls.AddRange(new Control[] { add, remove });
        var lunch = new GroupBox { Text = "Horario de almuerzo (opcional)", Dock = DockStyle.Fill, Padding = new Padding(12) }; var lunchLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2 }; lunchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); lunchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); lunchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90)); lunchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); lunchLayout.Controls.Add(lunchEnabled, 0, 0); lunchLayout.SetColumnSpan(lunchEnabled, 4); lunchLayout.Controls.Add(new Label { Text = "Desde", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1); lunchLayout.Controls.Add(lunchStart, 1, 1); lunchLayout.Controls.Add(new Label { Text = "Hasta", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 1); lunchLayout.Controls.Add(lunchEnd, 3, 1); lunch.Controls.Add(lunchLayout);
        var help = new Label { Text = "Use cuatro números por hora (ejemplo: 0830). Puede añadir tantos tramos como necesite. Las marcas de almuerzo son opcionales: si el trabajador no sale, no se genera ausencia ni error.", Dock = DockStyle.Fill, ForeColor = Color.DimGray, AutoEllipsis = true };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 7, 0, 0) }; var save = Button("Guardar jornada", Color.FromArgb(35, 91, 151)); save.Click += (_, _) => Save(); var cancel = Button("Cancelar", Color.Gray); cancel.Click += (_, _) => Close(); actions.Controls.AddRange(new Control[] { save, cancel });
        root.Controls.Add(nameRow, 0, 0); root.Controls.Add(segmentHost, 0, 1); root.Controls.Add(segmentBar, 0, 2); root.Controls.Add(lunch, 0, 3); root.Controls.Add(help, 0, 4); root.Controls.Add(actions, 0, 5); Controls.Add(root);
        lunchEnabled.CheckedChanged += (_, _) => UpdateLunchFields(); UpdateLunchFields();
    }

    void AddSegment()
    {
        var previous = segments.Rows.Cast<DataGridViewRow>().LastOrDefault(); var start = ""; var end = "";
        if (previous is not null && TryClock(previous.Cells[1].Value?.ToString() ?? "", out var lastExit)) { var next = lastExit.Add(TimeSpan.FromMinutes(30)); if (next.TotalHours < 23) { start = Clock(next); end = Clock(next.Add(TimeSpan.FromHours(2))); } }
        var index = segments.Rows.Add(start, end); segments.CurrentCell = segments.Rows[index].Cells[0]; segments.BeginEdit(true);
    }

    void RemoveSegment()
    {
        if (segments.Rows.Count <= 1) { MessageBox.Show(this, "La jornada debe conservar al menos un tramo.", "Tipo de jornada", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        if (segments.CurrentRow is not null) segments.Rows.Remove(segments.CurrentRow);
    }

    void Save()
    {
        try
        {
            segments.EndEdit();
            if (string.IsNullOrWhiteSpace(name.Text)) throw new InvalidOperationException("Indique un nombre para la jornada.");
            var workSegments = new List<WorkSegment>();
            foreach (DataGridViewRow row in segments.Rows)
            {
                if (!TryClock(row.Cells[0].Value?.ToString() ?? "", out var entry) || !TryClock(row.Cells[1].Value?.ToString() ?? "", out var exit)) throw new InvalidOperationException("Complete todos los tramos con cuatro números, por ejemplo 0830 y 1230.");
                workSegments.Add(new WorkSegment(entry, exit));
            }
            ValidateSegments(workSegments);
            LunchWindow? lunch = null;
            if (lunchEnabled.Checked)
            {
                if (!TryClock(lunchStart.Text, out var start) || !TryClock(lunchEnd.Text, out var end)) throw new InvalidOperationException("Complete el horario de almuerzo con cuatro números.");
                if (start >= end) throw new InvalidOperationException("El inicio del almuerzo debe ser anterior a su finalización.");
                if (!workSegments.Any(segment => start >= segment.Entry && end <= segment.Exit)) throw new InvalidOperationException("El almuerzo opcional debe quedar dentro de uno de los tramos de trabajo.");
                lunch = new LunchWindow(start, end);
            }
            var first = workSegments[0]; var second = workSegments.Count > 1 ? workSegments[1] : null;
            Result = new WorkdayType(id, name.Text.Trim(), first.Entry, first.Exit, second?.Entry ?? TimeSpan.Zero, second?.Exit ?? TimeSpan.Zero, workSegments.ToArray(), lunch);
            DialogResult = DialogResult.OK; Close();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Jornada inválida", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    static void ValidateSegments(IReadOnlyList<WorkSegment> values)
    {
        WorkSegment? previous = null;
        for (var index = 0; index < values.Count; index++)
        {
            var segment = values[index]; if (segment.Entry >= segment.Exit) throw new InvalidOperationException($"En el tramo {index + 1}, el ingreso debe ser anterior a la salida.");
            if (previous is not null && previous.Exit > segment.Entry) throw new InvalidOperationException($"El tramo {index + 1} se superpone con el anterior."); previous = segment;
        }
    }

    void UpdateLunchFields() { lunchStart.Enabled = lunchEnabled.Checked; lunchEnd.Enabled = lunchEnabled.Checked; }
    static bool TryClock(string value, out TimeSpan result) { result = TimeSpan.Zero; var text = new string((value ?? "").Where(char.IsDigit).ToArray()); if (text.Length == 3) text = "0" + text; if (text.Length != 4 || !int.TryParse(text[..2], out var hour) || !int.TryParse(text[2..], out var minute) || hour > 23 || minute > 59) return false; result = new TimeSpan(hour, minute, 0); return true; }
    static TextBox ClockBox() { var box = new TextBox { Dock = DockStyle.Fill }; box.KeyPress += DigitsOnly; return box; }
    static void DigitsOnly(object? sender, KeyPressEventArgs e) { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; }
    static string Clock(TimeSpan value) => value.ToString(@"hhmm");
    static Button Button(string text, Color color) => new() { Text = text, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, AutoSize = true, Height = 34, FlatAppearance = { BorderSize = 0 }, Margin = new Padding(0, 0, 8, 0), Padding = new Padding(10, 0, 10, 0) };
}
