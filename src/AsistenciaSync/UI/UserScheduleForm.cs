using AsistenciaSync.Configuration;
using AsistenciaSync.Models;

namespace AsistenciaSync.UI;

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
        var type = new DataGridViewComboBoxColumn { Name = "Tipo", HeaderText = "Jornada", FillWeight = 100 }; type.Items.AddRange("Continua", "Discontinua"); grid.Columns.Add(type);
        foreach (var day in new[] { "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb", "Dom" }) grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = day, HeaderText = day, FillWeight = 55 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Entrada1", HeaderText = "Ingreso 1", FillWeight = 85 }); grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Salida1", HeaderText = "Salida 1", FillWeight = 85 }); grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Entrada2", HeaderText = "Ingreso 2", FillWeight = 85 }); grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Salida2", HeaderText = "Salida 2", FillWeight = 85 });
        grid.CurrentCellDirtyStateChanged += (_, _) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        grid.CellValueChanged += (_, e) => { if (e.RowIndex >= 0 && grid.Columns[e.ColumnIndex].Name == "Tipo") ApplyJornadaRule(grid.Rows[e.RowIndex]); };
        grid.EditingControlShowing += (_, e) => { if (grid.CurrentCell is not null && IsTimeColumn(grid.CurrentCell.OwningColumn.Name) && e.Control is TextBox textBox) { textBox.KeyPress -= NumericOnly; textBox.KeyPress += NumericOnly; } };
        grid.CellValidating += (_, e) =>
        {
            if (e.RowIndex < 0 || !IsTimeColumn(grid.Columns[e.ColumnIndex].Name) || grid.Rows[e.RowIndex].Cells[e.ColumnIndex].ReadOnly) return;
            var value = Convert.ToString(e.FormattedValue) ?? "";
            if (value.Length == 0 && (grid.Columns[e.ColumnIndex].Name == "Entrada2" || grid.Columns[e.ColumnIndex].Name == "Salida2")) return;
            if (!TryClock(value, out var clock)) { e.Cancel = true; grid.Rows[e.RowIndex].ErrorText = "Escriba la hora con cuatro números, por ejemplo 0800."; return; }
            grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = clock.ToString(@"hh\:mm"); grid.Rows[e.RowIndex].ErrorText = "";
        };
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
            var s = schedules.TryGetValue(employee.Id, out var configured) ? configured : new EmployeeSchedule(employee.Id, true, true, true, true, true, false, false, false, settings.EntryTime, settings.ExitTime, TimeSpan.Zero, TimeSpan.Zero);
            var rowIndex = grid.Rows.Add(employee.Id, employee.Name, s.Discontinuous ? "Discontinua" : "Continua", s.Monday, s.Tuesday, s.Wednesday, s.Thursday, s.Friday, s.Saturday, s.Sunday, s.Entry.ToString(@"hh\:mm"), s.Exit.ToString(@"hh\:mm"), s.SecondEntry == TimeSpan.Zero ? "" : s.SecondEntry.ToString(@"hh\:mm"), s.SecondExit == TimeSpan.Zero ? "" : s.SecondExit.ToString(@"hh\:mm")); ApplyJornadaRule(grid.Rows[rowIndex]);
        }
    }

    void Save()
    {
        try
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                var id = Convert.ToString(row.Cells["Id"].Value) ?? ""; if (id.Length == 0) continue;
                var discontinuous = Convert.ToString(row.Cells["Tipo"].Value) == "Discontinua";
                if (!TryClock(Convert.ToString(row.Cells["Entrada1"].Value), out var entry) || !TryClock(Convert.ToString(row.Cells["Salida1"].Value), out var exit)) throw new InvalidOperationException($"Horario inválido para {id}. Escriba cuatro números, por ejemplo 0800.");
                var secondEntry = TimeSpan.Zero; var secondExit = TimeSpan.Zero;
                if (discontinuous && (!TryClock(Convert.ToString(row.Cells["Entrada2"].Value), out secondEntry) || !TryClock(Convert.ToString(row.Cells["Salida2"].Value), out secondExit))) throw new InvalidOperationException($"Complete los cuatro horarios para la jornada discontinua de {id} usando cuatro números.");
                if (discontinuous && !(entry < exit && exit <= secondEntry && secondEntry < secondExit)) throw new InvalidOperationException($"El horario discontinuo de {id} debe respetar: Ingreso 1 < Salida 1 <= Ingreso 2 < Salida 2.");
                AttendanceConfigurationStore.SaveSchedule(settings, new EmployeeSchedule(id, B(row, "Lun"), B(row, "Mar"), B(row, "Mié"), B(row, "Jue"), B(row, "Vie"), B(row, "Sáb"), B(row, "Dom"), discontinuous, entry, exit, secondEntry, secondExit));
            }
            MessageBox.Show(this, "Las jornadas fueron guardadas correctamente.", "Configuración de usuarios", MessageBoxButtons.OK, MessageBoxIcon.Information); Close();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "No se pudieron guardar las jornadas", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    static bool B(DataGridViewRow row, string name) => Convert.ToBoolean(row.Cells[name].Value ?? false);
    static bool IsTimeColumn(string name) => name is "Entrada1" or "Salida1" or "Entrada2" or "Salida2";
    static void NumericOnly(object? sender, KeyPressEventArgs e) { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; }
    static bool TryClock(string? value, out TimeSpan clock)
    {
        clock = TimeSpan.Zero; var text = (value ?? "").Trim(); if (text.Contains(':')) return TimeSpan.TryParseExact(text, new[] { @"h\:mm", @"hh\:mm" }, null, out clock) && clock.TotalHours < 24;
        if (text.Length != 4 || !text.All(char.IsDigit) || !int.TryParse(text[..2], out var hour) || !int.TryParse(text[2..], out var minute) || hour > 23 || minute > 59) return false;
        clock = new TimeSpan(hour, minute, 0); return true;
    }
    static void ApplyJornadaRule(DataGridViewRow row)
    {
        var discontinuous = Convert.ToString(row.Cells["Tipo"].Value) == "Discontinua";
        foreach (var name in new[] { "Entrada2", "Salida2" })
        {
            row.Cells[name].ReadOnly = !discontinuous;
            row.Cells[name].Style.BackColor = discontinuous ? Color.White : Color.FromArgb(235, 235, 235);
            if (!discontinuous) row.Cells[name].Value = "";
        }
    }
    static Button Button(string text, Color color) => new() { Text = text, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, AutoSize = true, Height = 32, FlatAppearance = { BorderSize = 0 } };
}
