using AsistenciaSync.Configuration;
using AsistenciaSync.Services;

namespace AsistenciaSync.Models;

public sealed record EmployeeOption(string Id, string Name);
public sealed record EmployeeSchedule(string EmployeeId, bool Monday, bool Tuesday, bool Wednesday, bool Thursday, bool Friday, bool Saturday, bool Sunday, bool Discontinuous, TimeSpan Entry, TimeSpan Exit, TimeSpan SecondEntry, TimeSpan SecondExit)
{
    public bool WorksOn(DayOfWeek day) => day switch { DayOfWeek.Monday => Monday, DayOfWeek.Tuesday => Tuesday, DayOfWeek.Wednesday => Wednesday, DayOfWeek.Thursday => Thursday, DayOfWeek.Friday => Friday, DayOfWeek.Saturday => Saturday, _ => Sunday };
}
public sealed record DailyIncident(long Id, string EmployeeId, DateTime Date, string Type, string Reason, bool JustifiesAbsence, bool JustifiesLateness);

public static class AttendanceConfigurationStore
{
    public static List<EmployeeOption> ReadEmployees(AppSettings settings) => CsvStore.ReadEmployees(settings).OrderBy(x => x.Key).Select(x => new EmployeeOption(x.Key, x.Value)).ToList();
    public static Dictionary<string, EmployeeSchedule> ReadSchedules(AppSettings settings) => CsvStore.ReadSchedules(settings);
    public static void SaveSchedule(AppSettings settings, EmployeeSchedule schedule) => CsvStore.SaveSchedule(settings, schedule);
    public static List<DailyIncident> ReadIncidents(AppSettings settings, DateTime from, DateTime to) => CsvStore.ReadIncidents(settings, from, to);
    public static void SaveIncident(AppSettings settings, string employeeId, DateTime date, string type, string reason, bool absence, bool lateness) => CsvStore.SaveIncident(settings, employeeId, date, type, reason, absence, lateness);
}
