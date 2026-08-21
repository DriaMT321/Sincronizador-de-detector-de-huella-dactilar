using AsistenciaSync.Backend;

namespace AsistenciaSync;

internal sealed class UserScheduleForm : Form
{
    readonly AppSettings settings;
    readonly DataGridView grid = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false };

    public UserScheduleForm(AppSettings settings)
    {
        this.settings = settings; Text = "Jornada por empleado"; Width = 1050; Height = 570; MinimumSize = new Size(850, 450); StartPosition = FormStartPosition.CenterParent;
        var iconPath = Path.Combine(AppContext.BaseDirectory, "LOGO.ico"); if (File.Exists(iconPath)) Icon = new Icon(iconPath);
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "Empleado", ReadOnly = true, FillWeight = 80 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Nombre", HeaderText = "Nombre", ReadOnly = true, FillWeight = 150 });
        foreach (var day in new[] { "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb", "Dom" }) grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = day, HeaderText = day, FillWeight = 55 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Entrada", HeaderText = "Ingreso", FillWeight = 85 }); grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Salida", HeaderText = "Salida", FillWeight = 85 });
        Controls.Add(grid);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, Padding = new Padding(10) };
        var save = Button("Guardar jornadas", Color.FromArgb(35, 91, 151)); save.Click += (_, _) => Save(); var close = Button("Cerrar", Color.Gray); close.Click += (_, _) => Close();
        bar.Controls.AddRange(new Control[] { save, close }); Controls.Add(bar); LoadRows();
    }

    void LoadRows()
    {
        var employees = AttendanceConfigurationStore.ReadEmployees(settings); var schedules = AttendanceConfigurationStore.ReadSchedules(settings);
        foreach (var employee in employees)
        {
            var s = schedules.TryGetValue(employee.Id, out var configured) ? configured : new EmployeeSchedule(employee.Id, true, true, true, true, true, false, false, settings.EntryTime, settings.ExitTime);
            grid.Rows.Add(employee.Id, employee.Name, s.Monday, s.Tuesday, s.Wednesday, s.Thursday, s.Friday, s.Saturday, s.Sunday, s.Entry.ToString(@"hh\:mm"), s.Exit.ToString(@"hh\:mm"));
        }
    }

    void Save()
    {
        try
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                var id = Convert.ToString(row.Cells["Id"].Value) ?? ""; if (id.Length == 0) continue;
                if (!TimeSpan.TryParse(Convert.ToString(row.Cells["Entrada"].Value), out var entry) || !TimeSpan.TryParse(Convert.ToString(row.Cells["Salida"].Value), out var exit)) throw new InvalidOperationException($"Horario inválido para {id}. Use HH:mm.");
                AttendanceConfigurationStore.SaveSchedule(settings, new EmployeeSchedule(id, B(row, "Lun"), B(row, "Mar"), B(row, "Mié"), B(row, "Jue"), B(row, "Vie"), B(row, "Sáb"), B(row, "Dom"), entry, exit));
            }
            MessageBox.Show(this, "Las jornadas fueron guardadas correctamente.", "Configuración de usuarios", MessageBoxButtons.OK, MessageBoxIcon.Information); Close();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "No se pudieron guardar las jornadas", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    static bool B(DataGridViewRow row, string name) => Convert.ToBoolean(row.Cells[name].Value ?? false);
    static Button Button(string text, Color color) => new() { Text = text, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, AutoSize = true, Height = 32, FlatAppearance = { BorderSize = 0 } };
}
