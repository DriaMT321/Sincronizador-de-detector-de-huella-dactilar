using System.Data.Odbc;
using System.Globalization;
using System.Text;

namespace AsistenciaSync.Backend;

public sealed record ReportResult(string DetailPath, string SummaryPath, int DetailRows);

public static class ReportService
{
    public static ReportResult Generate(AppSettings settings)
    {
        var from = settings.ReportFrom.Date;
        var to = settings.ReportTo.Date;
        if (from > to) throw new InvalidOperationException("La fecha inicial no puede ser posterior a la fecha final.");
        using var cn = Open(settings);
        EnsureEmployees(cn);
        var employees = ReadEmployees(cn);
        var schedules = ReadSchedules(cn, settings);
        var incidents = ReadIncidents(cn, settings, from, to);
        var punches = ReadPunches(cn, from, to);
        var details = new List<DailyRow>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            foreach (var employee in employees)
            {
                var schedule = schedules.TryGetValue(employee.Key, out var configured) ? configured : new EmployeeSchedule(employee.Key, true, true, true, true, true, false, false, settings.EntryTime, settings.ExitTime);
                var incident = incidents.FirstOrDefault(i => i.EmployeeId.Equals(employee.Key, StringComparison.OrdinalIgnoreCase) && i.Date.Date == day);
                var rows = punches.Where(p => p.EmpleadoId == employee.Key && p.FechaHora.Date == day).OrderBy(p => p.FechaHora).ToList();
                var entry = rows.FirstOrDefault(p => p.Tipo.Equals("Entrada", StringComparison.OrdinalIgnoreCase));
                var exit = entry is null ? null : rows.FirstOrDefault(p => p.Tipo.Equals("Salida", StringComparison.OrdinalIgnoreCase) && p.FechaHora > entry.FechaHora);
                var expectedIn = day.Add(schedule.Entry);
                var expectedOut = day.Add(schedule.Exit);
                if (expectedOut <= expectedIn) expectedOut = expectedOut.AddDays(1);
                var workingDay = schedule.WorksOn(day.DayOfWeek);
                var late = entry is null || incident?.JustifiesLateness == true ? 0 : Math.Max(0, (int)(entry.FechaHora - expectedIn).TotalMinutes);
                var early = exit is null ? 0 : Math.Max(0, (int)(expectedOut - exit.FechaHora).TotalMinutes);
                var worked = entry is null || exit is null ? 0 : Math.Max(0, (int)(exit.FechaHora - entry.FechaHora).TotalMinutes);
                var expected = workingDay ? (int)(expectedOut - expectedIn).TotalMinutes : 0;
                var state = !workingDay ? "No laborable" : entry is null && incident?.JustifiesAbsence == true ? $"Ausencia justificada ({incident.Type})" : entry is null ? "Ausente" : exit is null && incident?.JustifiesAbsence == true ? $"Salida justificada ({incident.Type})" : exit is null ? "Incompleto" : late > 0 && early > 0 ? "Tarde y salida anticipada" : late > 0 ? "Tarde" : early > 0 ? "Salida anticipada" : incident is not null ? $"Completo ({incident.Type})" : "Completo";
                details.Add(new DailyRow(employee.Key, employee.Value, day, entry?.FechaHora, exit?.FechaHora, worked, expected, late, early, Math.Max(0, expected - worked), Math.Max(0, worked - expected), state));
            }
        }
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "nibol", "reportes");
        Directory.CreateDirectory(folder);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var detailPath = Path.Combine(folder, $"reporte_detalle_{stamp}.csv");
        var summaryPath = Path.Combine(folder, $"reporte_resumen_{stamp}.csv");
        WriteDetails(detailPath, details);
        WriteSummary(summaryPath, details);
        return new ReportResult(detailPath, summaryPath, details.Count);
    }

    static OdbcConnection Open(AppSettings s)
    {
        var cs = s.SqlAuthentication
            ? $"Driver={{ODBC Driver 17 for SQL Server}};Server={s.Server};Database={s.Database};Uid={s.SqlUser};Pwd={s.SqlPassword};Trusted_Connection=No;TrustServerCertificate=Yes;"
            : $"Driver={{ODBC Driver 17 for SQL Server}};Server={s.Server};Database={s.Database};Trusted_Connection=Yes;TrustServerCertificate=Yes;";
        var cn = new OdbcConnection(cs); cn.Open(); return cn;
    }

    static void EnsureEmployees(OdbcConnection cn)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "IF OBJECT_ID('dbo.Empleados','U') IS NULL CREATE TABLE dbo.Empleados (EmpleadoId VARCHAR(30) NOT NULL PRIMARY KEY, Nombre VARCHAR(150) NOT NULL, Activo BIT NOT NULL CONSTRAINT DF_Empleados_Activo DEFAULT 1);";
        cmd.ExecuteNonQuery();
    }

    static Dictionary<string, EmployeeSchedule> ReadSchedules(OdbcConnection cn, AppSettings settings)
    {
        using var cmd = cn.CreateCommand(); cmd.CommandText = "IF OBJECT_ID('dbo.EmpleadoJornadas','U') IS NULL SELECT CAST(NULL AS VARCHAR(30)) WHERE 1=0 ELSE SELECT EmpleadoId,Lunes,Martes,Miercoles,Jueves,Viernes,Sabado,Domingo,HoraEntrada,HoraSalida FROM dbo.EmpleadoJornadas;";
        using var rd = cmd.ExecuteReader(); var result = new Dictionary<string, EmployeeSchedule>(StringComparer.OrdinalIgnoreCase);
        while (rd.Read()) result[rd.GetString(0)] = new EmployeeSchedule(rd.GetString(0), rd.GetBoolean(1), rd.GetBoolean(2), rd.GetBoolean(3), rd.GetBoolean(4), rd.GetBoolean(5), rd.GetBoolean(6), rd.GetBoolean(7), TimeSpan.Parse(rd.GetString(8)), TimeSpan.Parse(rd.GetString(9)));
        return result;
    }

    static List<DailyIncident> ReadIncidents(OdbcConnection cn, AppSettings settings, DateTime from, DateTime to)
    {
        using var cmd = cn.CreateCommand(); cmd.CommandText = "IF OBJECT_ID('dbo.IncidenciasAsistencia','U') IS NULL SELECT CAST(NULL AS BIGINT) WHERE 1=0 ELSE SELECT Id,EmpleadoId,Fecha,Tipo,Motivo,JustificaAusencia,JustificaTardanza FROM dbo.IncidenciasAsistencia WHERE Fecha>=? AND Fecha<=?;";
        cmd.Parameters.Add("from", OdbcType.Date).Value = from.Date; cmd.Parameters.Add("to", OdbcType.Date).Value = to.Date;
        using var rd = cmd.ExecuteReader(); var result = new List<DailyIncident>();
        while (rd.Read()) result.Add(new DailyIncident(rd.GetInt64(0), rd.GetString(1), rd.GetDateTime(2), rd.GetString(3), rd.IsDBNull(4) ? "" : rd.GetString(4), rd.GetBoolean(5), rd.GetBoolean(6)));
        return result;
    }

    static Dictionary<string, string> ReadEmployees(OdbcConnection cn)
    {
        using var cmd = cn.CreateCommand(); cmd.CommandText = "SELECT EmpleadoId, Nombre FROM dbo.Empleados WHERE Activo = 1 ORDER BY EmpleadoId;";
        using var rd = cmd.ExecuteReader(); var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (rd.Read()) result[rd.GetString(0)] = rd.GetString(1); return result;
    }

    static List<PunchRow> ReadPunches(OdbcConnection cn, DateTime from, DateTime to)
    {
        using var cmd = cn.CreateCommand(); cmd.CommandText = "SELECT EmpleadoId, Nombre, FechaHora, Tipo FROM dbo.Marcaciones WHERE FechaHora >= ? AND FechaHora < ? ORDER BY EmpleadoId, FechaHora;";
        cmd.Parameters.Add("from", OdbcType.DateTime).Value = from; cmd.Parameters.Add("to", OdbcType.DateTime).Value = to.AddDays(1);
        using var rd = cmd.ExecuteReader(); var result = new List<PunchRow>();
        while (rd.Read()) result.Add(new PunchRow(rd.GetString(0), rd.IsDBNull(1) ? rd.GetString(0) : rd.GetString(1), rd.GetDateTime(2), rd.GetString(3)));
        return result;
    }

    static void WriteDetails(string path, List<DailyRow> rows)
    {
        using var sw = new StreamWriter(path, false, new UnicodeEncoding(false, true));
        sw.WriteLine("sep=;");
        sw.WriteLine("ID empleado;Nombre completo;Fecha;Hora de entrada;Hora de salida;Horas trabajadas;Horas esperadas;Tiempo de tardanza;Salida anticipada;Tiempo faltante;Tiempo extra;Estado");
        foreach (var r in rows) sw.WriteLine(string.Join(';', Q(r.EmpleadoId), Q(r.Nombre), Q(r.Fecha.ToString("dd/MM/yyyy")), Q(Time(r.Entrada)), Q(Time(r.Salida)), Q(Hours(r.Trabajadas)), Q(Hours(r.Esperadas)), Q(Hours(r.MinutosTarde)), Q(Hours(r.MinutosSalidaAnticipada)), Q(Hours(r.MinutosFaltantes)), Q(Hours(r.MinutosExtra)), Q(r.Estado)));
    }

    static void WriteSummary(string path, List<DailyRow> rows)
    {
        using var sw = new StreamWriter(path, false, new UnicodeEncoding(false, true));
        sw.WriteLine("sep=;");
        sw.WriteLine("ID empleado;Nombre completo;Días del periodo;Días trabajados;Días ausente;Días incompletos;Horas trabajadas;Horas esperadas;Tiempo de tardanza;Salida anticipada;Tiempo faltante;Tiempo extra");
        foreach (var g in rows.GroupBy(r => new { r.EmpleadoId, r.Nombre }))
        {
            var workedDays = g.Count(r => r.Entrada.HasValue && r.Salida.HasValue); var absent = g.Count(r => r.Estado == "Ausente"); var incomplete = g.Count(r => r.Estado == "Incompleto");
            sw.WriteLine(string.Join(';', Q(g.Key.EmpleadoId), Q(g.Key.Nombre), Q(g.Count().ToString()), Q(workedDays.ToString()), Q(absent.ToString()), Q(incomplete.ToString()), Q(Hours(g.Sum(r => r.Trabajadas))), Q(Hours(g.Sum(r => r.Esperadas))), Q(Hours(g.Sum(r => r.MinutosTarde))), Q(Hours(g.Sum(r => r.MinutosSalidaAnticipada))), Q(Hours(g.Sum(r => r.MinutosFaltantes))), Q(Hours(g.Sum(r => r.MinutosExtra)))));
        }
    }

    static string Q(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
    static string Time(DateTime? d) => d?.ToString("HH:mm:ss") ?? "";
    static string Hours(int minutes) => $"{minutes / 60}:{minutes % 60:00}";
    sealed record PunchRow(string EmpleadoId, string Nombre, DateTime FechaHora, string Tipo);
    sealed record DailyRow(string EmpleadoId, string Nombre, DateTime Fecha, DateTime? Entrada, DateTime? Salida, int Trabajadas, int Esperadas, int MinutosTarde, int MinutosSalidaAnticipada, int MinutosFaltantes, int MinutosExtra, string Estado);
}
