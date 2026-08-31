namespace AsistenciaSync.Services;

/// <summary>
/// Un día (o tramo) de un empleado que figura sin justificar en el reporte del periodo actual.
/// <see cref="Segment"/> nulo = día completo.
/// </summary>
public sealed record PendingJustification(
    string EmployeeId,
    string EmployeeName,
    DateTime Date,
    int? Segment,
    string SegmentLabel,
    int ExpectedMinutes,
    string State);
