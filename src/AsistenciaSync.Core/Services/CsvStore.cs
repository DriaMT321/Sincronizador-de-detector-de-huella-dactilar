using System.Globalization;
using System.Text;
using AsistenciaSync.Configuration;
using AsistenciaSync.Models;

namespace AsistenciaSync.Services;

public sealed record CsvPunch(string EmployeeId, string Name, DateTime Timestamp, string Type, string Source);

public static partial class CsvStore
{
    const string Separator = ";";

    public static string Folder(AppSettings settings)
    {
        var folder = string.IsNullOrWhiteSpace(settings.CsvFolder) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "nibol") : settings.CsvFolder.Trim(); Directory.CreateDirectory(folder); return folder;
    }

    public static void SaveEmployees(AppSettings settings, IReadOnlyDictionary<string, string> users)
    {
        if (users.Count == 0) return; var existing = ReadEmployees(settings); foreach (var user in users) if (!string.IsNullOrWhiteSpace(user.Key)) existing[user.Key] = string.IsNullOrWhiteSpace(user.Value) ? user.Key : user.Value; var rows = existing.OrderBy(x => x.Key).Select(x => new[] { x.Key, x.Value, "1" }); Write(Path.Combine(Folder(settings), "empleados.csv"), "ID empleado;Nombre completo;Activo", rows);
    }

    public static Dictionary<string, string> ReadEmployees(AppSettings settings)
    {
        var path = Path.Combine(Folder(settings), "empleados.csv"); var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); if (!File.Exists(path)) return result; foreach (var row in ReadRows(path).Skip(1)) if (row.Length >= 2 && !string.IsNullOrWhiteSpace(row[0])) result[row[0]] = row[1]; return result;
    }

    public static void SaveEmployeesMap(AppSettings settings, IReadOnlyDictionary<string, string> users)
    {
        var rows = users.OrderBy(x => x.Key).Select(x => new[] { x.Key, string.IsNullOrWhiteSpace(x.Value) ? x.Key : x.Value, "1" }); Write(Path.Combine(Folder(settings), "empleados.csv"), "ID empleado;Nombre completo;Activo", rows);
    }

    public static int Save(AppSettings settings, IReadOnlyCollection<AttendanceRecord> records)
    {
        RotateMonthly(settings); var path = Path.Combine(Folder(settings), "marcaciones.csv"); var stored = ReadRows(path); var rows = stored.Count > 0 ? stored.Skip(1).ToList() : new List<string[]>(); var keys = rows.Where(x => x.Length >= 5).Select(x => DateTime.TryParse(x[2], CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp) ? $"{x[0]}|{timestamp:yyyy-MM-dd HH:mm:ss}|{x[4]}" : $"{x[0]}|{x[2]}|{x[4]}").ToHashSet(StringComparer.OrdinalIgnoreCase); var inserted = 0;
        foreach (var record in records) { var key = $"{record.UserId}|{record.Timestamp:yyyy-MM-dd HH:mm:ss}|{record.Source}"; if (!keys.Add(key)) continue; rows.Add(new[] { record.UserId, record.Name, record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), record.Type, record.Source }); inserted++; }
        Write(path, "ID empleado;Nombre completo;Fecha y hora;Tipo;Origen", rows); return inserted;
    }

    public static void ClearAllAttendance(AppSettings settings)
    {
        var folder = Folder(settings); foreach (var file in Directory.Exists(Path.Combine(folder, "historial")) ? Directory.EnumerateFiles(Path.Combine(folder, "historial"), "marcaciones.csv", SearchOption.AllDirectories).Append(Path.Combine(folder, "marcaciones.csv")) : new[] { Path.Combine(folder, "marcaciones.csv") }) if (File.Exists(file)) File.Delete(file);
    }

    public static List<CsvPunch> ReadPunches(AppSettings settings, DateTime from, DateTime to)
    {
        var result = new List<CsvPunch>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); foreach (var path in PunchFiles(settings)) foreach (var row in ReadRows(path).Skip(1)) if (row.Length >= 5 && DateTime.TryParse(row[2], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && date >= from && date < to.AddDays(1) && seen.Add($"{row[0]}|{row[2]}|{row[4]}")) result.Add(new CsvPunch(row[0], row[1], date, row[3], row[4])); return result;
    }

    public static DateTime? EarliestPunchDate(AppSettings settings)
    {
        DateTime? earliest = null; foreach (var path in PunchFiles(settings)) foreach (var row in ReadRows(path).Skip(1)) if (row.Length >= 3 && DateTime.TryParse(row[2], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && (earliest is null || date.Date < earliest.Value.Date)) earliest = date.Date; return earliest;
    }

    public static Dictionary<string, DateTime> ReadFirstPunchDates(AppSettings settings)
    {
        var result = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase); foreach (var path in PunchFiles(settings)) foreach (var row in ReadRows(path).Skip(1)) if (row.Length >= 3 && !string.IsNullOrWhiteSpace(row[0]) && DateTime.TryParse(row[2], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && (!result.TryGetValue(row[0], out var current) || date.Date < current)) result[row[0]] = date.Date; return result;
    }

    static IEnumerable<string> PunchFiles(AppSettings settings)
    {
        var folder = Folder(settings); var current = Path.Combine(folder, "marcaciones.csv"); if (File.Exists(current)) yield return current; var history = Path.Combine(folder, "historial"); if (!Directory.Exists(history)) yield break; foreach (var file in Directory.EnumerateFiles(history, "marcaciones.csv", SearchOption.AllDirectories)) yield return file;
    }

    static void RotateMonthly(AppSettings settings)
    {
        var folder = Folder(settings); var currentPath = Path.Combine(folder, "marcaciones.csv"); if (!File.Exists(currentPath)) return; var rows = ReadRows(currentPath).Skip(1).Where(x => x.Length >= 5).ToList(); if (rows.Count == 0) return; var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); var currentRows = new List<string[]>();
        foreach (var group in rows.GroupBy(row => ParseMonth(row[2]))) { if (group.Key == currentMonth) { currentRows.AddRange(group); continue; } var monthFolder = Path.Combine(folder, "historial", group.Key.ToString("yyyy-MM", CultureInfo.InvariantCulture)); Directory.CreateDirectory(monthFolder); var archivePath = Path.Combine(monthFolder, "marcaciones.csv"); var archived = File.Exists(archivePath) ? ReadRows(archivePath).Skip(1).ToList() : new List<string[]>(); var keys = archived.Where(x => x.Length >= 5).Select(x => $"{x[0]}|{x[2]}|{x[4]}").ToHashSet(StringComparer.OrdinalIgnoreCase); archived.AddRange(group.Where(x => x.Length >= 5 && keys.Add($"{x[0]}|{x[2]}|{x[4]}"))); Write(archivePath, "ID empleado;Nombre completo;Fecha y hora;Tipo;Origen", archived); }
        Write(currentPath, "ID empleado;Nombre completo;Fecha y hora;Tipo;Origen", currentRows);
    }

    static DateTime ParseMonth(string value) => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? new DateTime(date.Year, date.Month, 1) : new DateTime(2000, 1, 1);
    static bool Bit(string value) => value.Equals("1") || value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("sí", StringComparison.OrdinalIgnoreCase);
    static string Bool(bool value) => value ? "1" : "0";
    static void Write(string path, string header, IEnumerable<string[]> rows) { using var sw = new StreamWriter(path, false, new UnicodeEncoding(false, true)); sw.WriteLine("sep=;"); sw.WriteLine(header); foreach (var row in rows) sw.WriteLine(string.Join(Separator, row.Select(Q))); }
    static List<string[]> ReadRows(string path) { if (!File.Exists(path)) return new List<string[]>(); using var reader = new StreamReader(path, Encoding.UTF8, true); var lines = reader.ReadToEnd().Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries); var start = lines.Length > 0 && lines[0].StartsWith("sep=") ? 1 : 0; return lines.Skip(start).Select(Parse).Select(x => x.ToArray()).ToList(); }
    static List<string> Parse(string line) { var result = new List<string>(); var current = new StringBuilder(); var quoted = false; foreach (var c in line) { if (c == '"') quoted = !quoted; else if (c == ';' && !quoted) { result.Add(current.ToString()); current.Clear(); } else current.Append(c); } result.Add(current.ToString()); return result; }
    static string Q(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
}
