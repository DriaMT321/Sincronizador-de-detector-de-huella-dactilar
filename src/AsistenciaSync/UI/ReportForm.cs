using System.Drawing.Printing;
using System.Text;

using AsistenciaSync.Configuration;
using AsistenciaSync.Services;

namespace AsistenciaSync.UI;

internal sealed class ReportForm : Form
{
    readonly ReportDocument document;
    readonly AppSettings settings;
    readonly DataGridView detailGrid = Grid(), summaryGrid = Grid();
    readonly TextBox search = new() { Width = 240, PlaceholderText = "Buscar empleado..." };
    readonly DateTimePicker filterFrom = new() { Format = DateTimePickerFormat.Short, Width = 105 };
    readonly DateTimePicker filterTo = new() { Format = DateTimePickerFormat.Short, Width = 105 };
    readonly ComboBox filterState = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    readonly Label visibleRows = new() { AutoSize = true, ForeColor = Color.FromArgb(95, 105, 120), Margin = new Padding(12, 10, 0, 0) };
    readonly PrintDocument printDocument = new();
    DataGridView activeGrid;
    int printRow;

    public ReportForm(ReportDocument document, AppSettings settings)
    {
        this.document = document; this.settings = settings; activeGrid = detailGrid;
        filterFrom.MinDate = document.From; filterFrom.MaxDate = document.To; filterFrom.Value = document.From; filterTo.MinDate = document.From; filterTo.MaxDate = document.To; filterTo.Value = document.To;
        Text = "Reporte de asistencia"; Width = 1180; Height = 720; MinimumSize = new Size(900, 560); StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = true; WindowState = FormWindowState.Maximized; BackColor = Color.FromArgb(244, 247, 251);
        var iconPath = Path.Combine(AppContext.BaseDirectory, "LOGO.ico"); if (File.Exists(iconPath)) Icon = new Icon(iconPath);
        LoadCsv(detailGrid, document.DetailCsv); LoadCsv(summaryGrid, document.SummaryCsv); AddEditButton(detailGrid); AddEditButton(summaryGrid); StyleGrid(detailGrid); StyleGrid(summaryGrid); ApplyStatusColors(detailGrid);

        var header = new Panel { Dock = DockStyle.Top, Height = 94, BackColor = Color.FromArgb(24, 57, 92), Padding = new Padding(24, 13, 24, 10) };
        header.Controls.Add(new Label { Text = "REPORTE DE ASISTENCIA", ForeColor = Color.White, Font = new Font("Segoe UI", 17, FontStyle.Bold), AutoSize = true, Location = new Point(24, 12) });
        header.Controls.Add(new Label { Text = "Detalle y resumen de la plantilla", ForeColor = Color.FromArgb(190, 211, 232), Font = new Font("Segoe UI", 9), AutoSize = true, Location = new Point(26, 48) });

        var cards = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 86, Padding = new Padding(20, 12, 20, 8), BackColor = Color.FromArgb(244, 247, 251), WrapContents = false };
        cards.Controls.Add(Card("REGISTROS", detailGrid.Rows.Count.ToString("N0"), Color.FromArgb(35, 91, 151)));
        cards.Controls.Add(Card("COMPLETOS", CountStatus("Completo").ToString("N0"), Color.FromArgb(36, 145, 103)));
        cards.Controls.Add(Card("AUSENCIAS", CountStatus("Ausente").ToString("N0"), Color.FromArgb(207, 78, 78)));
        cards.Controls.Add(Card("HORAS TRABAJADAS", TotalWorked(), Color.FromArgb(211, 133, 37)));

