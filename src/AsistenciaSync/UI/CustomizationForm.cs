using AsistenciaSync.Configuration;

namespace AsistenciaSync.UI;

internal sealed partial class CustomizationForm : Form
{
    readonly AppSettings settings;
    readonly DataGridView holidays = Grid();
    readonly DataGridView employees = Grid();
    readonly DataGridView workdayTypes = Grid();
    readonly DataGridViewComboBoxColumn journeyColumn = new() { Name = "Tipo", HeaderText = "Jornada" };
    readonly DateTimePicker holidayDate = new() { Format = DateTimePickerFormat.Short };
    readonly TextBox holidayDescription = new() { Width = 240 };
    readonly ComboBox holidayEmployee = new() { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly CheckBox holidayCounts = new() { Text = "Cuenta como jornada laboral", AutoSize = true };

    public CustomizationForm(AppSettings settings)
    {
        this.settings = settings; Text = "Personalización y mantenimiento"; Width = 1180; Height = 760; MinimumSize = new Size(760, 520); StartPosition = FormStartPosition.CenterParent; BackColor = Color.FromArgb(245, 247, 250); MaximizeBox = true; AutoScaleMode = AutoScaleMode.Dpi;
        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(16, 8) };
        tabs.TabPages.Add(EmployeesPage()); tabs.TabPages.Add(HolidaysPage()); tabs.TabPages.Add(WorkdayTypesPage()); tabs.TabPages.Add(TolerancePage()); Controls.Add(tabs);
        Shown += (_, _) => FitInsideWorkingArea();
    }

    void FitInsideWorkingArea()
    {
        var area = Screen.FromControl(Owner ?? this).WorkingArea; var width = Math.Min(1180, Math.Max(MinimumSize.Width, area.Width - 24)); var height = Math.Min(760, Math.Max(MinimumSize.Height, area.Height - 24)); Bounds = new Rectangle(area.Left + Math.Max(0, (area.Width - width) / 2), area.Top + Math.Max(0, (area.Height - height) / 2), Math.Min(width, area.Width), Math.Min(height, area.Height));
    }

    static Button Button(string text, Color color) => new() { Text = text, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, AutoSize = true, Height = 36, FlatAppearance = { BorderSize = 0 } };
    static DataGridView Grid() => new() { AllowUserToAddRows = false, AllowUserToResizeColumns = false, AllowUserToResizeRows = false, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
    sealed record EmployeeChoice(string Id, string Display) { public override string ToString() => Display; }
}
