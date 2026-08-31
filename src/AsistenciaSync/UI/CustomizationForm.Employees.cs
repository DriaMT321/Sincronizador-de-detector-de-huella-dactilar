using AsistenciaSync.Models;
using AsistenciaSync.Services;

namespace AsistenciaSync.UI;

internal sealed partial class CustomizationForm
{
    TabPage EmployeesPage()
    {
        var page = new TabPage("Trabajadores y dispositivo") { Padding = new Padding(18) }; employees.Columns.Add("Id", "ID"); employees.Columns.Add("Name", "Nombre"); RefreshJourneyOptions(); employees.Columns.Add(journeyColumn); employees.Columns.Add("Days", "Días laborales"); employees.Columns["Days"].ReadOnly = true; employees.DataError += (_, e) => { e.ThrowException = false; e.Cancel = true; }; employees.Dock = DockStyle.Fill;
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 92, WrapContents = true, AutoScroll = true, Padding = new Padding(2, 4, 2, 2) };
        var rename = Button("Cambiar nombre", Color.FromArgb(80, 145, 112)); rename.Click += (_, _) => RunDeviceAction("Cambiar nombre", device => { var id = SelectedId(); var name = SelectedName(); device.RenameUser(id, name); var map = CsvStore.ReadEmployees(settings); map[id] = name; CsvStore.SaveEmployeesMap(settings, map); LoadEmployees(); });
        var create = Button("Crear trabajador", Color.FromArgb(35, 91, 151)); create.Click += (_, _) => CreateEmployee();
        var delete = Button("Borrar trabajador del reloj", Color.FromArgb(170, 75, 75)); delete.Click += (_, _) => { if (Confirm("Se borrará el trabajador seleccionado del dispositivo, incluyendo sus huellas. ¿Continuar?")) RunDeviceAction("Borrar trabajador", device => { device.DeleteUser(SelectedId()); var map = CsvStore.ReadEmployees(settings); map.Remove(SelectedId()); CsvStore.SaveEmployeesMap(settings, map); LoadEmployees(); }); };
        var clear = Button("Borrar marcaciones del reloj", Color.FromArgb(150, 80, 45)); clear.Click += (_, _) => { if (Confirm("Se borrarán todas las marcaciones del reloj, pero se conservarán los trabajadores. ¿Continuar?")) RunDeviceAction("Borrar marcaciones", device => { device.ClearAttendance(); CsvStore.ClearAllAttendance(settings); }); };
        var workdays = Button("Configurar días laborales", Color.FromArgb(92, 110, 145)); workdays.Click += (_, _) => ConfigureSelectedWorkdays();
        var saveJourney = Button("Guardar jornadas", Color.FromArgb(35, 91, 151)); saveJourney.Click += (_, _) => SaveEmployeeSchedules(); bar.Controls.AddRange(new Control[] { rename, create, workdays, saveJourney, delete, clear }); page.Controls.Add(employees); page.Controls.Add(bar); LoadEmployees(); return page;
    }

    void LoadEmployees()
    {
        employees.Rows.Clear(); var schedules = CsvStore.ReadSchedules(settings); var types = AttendanceConfigurationStore.ReadWorkdayTypes(settings);
        foreach (var x in CsvStore.ReadEmployees(settings))
        {
            var schedule = schedules.TryGetValue(x.Key, out var configured) ? configured : null; var typeId = schedule is null || string.IsNullOrWhiteSpace(schedule.WorkdayTypeId) ? (schedule?.Discontinuous == true ? "discontinua" : "continua") : schedule.WorkdayTypeId; var type = types.FirstOrDefault(t => t.Id.Equals(typeId, StringComparison.OrdinalIgnoreCase)); employees.Rows.Add(x.Key, x.Value, type?.Name ?? (schedule?.Discontinuous == true ? "Discontinua" : "Continua"), FormatWorkdays(schedule));
        }
    }

    void ConfigureSelectedWorkdays()
    {
        try
        {
            var row = employees.CurrentRow ?? throw new InvalidOperationException("Seleccione un trabajador.");
            var id = SelectedId(); var name = SelectedName(); var types = AttendanceConfigurationStore.ReadWorkdayTypes(settings); var typeName = row.Cells["Tipo"].Value?.ToString() ?? ""; var type = types.FirstOrDefault(x => x.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException("Seleccione primero un tipo de jornada válido.");
            var schedules = CsvStore.ReadSchedules(settings); var current = schedules.TryGetValue(id, out var saved) ? saved : new EmployeeSchedule(id, true, true, true, true, true, false, false, type.Discontinuous, type.Entry, type.Exit, type.SecondEntry, type.SecondExit, type.Id);
            using var form = new EmployeeWorkdaysForm($"{id} · {name}", current); if (form.ShowDialog(this) != DialogResult.OK) return;
            CsvStore.SaveSchedule(settings, new EmployeeSchedule(id, form.Monday, form.Tuesday, form.Wednesday, form.Thursday, form.Friday, form.Saturday, form.Sunday, type.Discontinuous, type.Entry, type.Exit, type.SecondEntry, type.SecondExit, type.Id)); LoadEmployees();
            MessageBox.Show(this, "Los días laborales fueron guardados.", "Días laborales", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "No se pudieron guardar los días", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    void RefreshJourneyOptions() { journeyColumn.Items.Clear(); foreach (var type in AttendanceConfigurationStore.ReadWorkdayTypes(settings)) journeyColumn.Items.Add(type.Name); }
    void SaveEmployeeSchedules()
    {
        var existing = CsvStore.ReadSchedules(settings); var types = AttendanceConfigurationStore.ReadWorkdayTypes(settings);
        foreach (DataGridViewRow row in employees.Rows)
        {
            if (row.IsNewRow) continue; var id = row.Cells["Id"].Value?.ToString() ?? ""; if (id.Length == 0) continue; var typeName = row.Cells["Tipo"].Value?.ToString() ?? ""; var type = types.FirstOrDefault(x => x.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException($"Seleccione un tipo de jornada válido para {id}."); var old = existing.TryGetValue(id, out var configured) ? configured : new EmployeeSchedule(id, true, true, true, true, true, false, false, false, type.Entry, type.Exit, type.SecondEntry, type.SecondExit, type.Id); CsvStore.SaveSchedule(settings, new EmployeeSchedule(id, old.Monday, old.Tuesday, old.Wednesday, old.Thursday, old.Friday, old.Saturday, old.Sunday, type.Discontinuous, type.Entry, type.Exit, type.SecondEntry, type.SecondExit, type.Id));
        }
        MessageBox.Show(this, "Las jornadas de los trabajadores fueron guardadas.", "Jornadas", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    string SelectedId() => employees.CurrentRow?.Cells[0].Value?.ToString() ?? throw new InvalidOperationException("Seleccione un trabajador.");
    string SelectedName() => employees.CurrentRow?.Cells[1].Value?.ToString() ?? throw new InvalidOperationException("Indique un nombre.");
    static string FormatWorkdays(EmployeeSchedule? schedule)
    {
        var days = schedule is null ? new[] { true, true, true, true, true, false, false } : new[] { schedule.Monday, schedule.Tuesday, schedule.Wednesday, schedule.Thursday, schedule.Friday, schedule.Saturday, schedule.Sunday };
        if (days.SequenceEqual(new[] { true, true, true, true, true, false, false })) return "Lun–Vie";
        if (days.SequenceEqual(new[] { true, true, true, true, true, true, false })) return "Lun–Sáb";
        if (days.All(x => x)) return "Todos";
        var names = new[] { "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb", "Dom" }; return string.Join(", ", names.Where((_, index) => days[index]));
    }
}
