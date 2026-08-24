namespace AsistenciaSync.UI;

internal sealed class CreateEmployeeForm : Form
{
    readonly TextBox id = new() { Width = 250 };
    readonly TextBox name = new() { Width = 250 };
    public string EmployeeId => id.Text.Trim();
    public string EmployeeName => name.Text.Trim();

    public CreateEmployeeForm()
    {
        Text = "Crear trabajador"; Width = 450; Height = 240; StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        var fields = new TableLayoutPanel { Dock = DockStyle.Top, Height = 105, Padding = new Padding(22), ColumnCount = 2 }; fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fields.Controls.Add(new Label { Text = "ID empleado", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0); fields.Controls.Add(id, 1, 0); fields.Controls.Add(new Label { Text = "Nombre", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1); fields.Controls.Add(name, 1, 1);
        var save = new Button { Text = "Crear", DialogResult = DialogResult.OK, AutoSize = true, Height = 36, BackColor = Color.FromArgb(35, 91, 151), ForeColor = Color.White, FlatStyle = FlatStyle.Flat }; save.Click += (_, _) => { if (!uint.TryParse(EmployeeId, out _) || EmployeeName.Length == 0) { MessageBox.Show(this, "Ingrese un ID numérico y un nombre.", "Datos inválidos", MessageBoxButtons.OK, MessageBoxIcon.Warning); DialogResult = DialogResult.None; } };
        var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, AutoSize = true, Height = 36 }; var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(22, 8, 0, 0) }; actions.Controls.Add(save); actions.Controls.Add(cancel); Controls.Add(fields); Controls.Add(actions); AcceptButton = save; CancelButton = cancel;
    }
}
