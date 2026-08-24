using AsistenciaSync.Configuration;
using AsistenciaSync.Models;

namespace AsistenciaSync.UI;

internal sealed class DailyIncidentForm : Form
{
    readonly AppSettings settings; readonly ComboBox employee = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 }; readonly DateTimePicker date = new() { Format = DateTimePickerFormat.Short, Width = 260 };
    readonly ComboBox type = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 }; readonly TextBox reason = new() { Width = 260, Multiline = true, Height = 60 }; readonly CheckBox absence = new() { Text = "Justifica ausencia completa", AutoSize = true }; readonly CheckBox lateness = new() { Text = "Justifica tardanza", AutoSize = true };

    public DailyIncidentForm(AppSettings settings)
    {
        this.settings = settings; Text = "Configuración del día"; Width = 560; Height = 430; StartPosition = FormStartPosition.CenterParent;
        var iconPath = Path.Combine(AppContext.BaseDirectory, "LOGO.ico"); if (File.Exists(iconPath)) Icon = new Icon(iconPath);
        employee.DataSource = AttendanceConfigurationStore.ReadEmployees(settings); employee.DisplayMember = nameof(EmployeeOption.Name); employee.ValueMember = nameof(EmployeeOption.Id);
        type.Items.AddRange(new object[] { "Enfermedad", "Inconveniente", "Permiso", "Otra" }); type.SelectedIndex = 0; date.Value = DateTime.Today;
        var fields = new TableLayoutPanel { Dock = DockStyle.Top, Height = 245, Padding = new Padding(24), ColumnCount = 2 }; fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Add(fields, "Empleado", employee); Add(fields, "Fecha", date); Add(fields, "Tipo", type); Add(fields, "Motivo / observación", reason); Add(fields, "", absence); Add(fields, "", lateness);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(24, 8, 0, 0) }; var save = Button("Guardar configuración del día", Color.FromArgb(35, 91, 151)); save.Click += (_, _) => Save(); var close = Button("Cerrar", Color.Gray); close.Click += (_, _) => Close(); bar.Controls.AddRange(new Control[] { save, close }); Controls.Add(fields); Controls.Add(bar);
    }

    void Save()
    {
        try
        {
            if (employee.SelectedItem is not EmployeeOption selected) throw new InvalidOperationException("Seleccione un empleado.");
            if (!absence.Checked && !lateness.Checked) throw new InvalidOperationException("Seleccione qué desea justificar: ausencia o tardanza.");
            AttendanceConfigurationStore.SaveIncident(settings, selected.Id, date.Value.Date, type.Text, reason.Text.Trim(), absence.Checked, lateness.Checked);
            MessageBox.Show(this, "La incidencia del día fue guardada. El reporte la tomará en cuenta.", "Configuración del día", MessageBoxButtons.OK, MessageBoxIcon.Information); Close();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "No se pudo guardar la incidencia", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    static void Add(TableLayoutPanel panel, string label, Control control) { var row = panel.RowCount++; panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row); panel.Controls.Add(control, 1, row); }
    static Button Button(string text, Color color) => new() { Text = text, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, AutoSize = true, Height = 34, FlatAppearance = { BorderSize = 0 } };
}
