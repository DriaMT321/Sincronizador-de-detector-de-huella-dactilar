using System.Globalization;
using System.Text;

using AsistenciaSync.Configuration;
using AsistenciaSync.Models;

namespace AsistenciaSync.Services;

internal sealed record CsvPunch(string EmployeeId, string Name, DateTime Timestamp, string Type, string Source);

internal static class CsvStore
{
    const string Separator = ";";

    public static string Folder(AppSettings settings)
    {
        var folder = string.IsNullOrWhiteSpace(settings.CsvFolder) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "nibol") : settings.CsvFolder.Trim();
        Directory.CreateDirectory(folder); return folder;
    }

    public static void SaveEmployees(AppSettings settings, IReadOnlyDictionary<string, string> users)
    {
        if (users.Count == 0) return; var existing = ReadEmployees(settings);
        foreach (var user in users) if (!string.IsNullOrWhiteSpace(user.Key)) existing[user.Key] = string.IsNullOrWhiteSpace(user.Value) ? user.Key : user.Value;
        var rows = existing.OrderBy(x => x.Key).Select(x => new[] { x.Key, x.Value, "1" }); Write(Path.Combine(Folder(settings), "empleados.csv"), "ID empleado;Nombre completo;Activo", rows);
    }

    public static int Save(AppSettings settings, IReadOnlyCollection<AttendanceRecord> records)
    {
        RotateMonthly(settings); var path = Path.Combine(Folder(settings), "marcaciones.csv"); var rows = ReadRows(path); var keys = rows.Skip(1).Where(x => x.Length >= 5).Select(x => $"{x[0]}|{x[2]}|{x[4]}").ToHashSet(StringComparer.OrdinalIgnoreCase); var inserted = 0;
        foreach (var record in records)
        {
            var key = $"{record.UserId}|{record.Timestamp:O}|{record.Source}"; if (!keys.Add(key)) continue;
            rows.Add(new[] { record.UserId, record.Name, record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), record.Type, record.Source }); inserted++;
        }
        Write(path, "ID empleado;Nombre completo;Fecha y hora;Tipo;Origen", rows.Skip(1)); return inserted;
    }

    public static Dictionary<string, string> ReadEmployees(AppSettings settings)
    {
        var path = Path.Combine(Folder(settings), "empleados.csv"); var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); if (!File.Exists(path)) return result;
        foreach (var row in ReadRows(path).Skip(1)) if (row.Length >= 2 && !string.IsNullOrWhiteSpace(row[0])) result[row[0]] = row[1]; return result;
    }

    public static void SaveEmployeesMap(AppSettings settings, IReadOnlyDictionary<string, string> users)
    {
        var rows = users.OrderBy(x => x.Key).Select(x => new[] { x.Key, string.IsNullOrWhiteSpace(x.Value) ? x.Key : x.Value, "1" });
        Write(Path.Combine(Folder(settings), "empleados.csv"), "ID empleado;Nombre completo;Activo", rows);
    }

    public static List<Holiday> ReadHolidays(AppSettings settings)
    {
        var path = Path.Combine(Folder(settings), "festivos.csv");
        if (!File.Exists(path)) return new();
        return ReadRows(path).Skip(1).Where(x => x.Length >= 2 && DateTime.TryParse(x[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            .Select(x => new Holiday(DateTime.Parse(x[0], CultureInfo.InvariantCulture).Date, x[1], x.Length >= 3 && !string.IsNullOrWhiteSpace(x[2]) ? x[2] : null)).OrderBy(x => x.Date).ThenBy(x => x.EmployeeId).ToList();
    }

    public static void SaveHoliday(AppSettings settings, Holiday holiday)
    {
        var all = ReadHolidays(settings).Where(x => x.Date != holiday.Date || !string.Equals(x.EmployeeId, holiday.EmployeeId, StringComparison.OrdinalIgnoreCase)).Append(holiday).OrderBy(x => x.Date).ThenBy(x => x.EmployeeId);
        Write(Path.Combine(Folder(settings), "festivos.csv"), "Fecha;Descripción;ID empleado (vacío=todos)", all.Select(x => new[] { x.Date.ToString("yyyy-MM-dd"), x.Description, x.EmployeeId ?? "" }));
    }

    public static void DeleteHoliday(AppSettings settings, DateTime date)
    {
        var all = ReadHolidays(settings).Where(x => x.Date != date.Date);
        Write(Path.Combine(Folder(settings), "festivos.csv"), "Fecha;Descripción;ID empleado (vacío=todos)", all.Select(x => new[] { x.Date.ToString("yyyy-MM-dd"), x.Description, x.EmployeeId ?? "" }));
    }

    public static void ClearAllAttendance(AppSettings settings)
    {
        var folder = Folder(settings); foreach (var file in Directory.Exists(Path.Combine(folder, "historial")) ? Directory.EnumerateFiles(Path.Combine(folder, "historial"), "marcaciones.csv", SearchOption.AllDirectories).Append(Path.Combine(folder, "marcaciones.csv")) : new[] { Path.Combine(folder, "marcaciones.csv") })
            if (File.Exists(file)) File.Delete(file);
    }

    public static List<CsvPunch> ReadPunches(AppSettings settings, DateTime from, DateTime to)
    {
        var result = new List<CsvPunch>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in PunchFiles(settings)) foreach (var row in ReadRows(path).Skip(1)) if (row.Length >= 5 && DateTime.TryParse(row[2], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && date >= from && date < to.AddDays(1) && seen.Add($"{row[0]}|{row[2]}|{row[4]}")) result.Add(new CsvPunch(row[0], row[1], date, row[3], row[4]));
        return result;
    }

    public static Dictionary<string, EmployeeSchedule> ReadSchedules(AppSettings settings)
    {
        var path = Path.Combine(Folder(settings), "jornadas.csv"); var result = new Dictionary<string, EmployeeSchedule>(StringComparer.OrdinalIgnoreCase); if (!File.Exists(path)) return result;
        foreach (var row in ReadRows(path).Skip(1))
        {
            if (row.Length >= 13 && TimeSpan.TryParse(row[9], out var entry) && TimeSpan.TryParse(row[10], out var exit) && TimeSpan.TryParse(row[11], out var secondEntry) && TimeSpan.TryParse(row[12], out var secondExit)) result[row[0]] = new EmployeeSchedule(row[0], Bit(row[1]), Bit(row[2]), Bit(row[3]), Bit(row[4]), Bit(row[5]), Bit(row[6]), Bit(row[7]), row[8].Equals("Discontinua", StringComparison.OrdinalIgnoreCase), entry, exit, secondEntry, secondExit);
            else if (row.Length >= 10 && TimeSpan.TryParse(row[8], out entry) && TimeSpan.TryParse(row[9], out exit)) result[row[0]] = new EmployeeSchedule(row[0], Bit(row[1]), Bit(row[2]), Bit(row[3]), Bit(row[4]), Bit(row[5]), Bit(row[6]), Bit(row[7]), false, entry, exit, TimeSpan.Zero, TimeSpan.Zero);
        }
        return result;
    }

    public static void SaveSchedule(AppSettings settings, EmployeeSchedule schedule)
    {
        var all = ReadSchedules(settings); all[schedule.EmployeeId] = schedule; var rows = all.OrderBy(x => x.Key).Select(x => new[] { x.Key, Bool(x.Value.Monday), Bool(x.Value.Tuesday), Bool(x.Value.Wednesday), Bool(x.Value.Thursday), Bool(x.Value.Friday), Bool(x.Value.Saturday), Bool(x.Value.Sunday), x.Value.Discontinuous ? "Discontinua" : "Continua", x.Value.Entry.ToString(@"hh\:mm"), x.Value.Exit.ToString(@"hh\:mm"), x.Value.SecondEntry.ToString(@"hh\:mm"), x.Value.SecondExit.ToString(@"hh\:mm") }); Write(Path.Combine(Folder(settings), "jornadas.csv"), "ID empleado;Lunes;Martes;Miércoles;Jueves;Viernes;Sábado;Domingo;Tipo jornada;Ingreso 1;Salida 1;Ingreso 2;Salida 2", rows);
    }

    public static List<DailyIncident> ReadIncidents(AppSettings settings, DateTime from, DateTime to)
    {
        var path = Path.Combine(Folder(settings), "incidencias.csv"); var result = new List<DailyIncident>(); if (!File.Exists(path)) return result;
        foreach (var row in ReadRows(path).Skip(1)) if (row.Length >= 7 && DateTime.TryParse(row[2], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && date.Date >= from.Date && date.Date <= to.Date) result.Add(new DailyIncident(long.TryParse(row[0], out var id) ? id : 0, row[1], date, row[3], row[4], Bit(row[5]), Bit(row[6]))); return result;
    }

    public static void SaveIncident(AppSettings settings, string employeeId, DateTime date, string type, string reason, bool absence, bool lateness)
    {
        var all = ReadAllIncidents(settings); var existing = all.FindIndex(x => x.EmployeeId.Equals(employeeId, StringComparison.OrdinalIgnoreCase) && x.Date.Date == date.Date); var item = new DailyIncident(existing >= 0 ? all[existing].Id : all.Select(x => x.Id).DefaultIfEmpty(0).Max() + 1, employeeId, date.Date, type, reason, absence, lateness); if (existing >= 0) all[existing] = item; else all.Add(item);
        var rows = all.OrderBy(x => x.Date).ThenBy(x => x.EmployeeId).Select(x => new[] { x.Id.ToString(), x.EmployeeId, x.Date.ToString("yyyy-MM-dd"), x.Type, x.Reason, Bool(x.JustifiesAbsence), Bool(x.JustifiesLateness) }); Write(Path.Combine(Folder(settings), "incidencias.csv"), "ID;ID empleado;Fecha;Tipo;Motivo;Justifica ausencia;Justifica tardanza", rows);
    }

    static List<DailyIncident> ReadAllIncidents(AppSettings settings) => ReadIncidents(settings, DateTime.MinValue, DateTime.MaxValue);
    static IEnumerable<string> PunchFiles(AppSettings settings)
    {
        var folder = Folder(settings); var current = Path.Combine(folder, "marcaciones.csv"); if (File.Exists(current)) yield return current;
        var history = Path.Combine(folder, "historial"); if (!Directory.Exists(history)) yield break;
        foreach (var file in Directory.EnumerateFiles(history, "marcaciones.csv", SearchOption.AllDirectories)) yield return file;
    }

    static void RotateMonthly(AppSettings settings)
    {
        var folder = Folder(settings); var currentPath = Path.Combine(folder, "marcaciones.csv"); if (!File.Exists(currentPath)) return;
        var rows = ReadRows(currentPath).Skip(1).Where(x => x.Length >= 5).ToList(); if (rows.Count == 0) return;
        var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); var currentRows = new List<string[]>();
        foreach (var group in rows.GroupBy(row => ParseMonth(row[2])))
        {
            if (group.Key == currentMonth) { currentRows.AddRange(group); continue; }
            var monthFolder = Path.Combine(folder, "historial", group.Key.ToString("yyyy-MM", CultureInfo.InvariantCulture)); Directory.CreateDirectory(monthFolder); var archivePath = Path.Combine(monthFolder, "marcaciones.csv"); var archived = File.Exists(archivePath) ? ReadRows(archivePath).Skip(1).ToList() : new List<string[]>(); var keys = archived.Where(x => x.Length >= 5).Select(x => $"{x[0]}|{x[2]}|{x[4]}").ToHashSet(StringComparer.OrdinalIgnoreCase); archived.AddRange(group.Where(x => x.Length >= 5 && keys.Add($"{x[0]}|{x[2]}|{x[4]}"))); Write(archivePath, "ID empleado;Nombre completo;Fecha y hora;Tipo;Origen", archived);
        }
        Write(currentPath, "ID empleado;Nombre completo;Fecha y hora;Tipo;Origen", currentRows);
    }

    static DateTime ParseMonth(string value) { return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? new DateTime(date.Year, date.Month, 1) : new DateTime(2000, 1, 1); }
    static bool Bit(string value) => value.Equals("1") || value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("sí", StringComparison.OrdinalIgnoreCase);
    static string Bool(bool value) => value ? "1" : "0";
    static void Write(string path, string header, IEnumerable<string[]> rows) { using var sw = new StreamWriter(path, false, new UnicodeEncoding(false, true)); sw.WriteLine("sep=;"); sw.WriteLine(header); foreach (var row in rows) sw.WriteLine(string.Join(Separator, row.Select(Q))); }
    static List<string[]> ReadRows(string path) { if (!File.Exists(path)) return new List<string[]>(); using var reader = new StreamReader(path, Encoding.UTF8, true); var lines = reader.ReadToEnd().Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries); var start = lines.Length > 0 && lines[0].StartsWith("sep=") ? 1 : 0; return lines.Skip(start).Select(Parse).Select(x => x.ToArray()).ToList(); }
    static List<string> Parse(string line) { var result = new List<string>(); var current = new StringBuilder(); var quoted = false; foreach (var c in line) { if (c == '"') quoted = !quoted; else if (c == ';' && !quoted) { result.Add(current.ToString()); current.Clear(); } else current.Append(c); } result.Add(current.ToString()); return result; }
    static string Q(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
}
