using AsistenciaSync.Configuration;
using AsistenciaSync.Models;
using AsistenciaSync.Services;

namespace AsistenciaSync.UI;

internal sealed class CustomizationForm : Form
{
    readonly AppSettings settings;
    readonly DataGridView holidays = Grid();
    readonly DataGridView statuses = Grid();
    readonly DataGridView employees = Grid();
    readonly DateTimePicker holidayDate = new() { Format = DateTimePickerFormat.Short };
    readonly TextBox holidayDescription = new() { Width = 240 };
    readonly ComboBox holidayEmployee = new() { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };

    public CustomizationForm(AppSettings settings)
    {
        this.settings = settings; Text = "Personalización y mantenimiento"; Width = 900; Height = 620; StartPosition = FormStartPosition.CenterParent; BackColor = Color.FromArgb(245, 247, 250);
        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(16, 8) };
        tabs.TabPages.Add(HolidaysPage()); tabs.TabPages.Add(StatusesPage()); tabs.TabPages.Add(EmployeesPage()); Controls.Add(tabs);
    }

    TabPage HolidaysPage()
    {
        var page = new TabPage("Días festivos") { Padding = new Padding(18) }; var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, WrapContents = false };
        top.Controls.Add(new Label { Text = "Fecha", AutoSize = true, Padding = new Padding(0, 8, 5, 0) }); top.Controls.Add(holidayDate); top.Controls.Add(new Label { Text = "Descripción", AutoSize = true, Padding = new Padding(15, 8, 5, 0) }); top.Controls.Add(holidayDescription); top.Controls.Add(new Label { Text = "Aplica a", AutoSize = true, Padding = new Padding(15, 8, 5, 0) }); top.Controls.Add(holidayEmployee);
        var add = Button("Añadir / actualizar", Color.FromArgb(35, 91, 151)); add.Click += (_, _) => { var employeeId = (holidayEmployee.SelectedItem as EmployeeChoice)?.Id; CsvStore.SaveHoliday(settings, new Holiday(holidayDate.Value.Date, holidayDescription.Text.Trim(), string.IsNullOrWhiteSpace(employeeId) ? null : employeeId)); LoadHolidays(); };
        var remove = Button("Eliminar seleccionado", Color.FromArgb(170, 75, 75)); remove.Click += (_, _) => { if (holidays.CurrentRow?.Cells[0].Value is string value && DateTime.TryParse(value, out var date)) { CsvStore.DeleteHoliday(settings, date); LoadHolidays(); } };
        top.Controls.Add(add); top.Controls.Add(remove); page.Controls.Add(holidays); page.Controls.Add(top); holidays.Columns.Add("Fecha", "Fecha"); holidays.Columns.Add("Descripción", "Descripción"); holidays.Columns.Add("Empleado", "Aplica a"); holidays.Dock = DockStyle.Fill;
        holidayEmployee.Items.Add(new EmployeeChoice("", "Todos los trabajadores")); foreach (var employee in CsvStore.ReadEmployees(settings)) holidayEmployee.Items.Add(new EmployeeChoice(employee.Key, $"{employee.Key} - {employee.Value}")); holidayEmployee.SelectedIndex = 0; LoadHolidays(); return page;
    }

    TabPage StatusesPage()
    {
        var page = new TabPage("Estados y colores") { Padding = new Padding(18) }; statuses.AllowUserToAddRows = true; statuses.Columns.Add("Key", "Clave"); statuses.Columns.Add("Name", "Nombre mostrado"); statuses.Columns.Add("Color", "Color"); statuses.Dock = DockStyle.Fill; statuses.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && e.ColumnIndex == 2) ChooseStatusColor(e.RowIndex); };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(0, 8, 0, 0) }; var add = Button("Añadir estado", Color.FromArgb(80, 145, 112)); add.Click += (_, _) => { var index = statuses.Rows.Add("", "", "#415066"); statuses.CurrentCell = statuses.Rows[index].Cells[0]; statuses.BeginEdit(true); }; var color = Button("Elegir color", Color.FromArgb(115, 92, 145)); color.Click += (_, _) => { if (statuses.CurrentRow is not null && !statuses.CurrentRow.IsNewRow) ChooseStatusColor(statuses.CurrentRow.Index); }; var remove = Button("Eliminar estado", Color.FromArgb(170, 75, 75)); remove.Click += (_, _) => { if (statuses.CurrentRow is not null && !statuses.CurrentRow.IsNewRow) statuses.Rows.Remove(statuses.CurrentRow); }; var save = Button("Guardar cambios", Color.FromArgb(35, 91, 151)); save.Click += (_, _) => SaveStatuses(); actions.Controls.Add(add); actions.Controls.Add(color); actions.Controls.Add(remove); actions.Controls.Add(save); page.Controls.Add(statuses); page.Controls.Add(actions);
        foreach (var item in StatusCatalog.Load(settings)) statuses.Rows.Add(item.Key, item.Name, item.Color); return page;
    }

    TabPage EmployeesPage()
    {
        var page = new TabPage("Trabajadores y dispositivo") { Padding = new Padding(18) }; employees.Columns.Add("Id", "ID"); employees.Columns.Add("Name", "Nombre"); employees.Dock = DockStyle.Fill;
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 92, WrapContents = true };
        var rename = Button("Cambiar nombre", Color.FromArgb(80, 145, 112)); rename.Click += (_, _) => RunDeviceAction("Cambiar nombre", device => { var id = SelectedId(); var name = SelectedName(); device.RenameUser(id, name); var map = CsvStore.ReadEmployees(settings); map[id] = name; CsvStore.SaveEmployeesMap(settings, map); LoadEmployees(); });
        var create = Button("Crear trabajador", Color.FromArgb(35, 91, 151)); create.Click += (_, _) => CreateEmployee();
        var delete = Button("Borrar trabajador del reloj", Color.FromArgb(170, 75, 75)); delete.Click += (_, _) => { if (Confirm("Se borrará el trabajador seleccionado del dispositivo, incluyendo sus huellas. ¿Continuar?")) RunDeviceAction("Borrar trabajador", device => { device.DeleteUser(SelectedId()); var map = CsvStore.ReadEmployees(settings); map.Remove(SelectedId()); CsvStore.SaveEmployeesMap(settings, map); LoadEmployees(); }); };
        var clear = Button("Borrar marcaciones del reloj", Color.FromArgb(150, 80, 45)); clear.Click += (_, _) => { if (Confirm("Se borrarán todas las marcaciones del reloj, pero se conservarán los trabajadores. ¿Continuar?")) RunDeviceAction("Borrar marcaciones", device => { device.ClearAttendance(); CsvStore.ClearAllAttendance(settings); }); };
        bar.Controls.AddRange(new Control[] { rename, create, delete, clear }); page.Controls.Add(employees); page.Controls.Add(bar); LoadEmployees(); return page;
    }

    void LoadHolidays() { holidays.Rows.Clear(); foreach (var x in CsvStore.ReadHolidays(settings)) holidays.Rows.Add(x.Date.ToString("yyyy-MM-dd"), x.Description, x.EmployeeId ?? "Todos"); }
    void ChooseStatusColor(int rowIndex)
    {
        var current = Color.FromArgb(65, 80, 102); try { if (statuses.Rows[rowIndex].Cells[2].Value is string hex) current = ColorTranslator.FromHtml(hex); } catch { }
        using var dialog = new ColorDialog { Color = current, FullOpen = true }; if (dialog.ShowDialog(this) == DialogResult.OK) statuses.Rows[rowIndex].Cells[2].Value = ColorTranslator.ToHtml(dialog.Color);
    }
    void LoadEmployees() { employees.Rows.Clear(); foreach (var x in CsvStore.ReadEmployees(settings)) employees.Rows.Add(x.Key, x.Value); }
    void CreateEmployee()
    {
        using var form = new CreateEmployeeForm(); if (form.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            using var device = new ZkDeviceClient(settings.DeviceIp, settings.DevicePort); device.CreateUser(form.EmployeeId, form.EmployeeName); var map = CsvStore.ReadEmployees(settings); map[form.EmployeeId] = form.EmployeeName; CsvStore.SaveEmployeesMap(settings, map); LoadEmployees(); MessageBox.Show(this, "Trabajador creado en el reloj y en el reporte local.", "Trabajadores", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "No se pudo crear el trabajador", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    void SaveStatuses() { var values = statuses.Rows.Cast<DataGridViewRow>().Where(x => !x.IsNewRow).Select(x => new StatusOption { Key = x.Cells[0].Value?.ToString() ?? "", Name = x.Cells[1].Value?.ToString() ?? "", Color = x.Cells[2].Value?.ToString() ?? "#415066" }).Where(x => x.Key.Length > 0 && x.Name.Length > 0).ToList(); StatusCatalog.Save(settings, values); MessageBox.Show(this, "Estados guardados.", "Estados", MessageBoxButtons.OK, MessageBoxIcon.Information); }
    string SelectedId() => employees.CurrentRow?.Cells[0].Value?.ToString() ?? throw new InvalidOperationException("Seleccione un trabajador.");
    string SelectedName() => employees.CurrentRow?.Cells[1].Value?.ToString() ?? throw new InvalidOperationException("Indique un nombre.");
    void RunDeviceAction(string title, Action<ZkDeviceClient> action) { try { using var device = new ZkDeviceClient(settings.DeviceIp, settings.DevicePort); action(device); MessageBox.Show(this, "Operación completada correctamente.", title, MessageBoxButtons.OK, MessageBoxIcon.Information); } catch (Exception ex) { MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error); } }
    bool Confirm(string message) => MessageBox.Show(this, message, "Confirmar operación", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
    static Button Button(string text, Color color) => new() { Text = text, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, AutoSize = true, Height = 36, FlatAppearance = { BorderSize = 0 } };
    static DataGridView Grid() => new() { AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
    sealed record EmployeeChoice(string Id, string Display) { public override string ToString() => Display; }
}
