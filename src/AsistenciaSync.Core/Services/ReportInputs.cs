using AsistenciaSync.Models;

namespace AsistenciaSync.Services;

/// <summary>
/// Datos de entrada ya materializados para <see cref="ReportService"/>. Es un DTO puro (sin IO):
/// permite ejercitar el cálculo de asistencia con datos en memoria y un "hoy" fijo.
/// </summary>
public sealed record ReportInputs(
    DateTime From,
    DateTime To,
    IReadOnlyDictionary<string, string> Employees,
    IReadOnlyDictionary<string, EmployeeSchedule> Schedules,
    IReadOnlyList<WorkdayType> WorkdayTypes,
    IReadOnlyList<DailyIncident> Incidents,
    IReadOnlyList<CsvPunch> Punches,
    IReadOnlyDictionary<string, DateTime> FirstPunchDates,
    IReadOnlyList<Holiday> Holidays,
    int ToleranceMinutes);
