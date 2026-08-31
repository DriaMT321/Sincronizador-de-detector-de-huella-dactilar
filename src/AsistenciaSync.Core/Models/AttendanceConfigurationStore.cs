using AsistenciaSync.Configuration;
using AsistenciaSync.Services;

namespace AsistenciaSync.Models;

public static class AttendanceConfigurationStore
{
    public static List<EmployeeOption> ReadEmployees(AppSettings settings) => CsvStore.ReadEmployees(settings).OrderBy(x => x.Key).Select(x => new EmployeeOption(x.Key, x.Value)).ToList();
    public static Dictionary<string, EmployeeSchedule> ReadSchedules(AppSettings settings) => CsvStore.ReadSchedules(settings);
    public static void SaveSchedule(AppSettings settings, EmployeeSchedule schedule) => CsvStore.SaveSchedule(settings, schedule);
    public static List<WorkdayType> ReadWorkdayTypes(AppSettings settings) => CsvStore.ReadWorkdayTypes(settings);
    public static void SaveWorkdayType(AppSettings settings, WorkdayType type) => CsvStore.SaveWorkdayType(settings, type);
    public static void DeleteWorkdayType(AppSettings settings, string id) => CsvStore.DeleteWorkdayType(settings, id);
    public static int ReadToleranceMinutes(AppSettings settings) => CsvStore.ReadToleranceMinutes(settings);
    public static void SaveToleranceMinutes(AppSettings settings, int minutes) => CsvStore.SaveToleranceMinutes(settings, minutes);
    public static List<DailyIncident> ReadIncidents(AppSettings settings, DateTime from, DateTime to) => CsvStore.ReadIncidents(settings, from, to);
    public static void SaveIncident(AppSettings settings, string employeeId, DateTime date, string type, string reason, bool absence, bool lateness, bool permission, int permissionHours = 0, int? segment = null) => CsvStore.SaveIncident(settings, employeeId, date, type, reason, absence, lateness, permission, permissionHours, segment);
    public static void DeleteIncident(AppSettings settings, string employeeId, DateTime date, int? segment) => CsvStore.DeleteIncident(settings, employeeId, date, segment);
}
