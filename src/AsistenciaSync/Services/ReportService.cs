using System.Globalization;
using System.Text;

using AsistenciaSync.Configuration;
using AsistenciaSync.Models;

namespace AsistenciaSync.Services;

public sealed record ReportDocument(string DetailCsv, string SummaryCsv, string DownloadFolder, DateTime GeneratedAt, DateTime From, DateTime To, int DetailRows);

public static class ReportService
{
    public static ReportDocument Build(AppSettings settings)
    {
        var from = settings.ReportFrom.Date; var to = settings.ReportTo.Date;
        if (from > to) throw new InvalidOperationException("La fecha inicial no puede ser posterior a la fecha final.");
        var employees = CsvStore.ReadEmployees(settings); var schedules = CsvStore.ReadSchedules(settings); var incidents = CsvStore.ReadIncidents(settings, from, to); var punches = CsvStore.ReadPunches(settings, from, to); var holidays = CsvStore.ReadHolidays(settings); var details = new List<DailyRow>();
        for (var day = from; day <= to; day = day.AddDays(1)) foreach (var employee in employees)
        {
            var schedule = schedules.TryGetValue(employee.Key, out var configured) ? configured : new EmployeeSchedule(employee.Key, true, true, true, true, true, false, false, false, settings.EntryTime, settings.ExitTime, TimeSpan.Zero, TimeSpan.Zero);
            var incident = incidents.FirstOrDefault(i => i.EmployeeId.Equals(employee.Key, StringComparison.OrdinalIgnoreCase) && i.Date.Date == day); var rawMarks = punches.Where(p => p.EmployeeId.Equals(employee.Key, StringComparison.OrdinalIgnoreCase) && p.Timestamp.Date == day).OrderBy(p => p.Timestamp).ToList(); var marks = ValidMarks(rawMarks, schedule.Discontinuous ? 4 : 2);
            var expectedIn = day.Add(schedule.Entry); var expectedOut = day.Add(schedule.Exit); var expectedSecondIn = day.Add(schedule.SecondEntry); var expectedSecondOut = day.Add(schedule.SecondExit); if (expectedOut <= expectedIn) expectedOut = expectedOut.AddDays(1); if (schedule.Discontinuous && expectedSecondOut <= expectedSecondIn) expectedSecondOut = expectedSecondOut.AddDays(1);
            var holiday = holidays.FirstOrDefault(x => x.Date == day.Date && (x.EmployeeId is null || x.EmployeeId.Equals(employee.Key, StringComparison.OrdinalIgnoreCase))); var workingDay = schedule.WorksOn(day.DayOfWeek) && holiday is null; DateTime? entry; DateTime? exit; DateTime? secondEntry; DateTime? secondExit; int late; int early; int worked; int expected;
            if (schedule.Discontinuous)
            {
                entry = marks.ElementAtOrDefault(0)?.Timestamp; exit = marks.ElementAtOrDefault(1)?.Timestamp; secondEntry = marks.ElementAtOrDefault(2)?.Timestamp; secondExit = marks.ElementAtOrDefault(3)?.Timestamp; late = entry is null || incident?.JustifiesLateness == true ? 0 : Math.Max(0, (int)(entry.Value - expectedIn).TotalMinutes) + (secondEntry is null ? 0 : Math.Max(0, (int)(secondEntry.Value - expectedSecondIn).TotalMinutes)); early = marks.Count < 2 ? 0 : Math.Max(0, (int)(expectedOut - marks[1].Timestamp).TotalMinutes) + (marks.Count < 4 ? 0 : Math.Max(0, (int)(expectedSecondOut - marks[3].Timestamp).TotalMinutes)); worked = marks.Count >= 4 ? Math.Max(0, (int)(marks[1].Timestamp - marks[0].Timestamp).TotalMinutes) + Math.Max(0, (int)(marks[3].Timestamp - marks[2].Timestamp).TotalMinutes) : 0; expected = workingDay ? Math.Max(0, (int)(expectedOut - expectedIn).TotalMinutes) + Math.Max(0, (int)(expectedSecondOut - expectedSecondIn).TotalMinutes) : 0;
            }
            else
            {
                entry = marks.ElementAtOrDefault(0)?.Timestamp; exit = marks.ElementAtOrDefault(1)?.Timestamp; secondEntry = null; secondExit = null; late = entry is null || incident?.JustifiesLateness == true ? 0 : Math.Max(0, (int)(entry.Value - expectedIn).TotalMinutes); early = exit is null ? 0 : Math.Max(0, (int)(expectedOut - exit.Value).TotalMinutes); worked = entry is null || exit is null ? 0 : Math.Max(0, (int)(exit.Value - entry.Value).TotalMinutes); expected = workingDay ? (int)(expectedOut - expectedIn).TotalMinutes : 0;
            }
            var state = holiday is not null ? $"Festivo ({holiday.Description})" : !workingDay ? "No laborable" : entry is null && incident?.JustifiesAbsence == true ? $"Ausencia justificada ({incident.Type})" : entry is null ? "Ausente" : (schedule.Discontinuous && marks.Count < 4) || (!schedule.Discontinuous && exit is null) ? "Incompleto" : late > 0 && early > 0 ? "Tarde y salida anticipada" : late > 0 ? "Tarde" : early > 0 ? "Salida anticipada" : incident is not null ? $"Completo ({incident.Type})" : "Completo"; var scheduleType = schedule.Discontinuous ? "Discontinua" : "Continua"; var scheduleText = schedule.Discontinuous ? $"{schedule.Entry:hh\\:mm}–{schedule.Exit:hh\\:mm} / {schedule.SecondEntry:hh\\:mm}–{schedule.SecondExit:hh\\:mm}" : $"{schedule.Entry:hh\\:mm}–{schedule.Exit:hh\\:mm}"; details.Add(new DailyRow(employee.Key, employee.Value, day, entry, exit, secondEntry, secondExit, worked, expected, late, early, Math.Max(0, expected - worked), Math.Max(0, worked - expected), state, scheduleType, scheduleText, state.StartsWith("Completo", StringComparison.OrdinalIgnoreCase)));
        }
        return new ReportDocument(WriteDetails(details), WriteSummary(details), Path.Combine(CsvStore.Folder(settings), "reportes"), DateTime.Now, from, to, details.Count);
    }

