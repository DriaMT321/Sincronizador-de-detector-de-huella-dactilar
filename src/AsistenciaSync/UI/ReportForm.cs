using System.Drawing.Printing;
using System.Text;
using AsistenciaSync.Configuration;
using AsistenciaSync.Models;
using AsistenciaSync.Services;

namespace AsistenciaSync.UI;

internal sealed class ReportForm : Form
{
    readonly ReportDocument document;
    readonly AppSettings settings;
    readonly List<string[]> allRows;
    List<string[]> currentRows = new();
    readonly DataGridView grid = Grid();
    readonly TextBox search = new() { Width = 260, PlaceholderText = "Nombre o ID" };
    readonly DateTimePicker from = new() { Format = DateTimePickerFormat.Short, Width = 112 };
    readonly DateTimePicker to = new() { Format = DateTimePickerFormat.Short, Width = 112 };
    readonly Label selectedEmployee = new() { AutoSize = true, ForeColor = Color.Black };
    readonly Label instruction = new() { Text = "Busque por nombre o ID para mostrar el reporte.", AutoSize = true, Font = new Font("Segoe UI", 11), ForeColor = Color.Black, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
    readonly FlowLayoutPanel summaryFlow = new() { FlowDirection = FlowDirection.TopDown, WrapContents = false, Width = 920, Height = 360, AutoScroll = false, BackColor = Color.White, Padding = new Padding(0) };
    readonly Panel summary = new() { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(24, 20, 24, 10) };
    readonly PrintDocument printDocument = new();
    readonly int toleranceMinutes;
    bool changingDates;
    bool applyingLayout;
    int printRow;

    static readonly Color Accent = Color.FromArgb(35, 91, 151);
    static readonly Color Ink = Color.FromArgb(33, 37, 41);
    static readonly Color Muted = Color.FromArgb(107, 116, 128);
    static readonly Color Danger = Color.FromArgb(170, 45, 45);
    static readonly Color Ok = Color.FromArgb(24, 110, 70);

    public ReportForm(ReportDocument document, AppSettings settings)
    {
        this.document = document; this.settings = settings; allRows = ParseRows(document.DetailCsv); toleranceMinutes = AttendanceConfigurationStore.ReadToleranceMinutes(settings); Text = "Hacer reporte"; Width = 1180; Height = 720; MinimumSize = new Size(900, 600); StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = true; WindowState = FormWindowState.Maximized; BackColor = Color.White;
        var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); var earliest = new DateTime(document.From.Year, document.From.Month, 1); var monthEnd = currentMonth.AddMonths(1).AddDays(-1); from.MinDate = earliest; from.MaxDate = monthEnd; to.MinDate = earliest; to.MaxDate = monthEnd; from.Value = currentMonth; to.Value = DateTime.Today; from.ValueChanged += (_, _) => KeepDatesInSameMonth(true); to.ValueChanged += (_, _) => KeepDatesInSameMonth(false);
        var iconPath = Path.Combine(AppContext.BaseDirectory, "LOGO.ico"); if (File.Exists(iconPath)) Icon = new Icon(iconPath);
        var titleLabel = new Label { Text = "HACER REPORTE", AutoSize = true, Font = new Font("Segoe UI", 22, FontStyle.Bold), ForeColor = Color.Black, Location = new Point(24, 10) }; var subtitleLabel = new Label { Text = "Busque un trabajador para consultar sus días", AutoSize = true, Font = new Font("Segoe UI", 10), ForeColor = Color.Black, Location = new Point(26, 50) }; var header = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = Color.White, Padding = new Padding(24, 14, 24, 8) }; header.Controls.Add(titleLabel); header.Controls.Add(subtitleLabel); header.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 3, BackColor = Color.FromArgb(35, 91, 151) });
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 62, BackColor = Color.White, Padding = new Padding(24, 12, 24, 8), WrapContents = false }; toolbar.Controls.Add(new Label { Text = "Desde", AutoSize = true, ForeColor = Color.Black, Margin = new Padding(0, 7, 5, 0) }); toolbar.Controls.Add(from); toolbar.Controls.Add(new Label { Text = "Hasta", AutoSize = true, ForeColor = Color.Black, Margin = new Padding(15, 7, 5, 0) }); toolbar.Controls.Add(to); toolbar.Controls.Add(new Label { Text = "Trabajador", AutoSize = true, ForeColor = Color.Black, Margin = new Padding(20, 7, 5, 0) }); toolbar.Controls.Add(search); var find = Button("Buscar", Color.FromArgb(35, 35, 35)); find.Click += (_, _) => SearchEmployee(); toolbar.Controls.Add(find); selectedEmployee.Margin = new Padding(18, 7, 0, 0); toolbar.Controls.Add(selectedEmployee);
        grid.Visible = false; grid.Font = new Font("Segoe UI", 12); grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 12, FontStyle.Bold); grid.ColumnHeadersHeight = 34; grid.RowTemplate.Height = 40; grid.CellFormatting += (_, e) => { if (e.RowIndex < 0 || e.ColumnIndex < 0) return; var text = e.Value?.ToString(); var style = e.CellStyle ?? new DataGridViewCellStyle(); var symbolSize = Math.Max(10, 14 * ResponsiveScale); if (text == "✓") { style.ForeColor = Color.FromArgb(24, 110, 70); style.Font = new Font("Segoe UI", symbolSize, FontStyle.Bold); } else if (text == "X") { style.ForeColor = Color.FromArgb(185, 55, 55); style.Font = new Font("Segoe UI", Math.Max(10, symbolSize - 1), FontStyle.Bold); } else if (text == "−") { style.ForeColor = Color.FromArgb(115, 120, 128); style.Font = new Font("Segoe UI", Math.Max(10, symbolSize - 1), FontStyle.Bold); } e.CellStyle = style; }; var tableHost = new Panel { Dock = DockStyle.Top, Height = 80, Padding = new Padding(24, 6, 24, 6), BackColor = Color.White }; tableHost.Controls.Add(instruction); tableHost.Controls.Add(grid);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, Padding = new Padding(24, 8, 0, 8), BackColor = Color.White }; var download = Button("Descargar detalle", Color.FromArgb(35, 35, 35)); download.Click += (_, _) => Download(); var summaryCsv = Button("Descargar resumen", Color.FromArgb(35, 35, 35)); summaryCsv.Click += (_, _) => DownloadSummary(); var pdf = Button("Descargar PDF", Color.FromArgb(35, 35, 35)); pdf.Click += (_, _) => DownloadPdf(); var preview = Button("Vista previa", Color.FromArgb(75, 75, 75)); preview.Click += (_, _) => Preview(); var print = Button("Imprimir", Color.Black); print.Click += (_, _) => Print(); var close = Button("Cerrar", Color.FromArgb(120, 120, 120)); close.Click += (_, _) => Close(); actions.Controls.AddRange(new Control[] { download, summaryCsv, pdf, preview, print, close });
        summary.Controls.Add(summaryFlow); var content = new Panel { Dock = DockStyle.Fill, BackColor = Color.White }; content.Controls.Add(summary); content.Controls.Add(tableHost); Controls.Add(content); Controls.Add(toolbar); Controls.Add(actions); Controls.Add(header); search.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SearchEmployee(); } }; printDocument.DefaultPageSettings.Landscape = true; printDocument.DefaultPageSettings.Margins = new Margins(35, 35, 35, 35); printDocument.PrintPage += PrintPage;
        Resize += (_, _) => ApplyResponsiveLayout(header, toolbar, tableHost, actions, titleLabel, subtitleLabel); Shown += (_, _) => ApplyResponsiveLayout(header, toolbar, tableHost, actions, titleLabel, subtitleLabel); summary.Resize += (_, _) => { SizeSummaryFlow(); if (!applyingLayout && currentRows.Count > 0) UpdateSummary(); };
    }

    float ResponsiveScale => Math.Clamp(Math.Min(ClientSize.Width / 1600f, ClientSize.Height / 900f), 0.72f, 1f);

    void ApplyResponsiveLayout(Panel header, FlowLayoutPanel toolbar, Panel tableHost, FlowLayoutPanel actions, Label title, Label subtitle)
    {
        if (applyingLayout) return; applyingLayout = true;
        try
        {
            var scale = ResponsiveScale; var horizontal = Math.Max(12, (int)(24 * scale));
            header.Height = Math.Max(62, (int)(82 * scale)); header.Padding = new Padding(horizontal, Math.Max(7, (int)(14 * scale)), horizontal, 6); title.Font = new Font("Segoe UI", Math.Max(16, 22 * scale), FontStyle.Bold); title.Location = new Point(horizontal, Math.Max(5, (int)(10 * scale))); subtitle.Font = new Font("Segoe UI", Math.Max(8, 10 * scale)); subtitle.Location = new Point(horizontal + 2, Math.Max(35, (int)(50 * scale)));
            toolbar.Height = Math.Max(46, (int)(62 * scale)); toolbar.Padding = new Padding(horizontal, Math.Max(5, (int)(12 * scale)), horizontal, 4); from.Width = Math.Max(96, (int)(112 * scale)); to.Width = from.Width; search.Width = Math.Clamp(ClientSize.Width / 5, 170, 260);
            actions.Height = Math.Max(44, (int)(54 * scale)); actions.Padding = new Padding(horizontal, Math.Max(4, (int)(8 * scale)), 0, 4); tableHost.Padding = new Padding(Math.Max(6, horizontal / 2), 4, Math.Max(6, horizontal / 2), 4);
            foreach (var button in toolbar.Controls.OfType<Button>().Concat(actions.Controls.OfType<Button>())) { button.Height = Math.Max(28, (int)(34 * scale)); button.Font = new Font("Segoe UI", Math.Max(8, 9 * scale)); button.Padding = new Padding(Math.Max(7, (int)(12 * scale)), 0, Math.Max(7, (int)(12 * scale)), 0); }
            grid.Font = new Font("Segoe UI", Math.Max(9, 12 * scale)); grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", Math.Max(9, 12 * scale), FontStyle.Bold); grid.ColumnHeadersHeight = Math.Max(26, (int)(34 * scale)); grid.RowTemplate.Height = Math.Max(27, (int)(40 * scale)); foreach (DataGridViewRow row in grid.Rows) row.Height = grid.RowTemplate.Height;
            summary.Padding = new Padding(horizontal, Math.Max(4, (int)(8 * scale)), horizontal, 4); SizeSummaryFlow(); if (grid.Visible) LayoutReportGrid(tableHost); if (currentRows.Count > 0) UpdateSummary();
        }
        finally { applyingLayout = false; }
    }

    void SizeSummaryFlow()
    {
        var horizontal = Math.Max(12, (int)(24 * ResponsiveScale));
        summaryFlow.Width = Math.Max(540, Math.Min(920, summary.ClientSize.Width - (horizontal * 2)));
        summaryFlow.Height = Math.Max(250, summary.ClientSize.Height - 8);
        summaryFlow.Left = Math.Max(0, (summary.ClientSize.Width - summaryFlow.Width) / 2);
        summaryFlow.Top = 2;
    }

    float SummaryScale => Math.Clamp(Math.Min(ResponsiveScale, summaryFlow.ClientSize.Height / 390f), 0.55f, 1f);

    void SearchEmployee()
    {
        var query = search.Text.Trim(); if (query.Length == 0) { MessageBox.Show(this, "Escriba el nombre o ID del trabajador.", "Búsqueda", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var employees = allRows
            .Where(r => r.Length > 1 && (r[0].Contains(query, StringComparison.OrdinalIgnoreCase) || r[1].Contains(query, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(r => r[0], StringComparer.OrdinalIgnoreCase)
            .Select(group => new { Id = group.Key, Name = group.First()[1] })
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (employees.Count == 0) { MessageBox.Show(this, "No se encontró el trabajador en el periodo seleccionado.", "Búsqueda", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

        var exact = employees.FirstOrDefault(x => x.Id.Equals(query, StringComparison.OrdinalIgnoreCase))
            ?? employees.FirstOrDefault(x => x.Name.Equals(query, StringComparison.OrdinalIgnoreCase));
        if (exact is null && employees.Count > 1)
        {
            MessageBox.Show(this, "La búsqueda coincide con varios trabajadores. Escriba más letras del nombre o el ID completo.", "Búsqueda", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var selected = exact ?? employees[0];
        var rows = allRows.Where(r => r.Length > 2
            && r[0].Equals(selected.Id, StringComparison.OrdinalIgnoreCase)
            && DateTime.TryParseExact(r[2], "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var date)
            && date.Date >= from.Value.Date
            && date.Date <= to.Value.Date).ToList();
        LoadEmployeeRows(rows); selectedEmployee.Text = $"{selected.Id} · {selected.Name} · Tolerancia: {toleranceMinutes} min"; selectedEmployee.ForeColor = Color.Black;
    }

    void KeepDatesInSameMonth(bool changedFrom)
    {
        if (changingDates) return; changingDates = true;
        try
        {
            var fromMonth = new DateTime(from.Value.Year, from.Value.Month, 1); var toMonth = new DateTime(to.Value.Year, to.Value.Month, 1); if (fromMonth != toMonth) { var month = changedFrom ? fromMonth : toMonth; var end = month.AddMonths(1).AddDays(-1); if (month == new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)) end = DateTime.Today; from.Value = month; to.Value = end; }
        }
        finally { changingDates = false; }
    }

    void LoadEmployeeRows(List<string[]> rows)
    {
        currentRows = rows; instruction.Visible = false; grid.Visible = true; grid.Rows.Clear(); grid.Columns.Clear();
        var dates = rows
            .Where(r => !IsConfiguredNonWorkingDay(r))
            .Select(r => DateTime.ParseExact(r[2], "dd/MM/yyyy", null).Date)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
        grid.Columns.Add("Horario", "Horario"); foreach (var date in dates) grid.Columns.Add(date.ToString("yyyy-MM-dd"), date.ToString("dd"));
        var template = ParseSegmentDetails(rows.FirstOrDefault());
        var lunch = ParseLunchDetails(rows.FirstOrDefault());
        for (var index = 0; index < template.Count; index++)
        {
            var segment = template[index];
            var suffix = template.Count == 1 ? "" : $" {segment.Number}";
            AddSegmentMarkRow(rows, dates, $"Ingreso{suffix} · {segment.ExpectedEntry:hh\\:mm}", segment.Number, true);

            if (lunch is not null && lunch.Start >= segment.ExpectedEntry && lunch.End <= segment.ExpectedExit)
                AddLunchRow(rows, dates, $"Descanso · {lunch.Start:hh\\:mm}–{lunch.End:hh\\:mm}");

            AddSegmentMarkRow(rows, dates, $"Salida{suffix} · {segment.ExpectedExit:hh\\:mm}", segment.Number, false);

            if (index < template.Count - 1)
            {
                var next = template[index + 1];
                if (next.ExpectedEntry > segment.ExpectedExit)
                    AddRestRow(dates, $"Descanso · {segment.ExpectedExit:hh\\:mm}–{next.ExpectedEntry:hh\\:mm}");
            }
        }
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None; if (grid.Parent is Panel host) LayoutReportGrid(host); UpdateSummary();
    }

    void AddSegmentMarkRow(List<string[]> rows, List<DateTime> dates, string title, int segmentNumber, bool isEntry)
    {
        var values = new object[dates.Count + 1]; values[0] = title; for (var i = 0; i < dates.Count; i++) values[i + 1] = MarkForSegment(rows, dates[i], segmentNumber, isEntry); grid.Rows.Add(values);
    }

    void AddLunchRow(List<string[]> rows, List<DateTime> dates, string title)
    {
        var values = new object[dates.Count + 1]; values[0] = title;
        for (var i = 0; i < dates.Count; i++) values[i + 1] = MarkForLunch(rows, dates[i]);
        var rowIndex = grid.Rows.Add(values);
        StyleRestRow(rowIndex);
    }

    void AddRestRow(List<DateTime> dates, string title)
    {
        var values = new object[dates.Count + 1];
        values[0] = title;
        for (var i = 0; i < dates.Count; i++) values[i + 1] = "";
        var rowIndex = grid.Rows.Add(values);
        StyleRestRow(rowIndex);
    }

    void StyleRestRow(int rowIndex)
    {
        var row = grid.Rows[rowIndex];
        row.DefaultCellStyle.BackColor = Color.FromArgb(245, 247, 250);
        row.DefaultCellStyle.ForeColor = Muted;
        row.DefaultCellStyle.Font = new Font("Segoe UI", Math.Max(8, 10 * ResponsiveScale), FontStyle.Italic);
    }

    string MarkForSegment(List<string[]> rows, DateTime date, int segmentNumber, bool isEntry)
    {
        var row = rows.FirstOrDefault(r => DateTime.ParseExact(r[2], "dd/MM/yyyy", null).Date == date); if (row is null || IsBlankState(row)) return "";
        var segment = ParseSegmentDetails(row).FirstOrDefault(x => x.Number == segmentNumber); if (segment is null) return "";
        var actual = isEntry ? segment.ActualEntry : segment.ActualExit;
        if (!actual.HasValue)
        {
            if (!segment.Closed && !isEntry) return "";
            if (date.Date > DateTime.Today) return "";
            var expected = isEntry ? segment.ExpectedEntry.Add(TimeSpan.FromMinutes(toleranceMinutes)) : segment.ExpectedExit;
            if (date.Date == DateTime.Today && DateTime.Now < date.Add(expected)) return "";
            return "−";
        }
        if (isEntry) return actual.Value <= segment.ExpectedEntry.Add(TimeSpan.FromMinutes(toleranceMinutes)) ? "✓" : "X";
        return actual.Value >= segment.ExpectedExit ? "✓" : "X";
    }

    static string MarkForLunch(List<string[]> rows, DateTime date)
    {
        var row = rows.FirstOrDefault(r => DateTime.ParseExact(r[2], "dd/MM/yyyy", null).Date == date); if (row is null || IsBlankState(row)) return "";
        var lunch = ParseLunchDetails(row); if (lunch is null) return "";
        if (lunch.ActualExit.HasValue && lunch.ActualReturn.HasValue) return "✓";
        if (lunch.ActualExit.HasValue || lunch.ActualReturn.HasValue) return "X";
        return "";
    }

    static bool IsBlankState(string[] row)
    {
        if (row.Length <= 16) return false; var state = row[16]; return state.Equals("Antes del primer marcado", StringComparison.OrdinalIgnoreCase) || state.Equals("No laborable", StringComparison.OrdinalIgnoreCase) || state.StartsWith("Festivo", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsConfiguredNonWorkingDay(string[] row)
    {
        if (row.Length <= 16) return false;
        return row[16].Equals("No laborable", StringComparison.OrdinalIgnoreCase)
            || row[16].Equals("Trabajo fuera de jornada", StringComparison.OrdinalIgnoreCase);
    }

    static List<SegmentView> ParseSegmentDetails(string[]? row)
    {
        var result = new List<SegmentView>();
        if (row is not null && row.Length > 21)
        {
            foreach (var encoded in row[21].Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var values = encoded.Split(',');
                if (values.Length < 8 || !int.TryParse(values[0], out var number) || !TimeSpan.TryParse(values[1], out var expectedEntry) || !TimeSpan.TryParse(values[2], out var expectedExit)) continue;
                var actualEntry = OptionalTime(values[3]); var actualExit = OptionalTime(values[4]); int.TryParse(values[5], out var worked); int.TryParse(values[6], out var expected); var closed = values[7] == "1";
                var wm = values.Length > 8 && int.TryParse(values[8], out var vwm) ? vwm : 0;
                var wa = values.Length > 9 && int.TryParse(values[9], out var vwa) ? vwa : 0;
                var em = values.Length > 10 && int.TryParse(values[10], out var vem) ? vem : 0;
                var ea = values.Length > 11 && int.TryParse(values[11], out var vea) ? vea : 0;
                result.Add(new SegmentView(number, expectedEntry, expectedExit, actualEntry, actualExit, worked, expected, closed, wm, wa, em, ea));
            }
        }
        if (result.Count > 0 || row is null) return result;
        var ranges = (row.Length > 4 ? row[4] : "").Split(" / ", StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < ranges.Length; index++)
        {
            var range = ParseScheduleRange(ranges[index]); var entryIndex = index == 0 ? 5 : 7; var exitIndex = index == 0 ? 6 : 8; var actualEntry = row.Length > entryIndex ? OptionalTime(row[entryIndex]) : null; var actualExit = row.Length > exitIndex ? OptionalTime(row[exitIndex]) : null; result.Add(new SegmentView(index + 1, range.Entry, range.Exit, actualEntry, actualExit, 0, 0, !row[16].Equals("En curso", StringComparison.OrdinalIgnoreCase)));
        }
        return result;
    }

    static LunchView? ParseLunchDetails(string[]? row)
    {
        if (row is null || row.Length <= 22 || string.IsNullOrWhiteSpace(row[22])) return null; var values = row[22].Split(','); if (values.Length < 4 || !TimeSpan.TryParse(values[0], out var start) || !TimeSpan.TryParse(values[1], out var end)) return null; return new LunchView(start, end, OptionalTime(values[2]), OptionalTime(values[3]));
    }

    static TimeSpan? OptionalTime(string? value) => TimeSpan.TryParse(value, out var time) ? time : null;
    static (TimeSpan Entry, TimeSpan Exit) ParseScheduleRange(string? range) { var parts = (range ?? "").Split(new[] { '–', '-' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); return parts.Length >= 2 && TimeSpan.TryParse(parts[0], out var entry) && TimeSpan.TryParse(parts[1], out var exit) ? (entry, exit) : (TimeSpan.Zero, TimeSpan.Zero); }

    void LayoutReportGrid(Panel host)
    {
        if (grid.Columns.Count == 0) return; var available = Math.Max(300, host.ClientSize.Width - 12); var dayCount = Math.Max(1, grid.Columns.Count - 1); var scheduleWidth = Math.Clamp((int)(225 * ResponsiveScale), 185, 225); var dayWidth = Math.Clamp((available - scheduleWidth - 2) / dayCount, 24, 46); grid.Columns["Horario"].Width = scheduleWidth; for (var i = 1; i < grid.Columns.Count; i++) grid.Columns[i].Width = dayWidth; var totalWidth = scheduleWidth + (dayWidth * dayCount) + 2; var naturalHeight = grid.ColumnHeadersHeight + grid.Rows.Cast<DataGridViewRow>().Sum(row => row.Height) + 2; var maximumHeight = Math.Max(120, (int)(ClientSize.Height * 0.48)); grid.Dock = DockStyle.None; grid.Width = Math.Min(totalWidth, available); grid.Height = Math.Min(naturalHeight, maximumHeight); grid.ScrollBars = naturalHeight > maximumHeight ? ScrollBars.Both : ScrollBars.Horizontal; grid.Location = new Point(Math.Max(6, (host.ClientSize.Width - grid.Width) / 2), 6); host.Height = grid.Height + 12;
    }

    enum SummaryStyle { Normal, Total, Box, Strong, Deduction, Note }

    (int WorkAM, int WorkPM, int ExpAM, int ExpPM, int Justified, int JustifiedCount, int Unjustified, int UnjustifiedCount, int Extra, int TotalWork, int TotalExp, int Trabajadas, int FinalTotal, int Balance) SummaryTotals()
    {
        int Col(string[] r, int i) => ParseHours(r.Length > i ? r[i] : "0:00");
        var workAM = currentRows.Sum(r => Col(r, 23));
        var workPM = currentRows.Sum(r => Col(r, 24));
        var expAM = currentRows.Sum(r => Col(r, 25));
        var expPM = currentRows.Sum(r => Col(r, 26));
        var justified = currentRows.Sum(r => Col(r, 17));
        var justifiedCount = currentRows.Sum(r => ParseCount(r, 18));
        var unjustified = currentRows.Sum(r => Col(r, 19));
        var unjustifiedCount = currentRows.Sum(r => ParseCount(r, 20));
        var extra = currentRows.Sum(r => Col(r, 14));
        var totalWork = workAM + workPM;
        var totalExp = expAM + expPM;
        var exigibleExp = currentRows.Sum(r => Col(r, 10));
        var trabajadas = totalWork - unjustified;
        var finalTotal = trabajadas + extra;
        return (workAM, workPM, expAM, expPM, justified, justifiedCount, unjustified, unjustifiedCount, extra, totalWork, totalExp, trabajadas, finalTotal, exigibleExp - finalTotal);
    }

    void UpdateSummary()
    {
        summaryFlow.Controls.Clear();
        if (currentRows.Count == 0) return;
        var t = SummaryTotals();
        AddSummaryTitle("RESUMEN GENERAL");
        AddSummaryHeader();
        AddSummaryRow("Horas mañana", FormatHours(t.ExpAM), FormatHours(t.WorkAM), SummaryStyle.Normal);
        AddSummaryRow("Horas tarde", FormatHours(t.ExpPM), FormatHours(t.WorkPM), SummaryStyle.Normal);
        AddSummaryRow("TOTAL", FormatHours(t.TotalExp), FormatHours(t.TotalWork), SummaryStyle.Total);
        AddSummarySection("INCIDENCIAS DEL PERÍODO");
        AddIncidentRow("Ausencias justificadas", t.JustifiedCount, t.Justified, false);
        AddIncidentRow("Ausencias sin justificar", t.UnjustifiedCount, t.Unjustified, true);
        AddSummarySection("RESULTADO");
        AddSummaryRow("Total de horas trabajadas", "", FormatHours(t.Trabajadas), SummaryStyle.Box);
        AddSummaryRow("Fuera de horario", "", t.Extra > 0 ? $"+ {FormatHours(t.Extra)}" : "—", SummaryStyle.Normal);
        AddSummaryRow("TOTAL FINAL", "", FormatHours(t.FinalTotal), SummaryStyle.Strong);
        AddSummaryRow(t.Balance > 0 ? $"Balance: debe {FormatHours(t.Balance)}" : t.Balance < 0 ? $"Balance: a favor {FormatHours(-t.Balance)}" : "Balance: al día", "", "", SummaryStyle.Note);
    }

    void AddSummaryTitle(string text)
    {
        var scale = SummaryScale;
        var panel = new Panel { Width = summaryFlow.ClientSize.Width, Height = Math.Max(20, (int)(30 * scale)), BackColor = Color.White, Margin = new Padding(0) };
        panel.Controls.Add(new Label { Text = text, AutoSize = true, ForeColor = Accent, Font = new Font("Segoe UI", Math.Max(8.5f, 12 * scale), FontStyle.Bold), Location = new Point(Math.Max(8, (int)(14 * scale)), Math.Max(2, (int)(5 * scale))) });
        summaryFlow.Controls.Add(panel);
    }

    void AddSummaryHeader()
    {
        var scale = SummaryScale;
        var width = summaryFlow.ClientSize.Width;
        var colW = Math.Max(90, (int)(width * 0.22));
        var doneLeft = width - Math.Max(10, (int)(16 * scale)) - colW;
        var shouldLeft = doneLeft - colW - 8;
        var panel = new Panel { Width = width, Height = Math.Max(16, (int)(22 * scale)), BackColor = Color.White, Margin = new Padding(0) };
        var font = new Font("Segoe UI", Math.Max(7, 9 * scale), FontStyle.Bold);
        panel.Controls.Add(new Label { Text = "Debería marcar", AutoSize = false, Width = colW, Height = panel.Height, TextAlign = ContentAlignment.MiddleRight, ForeColor = Muted, Font = font, Location = new Point(shouldLeft, 0) });
        panel.Controls.Add(new Label { Text = "Marcado", AutoSize = false, Width = colW, Height = panel.Height, TextAlign = ContentAlignment.MiddleRight, ForeColor = Muted, Font = font, Location = new Point(doneLeft, 0) });
        panel.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.FromArgb(200, 206, 214) });
        summaryFlow.Controls.Add(panel);
    }

    void AddSummarySection(string text)
    {
        var scale = SummaryScale;
        var width = summaryFlow.ClientSize.Width;
        var height = Math.Max(20, (int)(30 * scale));
        var panel = new Panel { Width = width, Height = height, BackColor = Color.White, Margin = new Padding(0, 1, 0, 0) };
        panel.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Color.FromArgb(170, 179, 190) });
        panel.Controls.Add(new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = Muted,
            Font = new Font("Segoe UI", Math.Max(7, 9 * scale), FontStyle.Bold),
            Location = new Point(Math.Max(8, (int)(14 * scale)), Math.Max(5, (int)(8 * scale)))
        });
        summaryFlow.Controls.Add(panel);
    }

    void AddIncidentRow(string title, int count, int minutes, bool deduction)
    {
        var scale = SummaryScale;
        var width = summaryFlow.ClientSize.Width;
        var pad = Math.Max(10, (int)(16 * scale));
        var height = Math.Max(22, (int)(31 * scale));
        var row = new Panel { Width = width, Height = height, BackColor = deduction ? Color.FromArgb(253, 247, 247) : Color.FromArgb(247, 250, 248), Margin = new Padding(0, 0, 0, 1) };
        row.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 4, BackColor = deduction ? Danger : Ok });
        row.Controls.Add(new Label
        {
            Text = title,
            AutoSize = false,
            Width = Math.Max(180, width / 2),
            Height = height,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Ink,
            Font = new Font("Segoe UI", Math.Max(7.5f, 10 * scale), FontStyle.Bold),
            Location = new Point(pad + 8, 0)
        });
        var detail = minutes > 0
            ? $"{Times(count)}  ·  {(deduction ? "− " : "")}{FormatHours(minutes)}{(deduction ? "" : "")}"
            : $"{Times(count)}  ·  —";
        row.Controls.Add(new Label
        {
            Text = detail,
            AutoSize = false,
            Width = Math.Max(220, width / 2 - pad),
            Height = height,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = deduction ? Danger : Ok,
            Font = new Font("Segoe UI", Math.Max(7.5f, 10 * scale)),
            Location = new Point(width / 2, 0)
        });
        summaryFlow.Controls.Add(row);
    }

    void AddSummaryRow(string title, string should, string done, SummaryStyle style)
    {
        var scale = SummaryScale;
        var width = summaryFlow.ClientSize.Width;
        var strong = style == SummaryStyle.Strong;
        var height = Math.Max(strong ? 25 : 20, (int)((strong ? 36 : 29) * scale));
        var fontSize = Math.Max(strong ? 9.5f : 8, (strong ? 13 : 10.5f) * scale);
        var bold = style is SummaryStyle.Total or SummaryStyle.Strong or SummaryStyle.Box;
        var back = style switch
        {
            SummaryStyle.Total => Color.FromArgb(232, 238, 245),
            SummaryStyle.Box => Color.FromArgb(240, 244, 248),
            SummaryStyle.Strong => Accent,
            _ => Color.White
        };
        var fore = strong ? Color.White : Ink;
        var colW = Math.Max(90, (int)(width * 0.22));
        var pad = Math.Max(10, (int)(16 * scale));
        var doneLeft = width - pad - colW;
        var shouldLeft = doneLeft - colW - 8;
        var row = new Panel { Width = width, Height = height, BackColor = back, Margin = new Padding(0, style == SummaryStyle.Note ? 2 : 0, 0, 0) };
        if (style == SummaryStyle.Box) row.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 4, BackColor = Accent });
        var titleFont = new Font("Segoe UI", fontSize, bold || style == SummaryStyle.Note ? FontStyle.Bold : FontStyle.Regular);
        row.Controls.Add(new Label { Text = title, AutoSize = false, Width = shouldLeft - pad - 4, Height = height, TextAlign = ContentAlignment.MiddleLeft, ForeColor = style == SummaryStyle.Note ? Accent : fore, Font = titleFont, Location = new Point(pad + (style == SummaryStyle.Box ? 8 : 0), 0) });
        var valueFont = new Font("Segoe UI", fontSize, bold ? FontStyle.Bold : FontStyle.Regular);
        if (should.Length > 0)
            row.Controls.Add(new Label { Text = should, AutoSize = false, Width = colW, Height = height, TextAlign = ContentAlignment.MiddleRight, ForeColor = fore, Font = valueFont, Location = new Point(shouldLeft, 0) });
        if (done.Length > 0)
            row.Controls.Add(new Label { Text = done, AutoSize = false, Width = colW, Height = height, TextAlign = ContentAlignment.MiddleRight, ForeColor = style == SummaryStyle.Deduction ? Danger : fore, Font = valueFont, Location = new Point(doneLeft, 0) });
        summaryFlow.Controls.Add(row);
    }

    static int ParseHours(string? value) { var parts = (value ?? "").Split(':'); return parts.Length == 2 && int.TryParse(parts[0], out var h) && int.TryParse(parts[1], out var m) ? h * 60 + m : 0; }
    static int ParseCount(string[] row, int index) => row.Length > index && int.TryParse(row[index], out var count) ? count : 0;
    static string Times(int count) => count == 1 ? "1 vez" : $"{count} veces";
    static string FormatHours(int minutes) { var absolute = Math.Abs((long)minutes); var sign = minutes < 0 ? "− " : ""; return $"{sign}{absolute / 60}h {absolute % 60:00}m"; }

    void Download() { if (!grid.Visible || grid.Rows.Count == 0) return; using var dialog = new SaveFileDialog { FileName = $"reporte_{DateTime.Now:yyyyMMdd_HHmmss}.csv", Filter = "Archivo CSV|*.csv", InitialDirectory = document.DownloadFolder }; if (dialog.ShowDialog(this) == DialogResult.OK) File.WriteAllText(dialog.FileName, GridCsv(), new UnicodeEncoding(false, true)); }

    void DownloadSummary()
    {
        if (currentRows.Count == 0) { MessageBox.Show(this, "Busque primero un trabajador.", "Descargar resumen", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        using var dialog = new SaveFileDialog { FileName = $"resumen_{DateTime.Now:yyyyMMdd_HHmmss}.csv", Filter = "Archivo CSV|*.csv", InitialDirectory = document.DownloadFolder };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var t = SummaryTotals();
        var b = new StringBuilder("sep=;\r\n");
        var employeeParts = selectedEmployee.Text.Split('·', StringSplitOptions.TrimEntries);
        var employeeId = employeeParts.Length > 0 ? employeeParts[0] : "";
        var employeeName = employeeParts.Length > 1 ? employeeParts[1] : selectedEmployee.Text;
        b.AppendLine(string.Join(';', Q("ID empleado"), Q("Nombre"), Q("Concepto"), Q("Debería marcar"), Q("Marcado")));
        void L(string c, string s, string d) => b.AppendLine(string.Join(';', Q(employeeId), Q(employeeName), Q(c), Q(s), Q(d)));
        L("Horas mañana", FormatHours(t.ExpAM), FormatHours(t.WorkAM));
        L("Horas tarde", FormatHours(t.ExpPM), FormatHours(t.WorkPM));
        L("Total", FormatHours(t.TotalExp), FormatHours(t.TotalWork));
        L("Ausencias justificadas", Times(t.JustifiedCount), t.Justified > 0 ? FormatHours(t.Justified) + " (registro)" : "—");
        L("Ausencias sin justificar", Times(t.UnjustifiedCount), t.Unjustified > 0 ? "- " + FormatHours(t.Unjustified) : "—");
        L("Total de horas trabajadas", "", FormatHours(t.Trabajadas));
        L("Fuera de horario", "", t.Extra > 0 ? "+ " + FormatHours(t.Extra) : "—");
        L("Total final", "", FormatHours(t.FinalTotal));
        L("Balance", "", t.Balance > 0 ? "Debe " + FormatHours(t.Balance) : t.Balance < 0 ? "A favor " + FormatHours(-t.Balance) : "Al día");
        File.WriteAllText(dialog.FileName, b.ToString(), new UnicodeEncoding(false, true));
    }
    void DownloadPdf()
    {
        if (!grid.Visible || grid.Rows.Count == 0) return;
        using var dialog = new SaveFileDialog { FileName = $"reporte_{DateTime.Now:yyyyMMdd_HHmmss}.pdf", Filter = "Documento PDF|*.pdf", InitialDirectory = document.DownloadFolder };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var printer = new PrinterSettings { PrinterName = "Microsoft Print to PDF" }; if (!printer.IsValid) throw new InvalidOperationException("No se encontró Microsoft Print to PDF en este equipo."); printer.PrintToFile = true; printer.PrintFileName = dialog.FileName;
            printDocument.PrinterSettings = printer; printDocument.PrintController = new StandardPrintController(); printRow = 0; printDocument.Print(); MessageBox.Show(this, "PDF guardado correctamente.", "Descarga completa", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "No se pudo crear el PDF", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    string GridCsv() { var columns = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Name != "Editar").ToList(); var b = new StringBuilder("sep=;\r\n"); b.AppendLine(string.Join(';', columns.Select(c => Q(c.HeaderText)))); foreach (DataGridViewRow row in grid.Rows) b.AppendLine(string.Join(';', columns.Select(c => Q(Convert.ToString(row.Cells[c.Index].Value) ?? "")))); return b.ToString(); }
    void Preview() { if (!grid.Visible || grid.Rows.Count == 0) return; printRow = 0; using var preview = new PrintPreviewDialog { Document = printDocument, Width = 1100, Height = 760, UseAntiAlias = true }; preview.ShowDialog(this); }
    void Print() { if (!grid.Visible || grid.Rows.Count == 0) return; using var dialog = new PrintDialog { Document = printDocument, UseEXDialog = true }; if (dialog.ShowDialog(this) == DialogResult.OK) { printRow = 0; printDocument.Print(); } }
    void PrintPage(object? sender, PrintPageEventArgs e)
    {
        var g = e.Graphics!;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        using var bodyFont = new Font("Segoe UI", 8.5f);
        using var bodyBold = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        using var smallFont = new Font("Segoe UI", 7f);
        using var smallBold = new Font("Segoe UI", 7f, FontStyle.Bold);
        using var dayFont = new Font("Segoe UI", 6.5f, FontStyle.Bold);
        using var titleFont = new Font("Segoe UI", 18f, FontStyle.Bold);
        using var subtitleFont = new Font("Segoe UI", 8f, FontStyle.Bold);
        using var totalFont = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
        using var left = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter };
        using var right = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
        using var borderPen = new Pen(Color.FromArgb(125, 132, 140), 0.8f);
        using var dividerPen = new Pen(Color.FromArgb(80, 86, 94), 1.1f);
        using var headerBrush = new SolidBrush(Color.FromArgb(232, 235, 239));
        using var alternateBrush = new SolidBrush(Color.FromArgb(247, 248, 249));
        using var sectionBrush = new SolidBrush(Color.FromArgb(239, 241, 244));
        using var totalBrush = new SolidBrush(Color.FromArgb(217, 222, 228));
        using var darkBrush = new SolidBrush(Color.FromArgb(42, 46, 51));
        using var mutedBrush = new SolidBrush(Color.FromArgb(85, 91, 99));

        var columns = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible && c.Name != "Editar").ToList();
        var bounds = e.MarginBounds;
        var y = bounds.Top;

        var reportHeader = new Rectangle(bounds.Left, y, bounds.Width, 62);
        g.FillRectangle(sectionBrush, reportHeader);
        g.FillRectangle(Brushes.Black, bounds.Left, y, 6, reportHeader.Height);
        g.DrawString("REPORTE DE ASISTENCIA", titleFont, darkBrush, bounds.Left + 18, y + 7);
        g.DrawString("CONTROL DE JORNADA LABORAL", smallBold, mutedBrush, bounds.Left + 20, y + 38);
        var metadataX = bounds.Left + (int)(bounds.Width * 0.55f);
        g.DrawString($"Empleado   {selectedEmployee.Text}", subtitleFont, darkBrush, new RectangleF(metadataX, y + 10, bounds.Right - metadataX - 12, 18), left);
        g.DrawString($"Periodo     {from.Value:dd/MM/yyyy}  —  {to.Value:dd/MM/yyyy}", bodyFont, darkBrush, new RectangleF(metadataX, y + 31, bounds.Right - metadataX - 12, 18), left);
        y += reportHeader.Height + 14;

        var firstWidth = columns.Count > 1 ? Math.Clamp((int)(bounds.Width * 0.17f), 120, 155) : bounds.Width;
        var dayWidth = columns.Count > 1 ? (float)(bounds.Width - firstWidth) / (columns.Count - 1) : 0f;
        const int headerHeight = 25;
        const int rowHeight = 23;

        void DrawTableCell(RectangleF cell, string text, Font font, Brush textBrush, Brush? background, StringFormat format)
        {
            if (background is not null) g.FillRectangle(background, cell);
            g.DrawRectangle(borderPen, cell.X, cell.Y, cell.Width, cell.Height);
            var textArea = RectangleF.Inflate(cell, -4, -1);
            g.DrawString(text, font, textBrush, textArea, format);
        }

        var x = (float)bounds.Left;
        for (var i = 0; i < columns.Count; i++)
        {
            var width = i == 0 ? firstWidth : dayWidth;
            DrawTableCell(new RectangleF(x, y, width, headerHeight), columns[i].HeaderText, i == 0 ? bodyBold : dayFont, darkBrush, headerBrush, centered);
            x += width;
        }
        y += headerHeight;

        while (printRow < grid.Rows.Count)
        {
            if (y + rowHeight > bounds.Bottom - 290)
            {
                e.HasMorePages = true;
                return;
            }

            x = bounds.Left;
            var rowBackground = printRow % 2 == 0 ? Brushes.White : alternateBrush;
            for (var i = 0; i < columns.Count; i++)
            {
                var width = i == 0 ? firstWidth : dayWidth;
                var text = grid.Rows[printRow].Cells[columns[i].Index].Value?.ToString() ?? "";
                DrawTableCell(new RectangleF(x, y, width, rowHeight), text, i == 0 ? bodyFont : bodyBold, darkBrush, rowBackground, i == 0 ? left : centered);
                x += width;
            }
            y += rowHeight;
            printRow++;
        }

        if (y + 275 > bounds.Bottom)
        {
            e.HasMorePages = true;
            return;
        }

        y += 16;
        var summaryWidth = Math.Min(720, bounds.Width);
        var summaryX = bounds.Left + (bounds.Width - summaryWidth) / 2;
        var labelWidth = (int)(summaryWidth * 0.54f);
        var valueWidth = (summaryWidth - labelWidth) / 2;
        var t = SummaryTotals();

        g.FillRectangle(darkBrush, summaryX, y, summaryWidth, 28);
        g.DrawString("RESUMEN GENERAL", bodyBold, Brushes.White, new RectangleF(summaryX + 12, y, summaryWidth - 24, 28), left);
        y += 28;

        void SummaryCell(float cellX, int cellWidth, string value, Font font, Brush textBrush, Brush? background, StringFormat format, int height)
        {
            var rectangle = new RectangleF(cellX, y, cellWidth, height);
            if (background is not null) g.FillRectangle(background, rectangle);
            g.DrawString(value, font, textBrush, RectangleF.Inflate(rectangle, -9, -1), format);
        }

        void SummaryRow(string label, string should, string done, bool strong = false, Brush? background = null, Brush? valueBrush = null, int height = 22)
        {
            var font = strong ? bodyBold : bodyFont;
            SummaryCell(summaryX, labelWidth, label, font, darkBrush, background, left, height);
            SummaryCell(summaryX + labelWidth, valueWidth, should, font, valueBrush ?? darkBrush, background, right, height);
            SummaryCell(summaryX + labelWidth + valueWidth, valueWidth, done, font, valueBrush ?? darkBrush, background, right, height);
            y += height;
        }

        SummaryCell(summaryX, labelWidth, "CONCEPTO", smallBold, mutedBrush, sectionBrush, left, 21);
        SummaryCell(summaryX + labelWidth, valueWidth, "DEBERÍA MARCAR", smallBold, mutedBrush, sectionBrush, right, 21);
        SummaryCell(summaryX + labelWidth + valueWidth, valueWidth, "MARCADO", smallBold, mutedBrush, sectionBrush, right, 21);
        y += 21;
        SummaryRow("Horas de la mañana", FormatHours(t.ExpAM), FormatHours(t.WorkAM));
        SummaryRow("Horas de la tarde", FormatHours(t.ExpPM), FormatHours(t.WorkPM));
        g.DrawLine(dividerPen, summaryX, y, summaryX + summaryWidth, y);
        SummaryRow("TOTAL", FormatHours(t.TotalExp), FormatHours(t.TotalWork), true, totalBrush);

        y += 7;
        SummaryCell(summaryX, summaryWidth, "INCIDENCIAS DEL PERÍODO", smallBold, mutedBrush, sectionBrush, left, 20);
        y += 20;
        SummaryRow("Ausencias justificadas", "", $"{Times(t.JustifiedCount)}  ·  {(t.Justified > 0 ? FormatHours(t.Justified) + " (registro)" : "—")}");
        SummaryRow("Ausencias sin justificar", "", $"{Times(t.UnjustifiedCount)}  ·  {(t.Unjustified > 0 ? "− " + FormatHours(t.Unjustified) : "—")}", true);

        y += 7;
        SummaryCell(summaryX, summaryWidth, "RESULTADO", smallBold, mutedBrush, sectionBrush, left, 20);
        y += 20;
        SummaryRow("Total de horas trabajadas", "", FormatHours(t.Trabajadas), true);
        SummaryRow("Fuera de horario", "", t.Extra > 0 ? "+ " + FormatHours(t.Extra) : "—");
        g.DrawLine(dividerPen, summaryX, y, summaryX + summaryWidth, y);
        SummaryRow("TOTAL FINAL", "", FormatHours(t.FinalTotal), true, darkBrush, Brushes.White, 27);
        var balanceText = t.Balance > 0 ? "DEBE  " + FormatHours(t.Balance) : t.Balance < 0 ? "A FAVOR  " + FormatHours(-t.Balance) : "AL DÍA";
        SummaryRow("BALANCE DEL PERÍODO", "", balanceText, true, sectionBrush, darkBrush, 25);
        g.DrawRectangle(borderPen, summaryX, y - 25, summaryWidth, 25);
        e.HasMorePages = false;
    }
    static List<string[]> ParseRows(string csv) { var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries); var start = lines.Length > 0 && lines[0].StartsWith("sep=") ? 2 : 1; return lines.Skip(start).Select(line => Parse(line, ';')).Where(x => x.Count >= 17).Select(x => x.ToArray()).ToList(); }
    static List<string> Parse(string line, char delimiter) { var result = new List<string>(); var value = new StringBuilder(); var quoted = false; foreach (var c in line) { if (c == '"') quoted = !quoted; else if (c == delimiter && !quoted) { result.Add(value.ToString()); value.Clear(); } else value.Append(c); } result.Add(value.ToString()); return result; }
    sealed record SegmentView(int Number, TimeSpan ExpectedEntry, TimeSpan ExpectedExit, TimeSpan? ActualEntry, TimeSpan? ActualExit, int WorkedMinutes, int ExpectedMinutes, bool Closed, int WorkedMorning = 0, int WorkedAfternoon = 0, int ExpectedMorning = 0, int ExpectedAfternoon = 0);
    sealed record LunchView(TimeSpan Start, TimeSpan End, TimeSpan? ActualExit, TimeSpan? ActualReturn);
    static DataGridView Grid() => new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToResizeColumns = false, AllowUserToResizeRows = false, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, BackgroundColor = Color.White, BorderStyle = BorderStyle.FixedSingle, GridColor = Color.FromArgb(215, 221, 229), CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, RowHeadersVisible = false, EnableHeadersVisualStyles = false, ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(232, 238, 245), ForeColor = Color.Black, Font = new Font("Segoe UI", 10, FontStyle.Bold), Alignment = DataGridViewContentAlignment.MiddleCenter, Padding = new Padding(2) }, DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.Black, BackColor = Color.White, SelectionForeColor = Color.Black, SelectionBackColor = Color.FromArgb(220, 230, 240), Alignment = DataGridViewContentAlignment.MiddleCenter, Padding = new Padding(2) }, AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(248, 250, 252), ForeColor = Color.Black, Alignment = DataGridViewContentAlignment.MiddleCenter } };
    static Button Button(string text, Color color) => new() { Text = text, AutoSize = true, Height = 34, Font = new Font("Segoe UI", 9), BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0 }, Padding = new Padding(12, 0, 12, 0), Margin = new Padding(0, 0, 8, 0) };
    static string Q(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
}
