using AsistenciaSync.Models;

namespace AsistenciaSync.UI;

internal sealed partial class CustomizationForm
{
    TabPage TolerancePage()
    {
        var page = new TabPage("Tiempo de tolerancia") { BackColor = Color.FromArgb(245, 247, 250), Padding = new Padding(24) };
        var card = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(24),
            BackColor = Color.White,
            Anchor = AnchorStyles.None
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        var title = new Label { Text = "Tiempo de tolerancia", AutoSize = true, Font = new Font("Segoe UI", 17, FontStyle.Bold), ForeColor = Color.FromArgb(24, 57, 92), Anchor = AnchorStyles.Left };
        card.Controls.Add(title, 0, 0); card.SetColumnSpan(title, 2);
        var help = new Label { Text = "Minutos adicionales permitidos después de la hora de ingreso configurada.", AutoSize = false, Dock = DockStyle.Fill, ForeColor = Color.DimGray, TextAlign = ContentAlignment.MiddleLeft };
        card.Controls.Add(help, 0, 1); card.SetColumnSpan(help, 2);
        card.Controls.Add(new Label { Text = "Minutos de tolerancia", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        var minutes = new NumericUpDown { Minimum = 0, Maximum = 180, Value = AttendanceConfigurationStore.ReadToleranceMinutes(settings), Width = 120, Anchor = AnchorStyles.Left };
        card.Controls.Add(minutes, 1, 2);
        var save = Button("Guardar tolerancia", Color.FromArgb(35, 91, 151));
        save.Anchor = AnchorStyles.Left;
        save.Click += (_, _) =>
        {
            try
            {
                AttendanceConfigurationStore.SaveToleranceMinutes(settings, (int)minutes.Value);
                MessageBox.Show(this, "Tiempo de tolerancia guardado correctamente.", "Tiempo de tolerancia", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "No se pudo guardar", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };
        card.Controls.Add(save, 1, 3);

        void CenterCard() { card.Location = new Point(Math.Max(24, (page.ClientSize.Width - card.Width) / 2), Math.Max(24, (page.ClientSize.Height - card.Height) / 3)); }
        page.Controls.Add(card); page.Resize += (_, _) => CenterCard(); page.HandleCreated += (_, _) => CenterCard();
        return page;
    }
}
