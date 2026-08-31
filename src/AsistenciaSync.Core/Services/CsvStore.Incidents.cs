using System.Globalization;
using AsistenciaSync.Configuration;
using AsistenciaSync.Models;

namespace AsistenciaSync.Services;

public static partial class CsvStore
{
    const string IncidentHeader = "ID;ID empleado;Fecha;Tipo;Motivo;Justifica ausencia;Justifica tardanza;Justifica permiso;Horas permiso;Tramo";

    public static List<DailyIncident> ReadIncidents(AppSettings settings, DateTime from, DateTime to)
    {
        var path = Path.Combine(Folder(settings), "incidencias.csv"); var result = new List<DailyIncident>(); if (!File.Exists(path)) return result; var rows = ReadRows(path); var header = rows.FirstOrDefault(); var currentFormat = header?.Any(x => x.Equals("Justifica permiso", StringComparison.OrdinalIgnoreCase)) == true; var hasSegment = header?.Any(x => x.Equals("Tramo", StringComparison.OrdinalIgnoreCase)) == true;
        foreach (var row in rows.Skip(1))
        {
            if (row.Length < 7 || !DateTime.TryParse(row[2], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || date.Date < from.Date || date.Date > to.Date) continue;
            var segment = hasSegment && row.Length >= 10 && int.TryParse(row[9], out var parsedSegment) && parsedSegment > 0 ? parsedSegment : (int?)null;
            if (currentFormat) { var permission = row.Length >= 8 && Bit(row[7]); var hours = row.Length >= 9 && int.TryParse(row[8], out var parsedHours) ? Math.Max(0, parsedHours) : 0; result.Add(new DailyIncident(long.TryParse(row[0], out var id) ? id : 0, row[1], date, row[3], row[4], Bit(row[5]), Bit(row[6]), permission, hours * 60, segment)); }
            else { var oldHours = row.Length >= 8 && int.TryParse(row[7], out var parsedHours) ? parsedHours : 0; var oldMinutes = row.Length >= 9 && int.TryParse(row[8], out var parsedMinutes) ? parsedMinutes : 0; var permission = row[3].Equals("Permiso", StringComparison.OrdinalIgnoreCase) || oldHours > 0 || oldMinutes > 0; result.Add(new DailyIncident(long.TryParse(row[0], out var id) ? id : 0, row[1], date, row[3], row[4], Bit(row[5]), Bit(row[6]), permission, Math.Max(0, oldHours * 60 + oldMinutes), segment)); }
        }
        return result;
    }

    public static void SaveIncident(AppSettings settings, string employeeId, DateTime date, string type, string reason, bool absence, bool lateness, bool permission, int permissionHours = 0, int? segment = null)
    {
        var all = ReadAllIncidents(settings); var existing = all.FindIndex(x => x.EmployeeId.Equals(employeeId, StringComparison.OrdinalIgnoreCase) && x.Date.Date == date.Date && x.Segment == segment); var item = new DailyIncident(existing >= 0 ? all[existing].Id : all.Select(x => x.Id).DefaultIfEmpty(0).Max() + 1, employeeId, date.Date, type, reason, absence, lateness, permission, Math.Max(0, permissionHours) * 60, segment); if (existing >= 0) all[existing] = item; else all.Add(item);
        WriteIncidents(settings, all);
    }

    public static void DeleteIncident(AppSettings settings, string employeeId, DateTime date, int? segment)
    {
        var all = ReadAllIncidents(settings); all.RemoveAll(x => x.EmployeeId.Equals(employeeId, StringComparison.OrdinalIgnoreCase) && x.Date.Date == date.Date && x.Segment == segment);
        WriteIncidents(settings, all);
    }

    static void WriteIncidents(AppSettings settings, List<DailyIncident> all)
    {
        var rows = all.OrderBy(x => x.Date).ThenBy(x => x.EmployeeId).ThenBy(x => x.Segment ?? 0).Select(x => new[] { x.Id.ToString(CultureInfo.InvariantCulture), x.EmployeeId, x.Date.ToString("yyyy-MM-dd"), x.Type, x.Reason, Bool(x.JustifiesAbsence), Bool(x.JustifiesLateness), Bool(x.JustifiesPermission), (x.PermissionMinutes / 60).ToString(CultureInfo.InvariantCulture), x.Segment?.ToString(CultureInfo.InvariantCulture) ?? "" });
        Write(Path.Combine(Folder(settings), "incidencias.csv"), IncidentHeader, rows);
    }

    static List<DailyIncident> ReadAllIncidents(AppSettings settings) => ReadIncidents(settings, DateTime.MinValue, DateTime.MaxValue);
}
