using System.Globalization;
using AsistenciaSync.Configuration;
using AsistenciaSync.Models;

namespace AsistenciaSync.Services;

public static partial class CsvStore
{
    public static List<Holiday> ReadHolidays(AppSettings settings)
    {
        var path = Path.Combine(Folder(settings), "festivos.csv"); if (!File.Exists(path)) return new(); return ReadRows(path).Skip(1).Where(x => x.Length >= 2 && DateTime.TryParse(x[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out _)).Select(x => new Holiday(DateTime.Parse(x[0], CultureInfo.InvariantCulture).Date, x[1], x.Length >= 3 && !string.IsNullOrWhiteSpace(x[2]) ? x[2] : null, x.Length >= 4 && Bit(x[3]))).OrderBy(x => x.Date).ThenBy(x => x.EmployeeId).ToList();
    }

    public static void SaveHoliday(AppSettings settings, Holiday holiday)
    {
        var all = ReadHolidays(settings).Where(x => x.Date != holiday.Date || !string.Equals(x.EmployeeId, holiday.EmployeeId, StringComparison.OrdinalIgnoreCase)).Append(holiday).OrderBy(x => x.Date).ThenBy(x => x.EmployeeId); Write(Path.Combine(Folder(settings), "festivos.csv"), "Fecha;Descripción;ID empleado (vacío=todos);Cuenta como jornada laboral", all.Select(x => new[] { x.Date.ToString("yyyy-MM-dd"), x.Description, x.EmployeeId ?? "", Bool(x.CountsAsWorkday) }));
    }

    public static void DeleteHoliday(AppSettings settings, DateTime date, string? employeeId = null)
    {
        var all = ReadHolidays(settings).Where(x => x.Date != date.Date || (employeeId is not null && !string.Equals(x.EmployeeId, employeeId, StringComparison.OrdinalIgnoreCase))); Write(Path.Combine(Folder(settings), "festivos.csv"), "Fecha;Descripción;ID empleado (vacío=todos);Cuenta como jornada laboral", all.Select(x => new[] { x.Date.ToString("yyyy-MM-dd"), x.Description, x.EmployeeId ?? "", Bool(x.CountsAsWorkday) }));
    }

    public static Dictionary<string, EmployeeSchedule> ReadSchedules(AppSettings settings)
    {
        var path = Path.Combine(Folder(settings), "jornadas.csv"); var result = new Dictionary<string, EmployeeSchedule>(StringComparer.OrdinalIgnoreCase); if (!File.Exists(path)) return result;
        foreach (var row in ReadRows(path).Skip(1)) { if (row.Length >= 13 && TimeSpan.TryParse(row[9], out var entry) && TimeSpan.TryParse(row[10], out var exit) && TimeSpan.TryParse(row[11], out var secondEntry) && TimeSpan.TryParse(row[12], out var secondExit)) result[row[0]] = new EmployeeSchedule(row[0], Bit(row[1]), Bit(row[2]), Bit(row[3]), Bit(row[4]), Bit(row[5]), Bit(row[6]), Bit(row[7]), row[8].Equals("Discontinua", StringComparison.OrdinalIgnoreCase), entry, exit, secondEntry, secondExit, row.Length >= 14 ? row[13] : row[8].Equals("Discontinua", StringComparison.OrdinalIgnoreCase) ? "discontinua" : "continua"); else if (row.Length >= 10 && TimeSpan.TryParse(row[8], out entry) && TimeSpan.TryParse(row[9], out exit)) result[row[0]] = new EmployeeSchedule(row[0], Bit(row[1]), Bit(row[2]), Bit(row[3]), Bit(row[4]), Bit(row[5]), Bit(row[6]), Bit(row[7]), false, entry, exit, TimeSpan.Zero, TimeSpan.Zero, "continua"); }
        return result;
    }

    public static void SaveSchedule(AppSettings settings, EmployeeSchedule schedule)
    {
        var all = ReadSchedules(settings); all[schedule.EmployeeId] = schedule; var rows = all.OrderBy(x => x.Key).Select(x => new[] { x.Key, Bool(x.Value.Monday), Bool(x.Value.Tuesday), Bool(x.Value.Wednesday), Bool(x.Value.Thursday), Bool(x.Value.Friday), Bool(x.Value.Saturday), Bool(x.Value.Sunday), x.Value.Discontinuous ? "Discontinua" : "Continua", x.Value.Entry.ToString(@"hh\:mm"), x.Value.Exit.ToString(@"hh\:mm"), x.Value.SecondEntry.ToString(@"hh\:mm"), x.Value.SecondExit.ToString(@"hh\:mm"), string.IsNullOrWhiteSpace(x.Value.WorkdayTypeId) ? (x.Value.Discontinuous ? "discontinua" : "continua") : x.Value.WorkdayTypeId }); Write(Path.Combine(Folder(settings), "jornadas.csv"), "ID empleado;Lunes;Martes;Miércoles;Jueves;Viernes;Sábado;Domingo;Tipo jornada;Ingreso 1;Salida 1;Ingreso 2;Salida 2;ID tipo jornada", rows);
    }

    public static List<WorkdayType> ReadWorkdayTypes(AppSettings settings)
    {
        var path = Path.Combine(Folder(settings), "tipos_jornada.csv"); if (!File.Exists(path)) return DefaultWorkdayTypes(); var result = new List<WorkdayType>();
        foreach (var row in ReadRows(path).Skip(1))
        {
            if (row.Length < 2 || string.IsNullOrWhiteSpace(row[0]) || string.IsNullOrWhiteSpace(row[1])) continue;
            var id = row[1].Equals("Continua", StringComparison.OrdinalIgnoreCase) ? "continua" : row[1].Equals("Discontinua", StringComparison.OrdinalIgnoreCase) ? "discontinua" : row[0];
            WorkdayType? type = null;
            if (row.Length >= 3 && TrySegments(row[2], out var segments))
            {
                LunchWindow? lunch = null;
                if (row.Length >= 5 && TimeSpan.TryParse(row[3], out var lunchStart) && TimeSpan.TryParse(row[4], out var lunchEnd)) lunch = new LunchWindow(lunchStart, lunchEnd);
                type = FromSegments(id, row[1], segments, lunch);
            }
            else if (row.Length >= 6 && TimeSpan.TryParse(row[2], out var entry) && TimeSpan.TryParse(row[3], out var exit) && OptionalTime(row[4], out var secondEntry) && OptionalTime(row[5], out var secondExit))
            {
                var legacySegments = secondEntry != TimeSpan.Zero && secondExit != TimeSpan.Zero ? new[] { new WorkSegment(entry, exit), new WorkSegment(secondEntry, secondExit) } : new[] { new WorkSegment(entry, exit) };
                type = new WorkdayType(id, row[1], entry, exit, secondEntry, secondExit, legacySegments);
            }
            if (type is null) continue; result.RemoveAll(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase)); result.Add(type);
        }
        foreach (var defaultType in DefaultWorkdayTypes()) if (!result.Any(x => x.Id.Equals(defaultType.Id, StringComparison.OrdinalIgnoreCase))) result.Add(defaultType); return result;
    }

