using AsistenciaSync.Models;

namespace AsistenciaSync.UI;

internal sealed class EmployeeWorkdaysForm : Form
{
    readonly CheckBox monday = Day("Lunes");
    readonly CheckBox tuesday = Day("Martes");
    readonly CheckBox wednesday = Day("Miércoles");
    readonly CheckBox thursday = Day("Jueves");
    readonly CheckBox friday = Day("Viernes");
    readonly CheckBox saturday = Day("Sábado");
    readonly CheckBox sunday = Day("Domingo");

    public bool Monday => monday.Checked;
    public bool Tuesday => tuesday.Checked;
    public bool Wednesday => wednesday.Checked;
    public bool Thursday => thursday.Checked;
    public bool Friday => friday.Checked;
    public bool Saturday => saturday.Checked;
    public bool Sunday => sunday.Checked;

    public EmployeeWorkdaysForm(string employee, EmployeeSchedule? current)
    {
        Text = "Días laborales del trabajador";
        Width = 610; Height = 390; MinimumSize = new Size(560, 360); StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; BackColor = Color.White;
        var iconPath = Path.Combine(AppContext.BaseDirectory, "LOGO.ico"); if (File.Exists(iconPath)) Icon = new Icon(iconPath);

        monday.Checked = current?.Monday ?? true;
        tuesday.Checked = current?.Tuesday ?? true;
        wednesday.Checked = current?.Wednesday ?? true;
        thursday.Checked = current?.Thursday ?? true;
        friday.Checked = current?.Friday ?? true;
        saturday.Checked = current?.Saturday ?? false;
        sunday.Checked = current?.Sunday ?? false;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 5 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.Controls.Add(new Label { Text = employee, AutoSize = true, Font = new Font("Segoe UI", 15, FontStyle.Bold), ForeColor = Color.FromArgb(24, 57, 92), Anchor = AnchorStyles.Left }, 0, 0);
        root.Controls.Add(new Label { Text = "Seleccione los días que forman parte de su jornada laboral.", AutoSize = true, ForeColor = Color.DimGray, Anchor = AnchorStyles.Left }, 0, 1);

        var days = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2, BackColor = Color.FromArgb(247, 249, 252), Padding = new Padding(16) };
        for (var index = 0; index < 4; index++) days.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        days.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); days.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        var controls = new[] { monday, tuesday, wednesday, thursday, friday, saturday, sunday };
        for (var index = 0; index < controls.Length; index++) days.Controls.Add(controls[index], index % 4, index / 4);
        root.Controls.Add(days, 0, 2);

        var presets = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 7, 0, 0) };
        var mondayFriday = Button("Lunes a viernes", Color.FromArgb(80, 110, 140)); mondayFriday.Click += (_, _) => SelectPreset(true, true, true, true, true, false, false);
        var mondaySaturday = Button("Lunes a sábado", Color.FromArgb(80, 110, 140)); mondaySaturday.Click += (_, _) => SelectPreset(true, true, true, true, true, true, false);
        var everyDay = Button("Todos los días", Color.FromArgb(80, 110, 140)); everyDay.Click += (_, _) => SelectPreset(true, true, true, true, true, true, true);
        presets.Controls.AddRange(new Control[] { mondayFriday, mondaySaturday, everyDay }); root.Controls.Add(presets, 0, 3);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 7, 0, 0) };
        var save = Button("Guardar días laborales", Color.FromArgb(35, 91, 151)); save.Click += (_, _) => Save();
        var cancel = Button("Cancelar", Color.Gray); cancel.Click += (_, _) => Close();
        actions.Controls.AddRange(new Control[] { save, cancel }); root.Controls.Add(actions, 0, 4);
        Controls.Add(root); AcceptButton = save; CancelButton = cancel;
    }

    void SelectPreset(bool mon, bool tue, bool wed, bool thu, bool fri, bool sat, bool sun)
    {
        monday.Checked = mon; tuesday.Checked = tue; wednesday.Checked = wed; thursday.Checked = thu; friday.Checked = fri; saturday.Checked = sat; sunday.Checked = sun;
    }

    void Save()
    {
        if (!(Monday || Tuesday || Wednesday || Thursday || Friday || Saturday || Sunday))
        {
            MessageBox.Show(this, "Seleccione al menos un día laboral.", "Días laborales", MessageBoxButtons.OK, MessageBoxIcon.Warning); return;
        }
        DialogResult = DialogResult.OK; Close();
    }

    static CheckBox Day(string text) => new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 10) };
    static Button Button(string text, Color color) => new() { Text = text, AutoSize = true, Height = 34, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, Margin = new Padding(0, 0, 8, 0), Padding = new Padding(10, 0, 10, 0) };
}