        filterState.Items.Add("Todos los estados"); foreach (var state in detailGrid.Rows.Cast<DataGridViewRow>().Select(r => r.Cells["Estado"].Value?.ToString()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).Distinct().OrderBy(s => s)) filterState.Items.Add(state); filterState.SelectedIndex = 0;
        var toolbar = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = Color.White, Padding = new Padding(20, 8, 20, 8) };
        toolbar.Controls.Add(new Label { Text = "Nombre:", AutoSize = true, Location = new Point(20, 15), ForeColor = Color.FromArgb(75, 85, 100) }); search.Location = new Point(75, 8); toolbar.Controls.Add(search);
        toolbar.Controls.Add(new Label { Text = "Desde:", AutoSize = true, Location = new Point(335, 15), ForeColor = Color.FromArgb(75, 85, 100) }); filterFrom.Location = new Point(380, 8); toolbar.Controls.Add(filterFrom);
        toolbar.Controls.Add(new Label { Text = "Hasta:", AutoSize = true, Location = new Point(500, 15), ForeColor = Color.FromArgb(75, 85, 100) }); filterTo.Location = new Point(545, 8); toolbar.Controls.Add(filterTo);
        toolbar.Controls.Add(new Label { Text = "Estado:", AutoSize = true, Location = new Point(665, 15), ForeColor = Color.FromArgb(75, 85, 100) }); filterState.Location = new Point(715, 8); toolbar.Controls.Add(filterState); visibleRows.Location = new Point(925, 15); toolbar.Controls.Add(visibleRows);
        search.TextChanged += (_, _) => FilterActive(); filterFrom.ValueChanged += (_, _) => FilterActive(); filterTo.ValueChanged += (_, _) => FilterActive(); filterState.SelectedIndexChanged += (_, _) => FilterActive();

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(16, 8) };
        tabs.TabPages.Add(Page("Detalle diario", detailGrid)); tabs.TabPages.Add(Page("Resumen por empleado", summaryGrid)); tabs.SelectedIndexChanged += (_, _) => { activeGrid = tabs.SelectedIndex == 0 ? detailGrid : summaryGrid; FilterActive(); };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(20, 12, 20, 10), BackColor = Color.White };
        var downloadDetail = Button("Descargar detalle", Color.FromArgb(35, 91, 151)); downloadDetail.Click += (_, _) => DownloadGrid(detailGrid, "reporte_detalle");
        var downloadSummary = Button("Descargar resumen", Color.FromArgb(80, 112, 145)); downloadSummary.Click += (_, _) => DownloadGrid(summaryGrid, "reporte_resumen");
        var preview = Button("Vista previa", Color.FromArgb(80, 112, 145)); preview.Click += (_, _) => Preview();
        var print = Button("Imprimir", Color.FromArgb(36, 145, 103)); print.Click += (_, _) => Print();
        var close = Button("Cerrar", Color.FromArgb(110, 118, 128)); close.Click += (_, _) => Close();
        actions.Controls.AddRange(new Control[] { downloadDetail, downloadSummary, preview, print, close });

        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 0, 20, 0), BackColor = Color.FromArgb(244, 247, 251) }; body.Controls.Add(tabs); body.Controls.Add(toolbar); body.Controls.Add(cards);
        Controls.Add(body); Controls.Add(actions); Controls.Add(header); printDocument.DefaultPageSettings.Landscape = true; printDocument.DefaultPageSettings.Margins = new Margins(35, 35, 35, 35); printDocument.PrintPage += PrintPage; FilterActive();
    }

    Panel Card(string title, string value, Color accent)
    {
        var card = new Panel { Width = 210, Height = 62, BackColor = Color.White, Margin = new Padding(0, 0, 12, 0), Padding = new Padding(14, 8, 10, 6) };
        card.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 5, BackColor = accent });
        card.Controls.Add(new Label { Text = title, AutoSize = true, ForeColor = Color.FromArgb(105, 115, 128), Font = new Font("Segoe UI", 8, FontStyle.Bold), Location = new Point(18, 8) });
        card.Controls.Add(new Label { Text = value, AutoSize = true, ForeColor = Color.FromArgb(35, 45, 58), Font = new Font("Segoe UI", 16, FontStyle.Bold), Location = new Point(18, 27) }); return card;
    }

    int CountStatus(string text) => detailGrid.Columns.Contains("Estado") ? detailGrid.Rows.Cast<DataGridViewRow>().Count(r => (r.Cells["Estado"].Value?.ToString() ?? "").StartsWith(text, StringComparison.OrdinalIgnoreCase)) : 0;
    string TotalWorked() => detailGrid.Columns.Contains("Horas trabajadas") ? TotalTime(detailGrid, "Horas trabajadas") : "0:00";
    static string TotalTime(DataGridView grid, string column) { var total = 0; foreach (DataGridViewRow row in grid.Rows) { var value = row.Cells[column].Value?.ToString() ?? ""; var parts = value.Split(':'); if (parts.Length == 2 && int.TryParse(parts[0], out var h) && int.TryParse(parts[1], out var m)) total += h * 60 + m; } return $"{total / 60}:{total % 60:00}"; }
    static TabPage Page(string title, DataGridView grid) { var page = new TabPage(title) { BackColor = Color.FromArgb(244, 247, 251), Padding = new Padding(0, 8, 0, 0) }; page.Controls.Add(grid); return page; }
    static DataGridView Grid() => new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.None, GridColor = Color.FromArgb(226, 231, 238), RowHeadersVisible = false, EnableHeadersVisualStyles = false, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, RowTemplate = { Height = 31 } };
    static Button Button(string text, Color color) => new() { Text = text, AutoSize = true, Height = 34, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold), FlatAppearance = { BorderSize = 0 }, Padding = new Padding(12, 0, 12, 0), Margin = new Padding(0, 0, 8, 0) };

    static void StyleGrid(DataGridView grid)
    {
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(35, 91, 151), ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold), Padding = new Padding(6, 0, 6, 0), Alignment = DataGridViewContentAlignment.MiddleLeft };
        grid.ColumnHeadersHeight = 38; grid.DefaultCellStyle = new DataGridViewCellStyle { Font = new Font("Segoe UI", 9), ForeColor = Color.FromArgb(55, 65, 80), BackColor = Color.White, SelectionBackColor = Color.FromArgb(220, 234, 248), SelectionForeColor = Color.FromArgb(25, 45, 70), Padding = new Padding(6, 0, 6, 0) }; grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(248, 250, 253) };
    }

    void ApplyStatusColors(DataGridView grid) { var options = StatusCatalog.Load(settings); grid.CellFormatting += (_, e) => { if (e.RowIndex < 0 || e.ColumnIndex < 0 || grid.Columns[e.ColumnIndex].Name != "Estado") return; var text = e.Value?.ToString() ?? ""; var option = options.FirstOrDefault(x => text.StartsWith(x.Name, StringComparison.OrdinalIgnoreCase)); var color = option is not null && ColorTranslator.FromHtml(option.Color) is var parsed ? parsed : Color.FromArgb(65, 75, 90); e.CellStyle = new DataGridViewCellStyle(e.CellStyle) { ForeColor = color, Font = new Font("Segoe UI", 9, FontStyle.Bold) }; }; }
    void FilterActive()
    {
        var query = search.Text.Trim(); var isDetail = activeGrid == detailGrid; var selectedState = filterState.SelectedIndex > 0 ? filterState.SelectedItem?.ToString() : null; var shown = 0;
        foreach (DataGridViewRow row in activeGrid.Rows)
        {
            var text = string.Join(" ", row.Cells.Cast<DataGridViewCell>().Select(c => c.Value?.ToString() ?? "")); var matchesName = query.Length == 0 || text.Contains(query, StringComparison.OrdinalIgnoreCase); var matchesDate = !isDetail || !DateTime.TryParseExact(row.Cells["Fecha"].Value?.ToString(), "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var date) || (date.Date >= filterFrom.Value.Date && date.Date <= filterTo.Value.Date); var matchesState = !isDetail || selectedState is null || string.Equals(row.Cells["Estado"].Value?.ToString(), selectedState, StringComparison.OrdinalIgnoreCase); row.Visible = matchesName && matchesDate && matchesState; if (row.Visible) shown++;
        }
        visibleRows.Text = $"{shown:N0} filas visibles";
    }
    static void LoadCsv(DataGridView grid, string content) { var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries); if (lines.Length == 0) return; var start = lines[0].StartsWith("sep=") ? 1 : 0; var delimiter = start == 1 ? lines[0][4] : ','; if (start >= lines.Length) return; foreach (var h in Parse(lines[start], delimiter)) grid.Columns.Add(h, h); for (var i = start + 1; i < lines.Length; i++) { var values = Parse(lines[i], delimiter); if (values.Count == grid.Columns.Count) grid.Rows.Add(values.ToArray()); } }
    void AddEditButton(DataGridView grid) { var edit = new DataGridViewButtonColumn { Name = "Editar", HeaderText = "Acción", Text = "Editar", UseColumnTextForButtonValue = true, FillWeight = 75 }; grid.Columns.Add(edit); grid.CellContentClick += (_, e) => { if (e.RowIndex < 0 || e.ColumnIndex != grid.Columns["Editar"].Index) return; using var editor = new ReportRowEditForm(grid.Rows[e.RowIndex], StatusCatalog.Load(settings).Select(x => x.Name)); if (editor.ShowDialog(grid.FindForm()) == DialogResult.OK) grid.InvalidateRow(e.RowIndex); }; }
    static List<string> Parse(string line, char delimiter) { var result = new List<string>(); var current = new StringBuilder(); var quoted = false; for (var i = 0; i < line.Length; i++) { var c = line[i]; if (c == '"') { if (quoted && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; } else quoted = !quoted; } else if (c == delimiter && !quoted) { result.Add(current.ToString()); current.Clear(); } else current.Append(c); } result.Add(current.ToString()); return result; }
    void DownloadGrid(DataGridView grid, string baseName) { Directory.CreateDirectory(document.DownloadFolder); using var dialog = new SaveFileDialog { FileName = $"{baseName}_{document.GeneratedAt:yyyyMMdd_HHmmss}.csv", Filter = "Archivo CSV|*.csv", InitialDirectory = document.DownloadFolder }; if (dialog.ShowDialog(this) == DialogResult.OK) File.WriteAllText(dialog.FileName, GridCsv(grid), new UnicodeEncoding(false, true)); }
    static string GridCsv(DataGridView grid) { var builder = new StringBuilder(); builder.AppendLine("sep=;"); var columns = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Name != "Editar").ToList(); builder.AppendLine(string.Join(';', columns.Select(c => Q(c.HeaderText)))); foreach (DataGridViewRow row in grid.Rows) builder.AppendLine(string.Join(';', columns.Select(c => Q(Convert.ToString(row.Cells[c.Index].Value) ?? "")))); return builder.ToString(); }
    static string Q(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    void Preview() { printRow = 0; using var preview = new PrintPreviewDialog { Document = printDocument, Width = 1100, Height = 760, UseAntiAlias = true }; preview.ShowDialog(this); }
    void Print() { using var dialog = new PrintDialog { Document = printDocument, UseEXDialog = true, AllowSomePages = false }; if (dialog.ShowDialog(this) == DialogResult.OK) { printRow = 0; try { printDocument.Print(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "No se pudo imprimir", MessageBoxButtons.OK, MessageBoxIcon.Error); } } }

    void PrintPage(object? sender, PrintPageEventArgs e)
    {
        var grid = activeGrid; var g = e.Graphics!; using var titleFont = new Font("Segoe UI", 14, FontStyle.Bold); using var smallFont = new Font("Segoe UI", 7); using var headerFont = new Font("Segoe UI", 7, FontStyle.Bold); using var pen = new Pen(Color.Black, 0.7f);
        var bounds = e.MarginBounds; var columns = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Name != "Editar").ToList(); var widths = columns.Select(c => Math.Max(42, Math.Min(115, TextRenderer.MeasureText(c.HeaderText, headerFont).Width + 12))).ToArray(); var total = widths.Sum(); var scale = Math.Min(1.0, (double)bounds.Width / total); var y = bounds.Top;
        g.DrawString("REPORTE DE ASISTENCIA", titleFont, Brushes.Black, bounds.Left, y); y += 24; g.DrawString(grid == detailGrid ? "Detalle diario" : "Resumen por empleado", smallFont, Brushes.Black, bounds.Left, y); y += 20;
        var x = bounds.Left; for (var i = 0; i < columns.Count; i++) { var w = (int)(widths[i] * scale); g.DrawRectangle(pen, x, y, w, 24); g.DrawString(columns[i].HeaderText, headerFont, Brushes.Black, new RectangleF(x + 3, y + 4, w - 6, 16)); x += w; } y += 24;
        while (printRow < grid.Rows.Count)
        {
            if (y + 20 > bounds.Bottom) { e.HasMorePages = true; return; }
            x = bounds.Left; for (var i = 0; i < columns.Count; i++) { var w = (int)(widths[i] * scale); var text = grid.Rows[printRow].Cells[columns[i].Index].Value?.ToString() ?? ""; g.DrawRectangle(pen, x, y, w, 20); g.DrawString(text, smallFont, Brushes.Black, new RectangleF(x + 3, y + 3, w - 6, 14)); x += w; } y += 20; printRow++;
        }
        e.HasMorePages = false;
    }
}
