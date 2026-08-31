using System.Globalization;
using AsistenciaSync.Models;

namespace AsistenciaSync.Services;

public sealed record ReportDocument(string DetailCsv, string SummaryCsv, string DownloadFolder, DateTime GeneratedAt, DateTime From, DateTime To, int DetailRows);

internal sealed record SegmentAttendance(
    int Number, TimeSpan ExpectedEntry, TimeSpan ExpectedExit, DateTime? ActualEntry, DateTime? ActualExit,
    int WorkedMinutes, int ExpectedMinutes, bool Closed,
    int WorkedMorning, int WorkedAfternoon, int ExpectedMorning, int ExpectedAfternoon,
    int JustifiedMinutes = 0, int UnjustifiedMinutes = 0);

internal sealed record LunchAttendance(TimeSpan Start, TimeSpan End, DateTime? ActualExit, DateTime? ActualReturn);

internal sealed record DailyRow(
    string EmpleadoId, string Nombre, DateTime Fecha, DateTime? Entrada, DateTime? Salida, DateTime? SecondEntry, DateTime? SecondExit,
    int Trabajadas, int Esperadas, int MinutosTarde, int MinutosSalidaAnticipada, int MinutosFaltantes, int MinutosExtra,
    string Estado, string TipoJornada, string Horario, bool Cumplio, int AusenciaJustificada, int AusenciaSinJustificar,
    IReadOnlyList<SegmentAttendance> Segments, LunchAttendance? Lunch,
    int TrabajadasManana, int TrabajadasTarde, int EsperadasManana, int EsperadasTarde, string? Descanso);

public static partial class ReportService
{
    static string WriteDetails(List<DailyRow> rows)
    {
        using var sw = new StringWriter(CultureInfo.InvariantCulture);
        sw.WriteLine("sep=;");
        sw.WriteLine("ID empleado;Nombre completo;Fecha;Tipo jornada;Horario;Ingreso 1;Salida 1;Ingreso 2;Salida 2;Horas trabajadas;Horas esperadas;Tiempo de tardanza;Salida anticipada;Tiempo faltante;Tiempo extra;Cumplió;Estado;Ausencias con justificación;Veces con justificación;Ausencias sin justificación;Veces sin justificación;Detalle tramos;Detalle almuerzo;Horas mañana;Horas tarde;Esperado mañana;Esperado tarde;Descanso");
        foreach (var r in rows)
            sw.WriteLine(Line(
                r.EmpleadoId, r.Nombre, r.Fecha.ToString("dd/MM/yyyy"), r.TipoJornada, r.Horario,
                Time(r.Entrada), Time(r.Salida), Time(r.SecondEntry), Time(r.SecondExit),
                Hours(r.Trabajadas), Hours(r.Esperadas), Hours(r.MinutosTarde), Hours(r.MinutosSalidaAnticipada), Hours(r.MinutosFaltantes), Hours(r.MinutosExtra),
                r.Cumplio ? "✓" : "−", r.Estado,
                Hours(r.AusenciaJustificada), r.AusenciaJustificada > 0 ? 1 : 0, Hours(r.AusenciaSinJustificar), r.AusenciaSinJustificar > 0 ? 1 : 0,
                SegmentDetails(r.Segments), LunchDetails(r.Lunch),
                Hours(r.TrabajadasManana), Hours(r.TrabajadasTarde), Hours(r.EsperadasManana), Hours(r.EsperadasTarde), r.Descanso ?? ""));
        return sw.ToString();
    }

    static string WriteSummary(List<DailyRow> rows)
    {
        using var sw = new StringWriter(CultureInfo.InvariantCulture);
        sw.WriteLine("sep=;");
        sw.WriteLine("ID empleado;Nombre completo;Concepto;Debería marcar;Marcado");
        foreach (var g in rows.GroupBy(r => new { r.EmpleadoId, r.Nombre }))
        {
            var countable = g.Where(r => !r.Estado.Equals("Antes del primer marcado", StringComparison.OrdinalIgnoreCase)).ToList();
            var workAm = countable.Sum(r => r.TrabajadasManana);
            var workPm = countable.Sum(r => r.TrabajadasTarde);
            var expAm = countable.Sum(r => r.EsperadasManana);
            var expPm = countable.Sum(r => r.EsperadasTarde);
            var justifiedTimes = countable.Count(r => r.AusenciaJustificada > 0);
            var justifiedMinutes = countable.Sum(r => r.AusenciaJustificada);
            var unjustifiedTimes = countable.Count(r => r.AusenciaSinJustificar > 0);
            var unjustifiedMinutes = countable.Sum(r => r.AusenciaSinJustificar);
            var extra = countable.Sum(r => r.MinutosExtra);
            var totalWork = workAm + workPm;
            var totalExp = expAm + expPm;
            var exigibleExp = countable.Sum(r => r.Esperadas);
            var trabajadas = totalWork - unjustifiedMinutes;
            var finalTotal = trabajadas + extra;
            var balance = exigibleExp - finalTotal;

            void Row(string concept, string should, string done) => sw.WriteLine(Line(g.Key.EmpleadoId, g.Key.Nombre, concept, should, done));
            Row("Horas mañana", Hours(expAm), Hours(workAm));
            Row("Horas tarde", Hours(expPm), Hours(workPm));
            Row("Total", Hours(totalExp), Hours(totalWork));
            Row("Ausencias con justificación", Times(justifiedTimes), Hours(justifiedMinutes) + " (registro)");
            Row("Ausencias sin justificación", Times(unjustifiedTimes), "-" + Hours(unjustifiedMinutes));
            Row("Total de horas trabajadas", "", Hours(trabajadas));
            Row("Fuera de horario", "", "+" + Hours(extra));
            Row("Total final", "", Hours(finalTotal));
            Row("Balance", "", balance > 0 ? "Debe " + Hours(balance) : balance < 0 ? "A favor " + Hours(-balance) : "Al día");
        }
        return sw.ToString();
    }

    static string Line(params object?[] values) => string.Join(';', values.Select(x => Q(Convert.ToString(x, CultureInfo.InvariantCulture))));
    static string Q(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    static string Time(DateTime? value) => value?.ToString("HH:mm:ss") ?? "";
    static string Hours(int minutes) { var a = Math.Abs(minutes); return $"{(minutes < 0 ? "-" : "")}{a / 60}:{a % 60:00}"; }
    static string Times(int count) => count == 1 ? "1 vez" : $"{count} veces";
    static string SegmentDetails(IEnumerable<SegmentAttendance> segments) => string.Join('|', segments.Select(x => string.Join(',', x.Number, x.ExpectedEntry.ToString(@"hh\:mm"), x.ExpectedExit.ToString(@"hh\:mm"), Time(x.ActualEntry), Time(x.ActualExit), x.WorkedMinutes, x.ExpectedMinutes, x.Closed ? "1" : "0", x.WorkedMorning, x.WorkedAfternoon, x.ExpectedMorning, x.ExpectedAfternoon)));
    static string LunchDetails(LunchAttendance? lunch) => lunch is null ? "" : string.Join(',', lunch.Start.ToString(@"hh\:mm"), lunch.End.ToString(@"hh\:mm"), Time(lunch.ActualExit), Time(lunch.ActualReturn));
}
