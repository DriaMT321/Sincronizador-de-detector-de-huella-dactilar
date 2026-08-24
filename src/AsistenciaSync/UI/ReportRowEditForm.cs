namespace AsistenciaSync.UI;

internal sealed class ReportRowEditForm : Form
{
    readonly DataGridViewRow row;
    readonly Dictionary<string, Control> fields = new(StringComparer.OrdinalIgnoreCase);

    public ReportRowEditForm(DataGridViewRow row, IEnumerable<string> statusOptions)
    {
        this.row = row; Text = "Editar registro del reporte"; Width = 560; Height = 720; MinimumSize = new Size(500, 500); StartPosition = FormStartPosition.CenterParent; Icon = row.DataGridView?.FindForm()?.Icon;
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(22), ColumnCount = 2 }; table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (DataGridViewColumn column in row.DataGridView!.Columns)
        {
            if (column.Name == "Editar") continue;
            var value = Convert.ToString(row.Cells[column.Index].Value) ?? ""; Control editor;
            if (column.Name == "Estado")
            {
                var state = new ComboBox { Width = 270, DropDownStyle = ComboBoxStyle.DropDownList }; state.Items.AddRange(statusOptions.Cast<object>().ToArray()); if (!state.Items.Contains(value)) state.Items.Add(value); state.SelectedItem = value; editor = state;
            }
            else editor = new TextBox { Text = value, Width = 270 };
            fields[column.Name] = editor; var index = table.RowCount++; table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); table.Controls.Add(new Label { Text = column.Name == "Estado" ? "Estado de asistencia" : column.HeaderText, AutoSize = true, Anchor = AnchorStyles.Left }, 0, index); table.Controls.Add(editor, 1, index);
        }
        var save = new Button { Text = "Guardar cambios", DialogResult = DialogResult.OK, AutoSize = true, Height = 34, BackColor = Color.FromArgb(35, 91, 151), ForeColor = Color.White, FlatStyle = FlatStyle.Flat }; save.Click += (_, _) => Apply(); var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, AutoSize = true, Height = 34 };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, Padding = new Padding(22, 8, 0, 0) }; actions.Controls.AddRange(new Control[] { save, cancel }); Controls.Add(table); Controls.Add(actions); AcceptButton = save; CancelButton = cancel;
    }

    void Apply() { foreach (var item in fields) row.Cells[item.Key].Value = item.Value is ComboBox combo ? combo.Text : ((TextBox)item.Value).Text; }
}