    public static void SaveWorkdayType(AppSettings settings, WorkdayType type)
    {
        ValidateWorkdayType(type); var all = ReadWorkdayTypes(settings).Where(x => !x.Id.Equals(type.Id, StringComparison.OrdinalIgnoreCase)).Append(type).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase); WriteWorkdayTypes(settings, all);
    }

    public static void DeleteWorkdayType(AppSettings settings, string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return; if (id.Equals("continua", StringComparison.OrdinalIgnoreCase) || id.Equals("discontinua", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Las jornadas Continua y Discontinua son necesarias para el sistema y no se pueden eliminar."); var selected = ReadWorkdayTypes(settings).FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase)); var assigned = ReadSchedules(settings).Values.Any(x => (string.IsNullOrWhiteSpace(x.WorkdayTypeId) ? (x.Discontinuous ? "discontinua" : "continua") : x.WorkdayTypeId).Equals(id, StringComparison.OrdinalIgnoreCase) || (selected is not null && string.IsNullOrWhiteSpace(x.WorkdayTypeId) && x.Discontinuous == selected.Discontinuous)); if (assigned) throw new InvalidOperationException("No se puede eliminar esta jornada porque está asignada a uno o más trabajadores."); var all = ReadWorkdayTypes(settings).Where(x => !x.Id.Equals(id, StringComparison.OrdinalIgnoreCase)); WriteWorkdayTypes(settings, all);
    }

    public static int ReadToleranceMinutes(AppSettings settings)
    {
        var path = Path.Combine(Folder(settings), "tolerancia.csv");
        if (!File.Exists(path)) return 5;
        var row = ReadRows(path).Skip(1).FirstOrDefault();
        return row is not null && row.Length > 0 && int.TryParse(row[0], out var minutes)
            ? Math.Clamp(minutes, 0, 180)
            : 5;
    }

    public static void SaveToleranceMinutes(AppSettings settings, int minutes)
    {
        if (minutes is < 0 or > 180) throw new InvalidOperationException("El tiempo de tolerancia debe estar entre 0 y 180 minutos.");
        Write(Path.Combine(Folder(settings), "tolerancia.csv"), "Minutos de tolerancia", new[] { new[] { minutes.ToString(CultureInfo.InvariantCulture) } });
    }

    static List<WorkdayType> DefaultWorkdayTypes() => new()
    {
        FromSegments("continua", "Continua", new[] { new WorkSegment(new TimeSpan(8, 30, 0), new TimeSpan(17, 0, 0)) }, null),
        FromSegments("discontinua", "Discontinua", new[] { new WorkSegment(new TimeSpan(8, 30, 0), new TimeSpan(12, 30, 0)), new WorkSegment(new TimeSpan(14, 20, 0), new TimeSpan(18, 30, 0)) }, null)
    };

    static WorkdayType FromSegments(string id, string name, IReadOnlyList<WorkSegment> segments, LunchWindow? lunch)
    {
        var first = segments[0]; var second = segments.Count > 1 ? segments[1] : null;
        return new WorkdayType(id, name, first.Entry, first.Exit, second?.Entry ?? TimeSpan.Zero, second?.Exit ?? TimeSpan.Zero, segments.ToArray(), lunch);
    }

    static void WriteWorkdayTypes(AppSettings settings, IEnumerable<WorkdayType> types) => Write(
        Path.Combine(Folder(settings), "tipos_jornada.csv"),
        "ID tipo;Nombre;Tramos;Almuerzo desde;Almuerzo hasta",
        types.Select(x => new[] { x.Id, x.Name, FormatSegments(x.Segments), x.Lunch?.Start.ToString(@"hh\:mm") ?? "", x.Lunch?.End.ToString(@"hh\:mm") ?? "" }));

    static string FormatSegments(IEnumerable<WorkSegment> segments) => string.Join('|', segments.Select(x => $"{x.Entry:hh\\:mm}-{x.Exit:hh\\:mm}"));

    static bool TrySegments(string value, out IReadOnlyList<WorkSegment> segments)
    {
        var parsed = new List<WorkSegment>();
        foreach (var part in (value ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var times = part.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (times.Length != 2 || !TimeSpan.TryParse(times[0], out var entry) || !TimeSpan.TryParse(times[1], out var exit)) { segments = Array.Empty<WorkSegment>(); return false; }
            parsed.Add(new WorkSegment(entry, exit));
        }
        segments = parsed; return parsed.Count > 0;
    }

    static void ValidateWorkdayType(WorkdayType type)
    {
        if (string.IsNullOrWhiteSpace(type.Id) || string.IsNullOrWhiteSpace(type.Name)) throw new InvalidOperationException("La jornada debe tener un identificador y un nombre.");
        if (type.Segments.Count == 0) throw new InvalidOperationException("La jornada debe tener al menos un tramo.");
        WorkSegment? previous = null;
        foreach (var segment in type.Segments)
        {
            if (segment.Entry >= segment.Exit) throw new InvalidOperationException("En cada tramo, la hora de ingreso debe ser anterior a la salida.");
            if (previous is not null && previous.Exit > segment.Entry) throw new InvalidOperationException("Los tramos deben estar ordenados y no pueden superponerse.");
            previous = segment;
        }
        if (type.Lunch is not null)
        {
            if (type.Lunch.Start >= type.Lunch.End) throw new InvalidOperationException("El inicio del almuerzo debe ser anterior a su finalización.");
            if (!type.Segments.Any(segment => type.Lunch.Start >= segment.Entry && type.Lunch.End <= segment.Exit)) throw new InvalidOperationException("El almuerzo opcional debe quedar dentro de uno de los tramos de trabajo.");
        }
    }
    static bool OptionalTime(string value, out TimeSpan result) { result = TimeSpan.Zero; return string.IsNullOrWhiteSpace(value) || TimeSpan.TryParse(value, out result); }
}
