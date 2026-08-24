using System.Drawing.Printing;
using System.Text;
using AsistenciaSync.Configuration;
using AsistenciaSync.Services;

namespace AsistenciaSync.UI;

internal sealed class ReportForm : Form
{
    readonly ReportDocument document;
    readonly AppSettings settings;
    readonly List<string[]> allRows;
    readonly DataGridView grid = Grid();
    readonly TextBox search = new() { Width = 260, PlaceholderText = "Nombre o ID" };
    readonly DateTimePicker from = new() { Format = DateTimePickerFormat.Short, Width = 112 };
    readonly DateTimePicker to = new() { Format = DateTimePickerFormat.Short, Width = 112 };
    readonly Label selectedEmployee = new() { AutoSize = true, ForeColor = Color.Black };
    readonly Panel summary = new() { Dock = DockStyle.Bottom, Height = 175, BackColor = Color.White, Padding = new Padding(24, 12, 24, 10) };
    readonly PrintDocument printDocument = new();
    int printRow;

    public ReportForm(ReportDocument document, AppSettings settings)
    {
        this.document = document; this.settings = settings; allRows = ParseRows(document.DetailCsv); Text = "Hacer reporte"; Width = 1180; Height = 720; MinimumSize = new Size(900, 600); StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = true; WindowState = FormWindowState.Maximized; BackColor = Color.White;
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); var monthEnd = monthStart.AddMonths(1).AddDays(-1); from.MinDate = monthStart; from.MaxDate = monthEnd; to.MinDate = monthStart; to.MaxDate = monthEnd; from.Value = document.From < monthStart ? monthStart : document.From; to.Value = document.To > monthEnd ? monthEnd : document.To;
        var iconPath = Path.Combine(AppContext.BaseDirectory, "LOGO.ico"); if (File.Exists(iconPath)) Icon = new Icon(iconPath);
        var header = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = Color.White, Padding = new Padding(24, 14, 24, 8) }; header.Controls.Add(new Label { Text = "HACER REPORTE", AutoSize = true, Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = Color.Black, Location = new Point(24, 12) }); header.Controls.Add(new Label { Text = "Busque un trabajador para consultar sus días", AutoSize = true, ForeColor = Color.Black, Location = new Point(26, 48) });
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 62, BackColor = Color.White, Padding = new Padding(24, 12, 24, 8), WrapContents = false }; toolbar.Controls.Add(new Label { Text = "Desde", AutoSize = true, ForeColor = Color.Black, Margin = new Padding(0, 7, 5, 0) }); toolbar.Controls.Add(from); toolbar.Controls.Add(new Label { Text = "Hasta", AutoSize = true, ForeColor = Color.Black, Margin = new Padding(15, 7, 5, 0) }); toolbar.Controls.Add(to); toolbar.Controls.Add(new Label { Text = "Trabajador", AutoSize = true, ForeColor = Color.Black, Margin = new Padding(20, 7, 5, 0) }); toolbar.Controls.Add(search); var find = Button("Buscar", Color.FromArgb(35, 35, 35)); find.Click += (_, _) => SearchEmployee(); toolbar.Controls.Add(find); selectedEmployee.Margin = new Padding(18, 7, 0, 0); toolbar.Controls.Add(selectedEmployee);
        grid.Visible = false; var instruction = new Label { Text = "Busque por nombre o ID para mostrar el reporte.", AutoSize = true, ForeColor = Color.Black, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }; var tableHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 8, 24, 8), BackColor = Color.White }; tableHost.Controls.Add(instruction); tableHost.Controls.Add(grid);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, Padding = new Padding(24, 8, 0, 8), BackColor = Color.White }; var download = Button("Descargar CSV", Color.FromArgb(35, 35, 35)); download.Click += (_, _) => Download(); var preview = Button("Vista previa", Color.FromArgb(75, 75, 75)); preview.Click += (_, _) => Preview(); var print = Button("Imprimir", Color.Black); print.Click += (_, _) => Print(); var close = Button("Cerrar", Color.FromArgb(120, 120, 120)); close.Click += (_, _) => Close(); actions.Controls.AddRange(new Control[] { download, preview, print, close });
        Controls.Add(tableHost); Controls.Add(summary); Controls.Add(toolbar); Controls.Add(actions); Controls.Add(header); search.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SearchEmployee(); } }; printDocument.DefaultPageSettings.Landscape = true; printDocument.DefaultPageSettings.Margins = new Margins(35, 35, 35, 35); printDocument.PrintPage += PrintPage;
    }

    void SearchEmployee()
    {
        var query = search.Text.Trim(); if (query.Length == 0) { MessageBox.Show(this, "Escriba el nombre o ID del trabajador.", "Búsqueda", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var matches = allRows.Where(r => r.Length > 1 && (r[0].Contains(query, StringComparison.OrdinalIgnoreCase) || r[1].Contains(query, StringComparison.OrdinalIgnoreCase))).ToList(); if (matches.Count == 0) { MessageBox.Show(this, "No se encontró el trabajador en el periodo seleccionado.", "Búsqueda", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var employeeId = matches[0][0]; var employeeName = matches[0][1]; var rows = matches.Where(r => DateTime.TryParseExact(r[2], "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var date) && date.Date >= from.Value.Date && date.Date <= to.Value.Date).ToList(); LoadEmployeeRows(rows); selectedEmployee.Text = $"{employeeId} · {employeeName}"; selectedEmployee.ForeColor = Color.Black;
    }

    void LoadEmployeeRows(List<string[]> rows)
    {
        grid.Visible = true; grid.CellContentClick -= EditRow; grid.Rows.Clear(); grid.Columns.Clear(); var headers = new[] { "Fecha", "Tipo jornada", "Horario", "Ingreso 1", "Salida 1", "Ingreso 2", "Salida 2", "Cumplió", "Estado", "Horas esperadas", "Tiempo fuera" }; foreach (var header in headers) grid.Columns.Add(header, header); foreach (var row in rows) grid.Rows.Add(row[2], row[3], row[4], row[5], row[6], row[7], row[8], row[15], row[16], row[10], row[14]); grid.Columns["Horas esperadas"].Visible = false; grid.Columns["Tiempo fuera"].Visible = false; var edit = new DataGridViewButtonColumn { Name = "Editar", HeaderText = "Acción", Text = "Editar", UseColumnTextForButtonValue = true, FillWeight = 70 }; grid.Columns.Add(edit); grid.CellContentClick += EditRow; UpdateSummary();
    }

    void EditRow(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != grid.Columns["Editar"].Index) return; using var editor = new ReportRowEditForm(grid.Rows[e.RowIndex], StatusCatalog.Load(settings).Select(x => x.Name)); if (editor.ShowDialog(this) == DialogResult.OK) { grid.InvalidateRow(e.RowIndex); UpdateSummary(); }
    }

    void UpdateSummary()
    {
        var morning = 0; var afternoon = 0; var outside = 0; var expected = 0; foreach (DataGridViewRow row in grid.Rows) { var discontinuous = string.Equals(row.Cells["Tipo jornada"].Value?.ToString(), "Discontinua", StringComparison.OrdinalIgnoreCase); var first = SegmentMinutes(row, "Ingreso 1", "Salida 1"); var second = SegmentMinutes(row, "Ingreso 2", "Salida 2"); morning += first; afternoon += discontinuous ? second : 0; outside += ParseHours(row.Cells["Tiempo fuera"].Value?.ToString()); expected += ParseHours(row.Cells["Horas esperadas"].Value?.ToString()); }
        summary.Controls.Clear(); AddSummaryLine("Horas trabajadas", FormatHours(morning + afternoon)); AddSummaryLine("Horas trabajadas en la mañana", FormatHours(morning)); AddSummaryLine("Horas trabajadas en la tarde", FormatHours(afternoon)); AddSummaryLine("Horas fuera de horario", FormatHours(outside)); var total = Math.Max(0, morning + afternoon - outside); AddSummaryLine("Total", FormatHours(total)); var debt = Math.Max(0, expected - total); AddSummaryLine(debt == 0 ? "No debe horas" : $"Debe {FormatHours(debt)}", "");
    }

    void AddSummaryLine(string title, string value) { var row = new Panel { Dock = DockStyle.Top, Height = 24 }; row.Controls.Add(new Label { Text = title, AutoSize = true, ForeColor = Color.Black, Font = new Font("Segoe UI", 9, FontStyle.Bold), Location = new Point(0, 3) }); row.Controls.Add(new Label { Text = value, AutoSize = true, ForeColor = Color.Black, Location = new Point(280, 3) }); summary.Controls.Add(row); summary.Controls.SetChildIndex(row, 0); }
    static int SegmentMinutes(DataGridViewRow row, string entry, string exit) { var a = DateTime.TryParse(row.Cells[entry].Value?.ToString(), out var start); var b = DateTime.TryParse(row.Cells[exit].Value?.ToString(), out var end); return a && b && end >= start ? (int)(end - start).TotalMinutes : 0; }
    static int ParseHours(string? value) { var parts = (value ?? "").Split(':'); return parts.Length == 2 && int.TryParse(parts[0], out var h) && int.TryParse(parts[1], out var m) ? h * 60 + m : 0; }
    static string FormatHours(int minutes) => $"{minutes / 60} horas y {minutes % 60:00} minutos";

    void Download() { if (!grid.Visible || grid.Rows.Count == 0) return; using var dialog = new SaveFileDialog { FileName = $"reporte_{DateTime.Now:yyyyMMdd_HHmmss}.csv", Filter = "Archivo CSV|*.csv", InitialDirectory = document.DownloadFolder }; if (dialog.ShowDialog(this) == DialogResult.OK) File.WriteAllText(dialog.FileName, GridCsv(), new UnicodeEncoding(false, true)); }
    string GridCsv() { var columns = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Name != "Editar").ToList(); var b = new StringBuilder("sep=;\r\n"); b.AppendLine(string.Join(';', columns.Select(c => Q(c.HeaderText)))); foreach (DataGridViewRow row in grid.Rows) b.AppendLine(string.Join(';', columns.Select(c => Q(Convert.ToString(row.Cells[c.Index].Value) ?? "")))); return b.ToString(); }
    void Preview() { if (!grid.Visible || grid.Rows.Count == 0) return; printRow = 0; using var preview = new PrintPreviewDialog { Document = printDocument, Width = 1100, Height = 760, UseAntiAlias = true }; preview.ShowDialog(this); }
    void Print() { if (!grid.Visible || grid.Rows.Count == 0) return; using var dialog = new PrintDialog { Document = printDocument, UseEXDialog = true }; if (dialog.ShowDialog(this) == DialogResult.OK) { printRow = 0; printDocument.Print(); } }
    void PrintPage(object? sender, PrintPageEventArgs e) { var g = e.Graphics!; using var font = new Font("Segoe UI", 8); using var bold = new Font("Segoe UI", 8, FontStyle.Bold); var x = e.MarginBounds.Left; var y = e.MarginBounds.Top; g.DrawString("REPORTE DE ASISTENCIA", new Font("Segoe UI", 14, FontStyle.Bold), Brushes.Black, x, y); y += 28; foreach (DataGridViewColumn column in grid.Columns) { if (column.Name == "Editar") continue; g.DrawString(column.HeaderText, bold, Brushes.Black, x, y); x += 90; } y += 20; while (printRow < grid.Rows.Count) { if (y > e.MarginBounds.Bottom - 20) { e.HasMorePages = true; return; } x = e.MarginBounds.Left; foreach (DataGridViewColumn column in grid.Columns) { if (column.Name == "Editar") continue; g.DrawString(grid.Rows[printRow].Cells[column.Index].Value?.ToString() ?? "", font, Brushes.Black, x, y); x += 90; } y += 18; printRow++; } e.HasMorePages = false; }
    static List<string[]> ParseRows(string csv) { var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries); var start = lines.Length > 0 && lines[0].StartsWith("sep=") ? 2 : 1; return lines.Skip(start).Select(line => Parse(line, ';')).Where(x => x.Count >= 17).Select(x => x.ToArray()).ToList(); }
    static List<string> Parse(string line, char delimiter) { var result = new List<string>(); var value = new StringBuilder(); var quoted = false; foreach (var c in line) { if (c == '"') quoted = !quoted; else if (c == delimiter && !quoted) { result.Add(value.ToString()); value.Clear(); } else value.Append(c); } result.Add(value.ToString()); return result; }
    static DataGridView Grid() => new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, BackgroundColor = Color.White, RowHeadersVisible = false, EnableHeadersVisualStyles = false, ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.White, ForeColor = Color.Black, Font = new Font("Segoe UI", 9, FontStyle.Bold) }, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.Black, BackColor = Color.White, SelectionForeColor = Color.Black, SelectionBackColor = Color.FromArgb(230, 230, 230) } };
    static Button Button(string text, Color color) => new() { Text = text, AutoSize = true, Height = 34, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, Padding = new Padding(12, 0, 12, 0), Margin = new Padding(0, 0, 8, 0) };
    static string Q(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
}