    static List<CsvPunch> ValidMarks(List<CsvPunch> rawMarks, int maximum)
    {
        var valid = new List<CsvPunch>();
        foreach (var mark in rawMarks)
        {
            if (valid.Count == 0 || (mark.Timestamp - valid[^1].Timestamp).TotalMinutes >= 5) valid.Add(mark);
            if (valid.Count == maximum) break;
        }
        return valid;
    }

    static string WriteDetails(List<DailyRow> rows)
    {
        using var sw = new StringWriter(CultureInfo.InvariantCulture); sw.WriteLine("sep=;"); sw.WriteLine("ID empleado;Nombre completo;Fecha;Tipo jornada;Horario;Ingreso 1;Salida 1;Ingreso 2;Salida 2;Horas trabajadas;Horas esperadas;Tiempo de tardanza;Salida anticipada;Tiempo faltante;Tiempo extra;Cumplió;Estado"); foreach (var r in rows) sw.WriteLine(Line(r.EmpleadoId, r.Nombre, r.Fecha.ToString("dd/MM/yyyy"), r.TipoJornada, r.Horario, Time(r.Entrada), Time(r.Salida), Time(r.SecondEntry), Time(r.SecondExit), Hours(r.Trabajadas), Hours(r.Esperadas), Hours(r.MinutosTarde), Hours(r.MinutosSalidaAnticipada), Hours(r.MinutosFaltantes), Hours(r.MinutosExtra), r.Cumplio ? "✓" : "−", r.Estado)); return sw.ToString();
    }

    static string WriteSummary(List<DailyRow> rows)
    {
        using var sw = new StringWriter(CultureInfo.InvariantCulture); sw.WriteLine("sep=;"); sw.WriteLine("ID empleado;Nombre completo;Días del periodo;Días trabajados;Días ausente;Días incompletos;Horas trabajadas;Horas esperadas;Tiempo de tardanza;Salida anticipada;Tiempo faltante;Tiempo extra"); foreach (var g in rows.GroupBy(r => new { r.EmpleadoId, r.Nombre })) { var workedDays = g.Count(r => r.Entrada.HasValue && r.Salida.HasValue && r.Estado != "Incompleto"); var absent = g.Count(r => r.Estado == "Ausente"); var incomplete = g.Count(r => r.Estado == "Incompleto"); sw.WriteLine(Line(g.Key.EmpleadoId, g.Key.Nombre, g.Count(), workedDays, absent, incomplete, Hours(g.Sum(r => r.Trabajadas)), Hours(g.Sum(r => r.Esperadas)), Hours(g.Sum(r => r.MinutosTarde)), Hours(g.Sum(r => r.MinutosSalidaAnticipada)), Hours(g.Sum(r => r.MinutosFaltantes)), Hours(g.Sum(r => r.MinutosExtra)))); } return sw.ToString();
    }

    static string Line(params object?[] values) => string.Join(';', values.Select(x => Q(Convert.ToString(x, CultureInfo.InvariantCulture)))); static string Q(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\""; static string Time(DateTime? value) => value?.ToString("HH:mm:ss") ?? ""; static string Hours(int minutes) => $"{minutes / 60}:{minutes % 60:00}";
    sealed record DailyRow(string EmpleadoId, string Nombre, DateTime Fecha, DateTime? Entrada, DateTime? Salida, DateTime? SecondEntry, DateTime? SecondExit, int Trabajadas, int Esperadas, int MinutosTarde, int MinutosSalidaAnticipada, int MinutosFaltantes, int MinutosExtra, string Estado, string TipoJornada, string Horario, bool Cumplio);
}
