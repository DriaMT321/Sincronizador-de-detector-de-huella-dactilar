namespace AsistenciaSync.Models;

/// <summary>
/// Justificación o falta de un empleado en una fecha. <see cref="Segment"/> nulo = aplica a todo el
/// día; con valor = aplica solo a ese tramo (1 = primer tramo, 2 = segundo, …).
/// </summary>
public sealed record DailyIncident(long Id, string EmployeeId, DateTime Date, string Type, string Reason, bool JustifiesAbsence, bool JustifiesLateness, bool JustifiesPermission, int PermissionMinutes = 0, int? Segment = null);
