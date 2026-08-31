using AsistenciaSync.Models;
using AsistenciaSync.Services;

namespace AsistenciaSync.UI;

internal sealed partial class CustomizationForm
{
    TabPage HolidaysPage()
    {
        var page = new TabPage("Días festivos") { Padding = new Padding(18) }; var editor = new TableLayoutPanel { Dock = DockStyle.Top, Height = 92, ColumnCount = 4, RowCount = 2, Padding = new Padding(4, 0, 4, 6) }; editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82)); editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270)); editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105)); editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        editor.Controls.Add(new Label { Text = "Fecha", AutoSize = true }, 0, 0); editor.Controls.Add(holidayDate, 1, 0); editor.Controls.Add(new Label { Text = "Descripción", AutoSize = true }, 2, 0); editor.Controls.Add(holidayDescription, 3, 0); editor.Controls.Add(new Label { Text = "Aplica a", AutoSize = true }, 0, 1); editor.Controls.Add(holidayEmployee, 1, 1); editor.Controls.Add(new Label { Text = "Regla", AutoSize = true }, 2, 1); editor.Controls.Add(holidayCounts, 3, 1);
        foreach (Control control in editor.Controls) { control.Anchor = AnchorStyles.Left | AnchorStyles.Top; control.Margin = new Padding(0, 7, 8, 0); } holidayDate.Width = 205; holidayDescription.Width = 300; holidayEmployee.Width = 260; holidayCounts.Margin = new Padding(0, 5, 0, 0);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, WrapContents = false, Padding = new Padding(4, 4, 0, 8) }; var add = Button("Añadir / actualizar", Color.FromArgb(35, 91, 151)); add.Click += (_, _) => SaveHoliday(); var remove = Button("Eliminar seleccionado", Color.FromArgb(170, 75, 75)); remove.Click += (_, _) => DeleteSelectedHoliday(); buttons.Controls.Add(add); buttons.Controls.Add(remove);
        holidays.Columns.Add("Fecha", "Fecha"); holidays.Columns.Add("Descripción", "Descripción"); holidays.Columns.Add("Empleado", "Aplica a"); holidays.Columns.Add("Cuenta", "Cuenta como jornada"); holidays.Columns.Add("EmpleadoId", "ID"); holidays.Columns["EmpleadoId"].Visible = false; holidays.Dock = DockStyle.Fill; holidays.SelectionChanged += (_, _) => LoadSelectedHoliday();
        page.Controls.Add(holidays); page.Controls.Add(buttons); page.Controls.Add(editor); holidayEmployee.Items.Add(new EmployeeChoice("", "Todos los trabajadores")); foreach (var employee in CsvStore.ReadEmployees(settings)) holidayEmployee.Items.Add(new EmployeeChoice(employee.Key, $"{employee.Key} - {employee.Value}")); holidayEmployee.SelectedIndex = 0; LoadHolidays(); return page;
    }

    void LoadHolidays() { holidays.Rows.Clear(); foreach (var x in CsvStore.ReadHolidays(settings)) holidays.Rows.Add(x.Date.ToString("yyyy-MM-dd"), x.Description, x.EmployeeId ?? "Todos", x.CountsAsWorkday ? "Sí" : "No", x.EmployeeId ?? ""); }
    void SaveHoliday()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(holidayDescription.Text)) throw new InvalidOperationException("Indique una descripción para el día festivo.");
            var employeeId = (holidayEmployee.SelectedItem as EmployeeChoice)?.Id; CsvStore.SaveHoliday(settings, new Holiday(holidayDate.Value.Date, holidayDescription.Text.Trim(), string.IsNullOrWhiteSpace(employeeId) ? null : employeeId, holidayCounts.Checked)); LoadHolidays();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "No se pudo guardar el día festivo", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    void DeleteSelectedHoliday()
    {
        if (holidays.CurrentRow?.Cells[0].Value is not string value || !DateTime.TryParseExact(value, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var date)) return; var employeeId = holidays.CurrentRow.Cells["EmpleadoId"].Value?.ToString(); if (!Confirm("¿Eliminar el día festivo seleccionado?")) return; CsvStore.DeleteHoliday(settings, date, string.IsNullOrWhiteSpace(employeeId) ? null : employeeId); LoadHolidays();
    }
    void LoadSelectedHoliday()
    {
        if (holidays.CurrentRow is not { IsNewRow: false } row || row.Cells[0].Value is not string dateText || !DateTime.TryParseExact(dateText, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var selectedDate)) return; holidayDate.Value = selectedDate; holidayDescription.Text = row.Cells[1].Value?.ToString() ?? ""; holidayCounts.Checked = string.Equals(row.Cells[3].Value?.ToString(), "Sí", StringComparison.OrdinalIgnoreCase); var employeeId = row.Cells["EmpleadoId"].Value?.ToString() ?? ""; holidayEmployee.SelectedItem = holidayEmployee.Items.OfType<EmployeeChoice>().FirstOrDefault(x => x.Id.Equals(employeeId, StringComparison.OrdinalIgnoreCase)) ?? holidayEmployee.Items[0];
    }
}
