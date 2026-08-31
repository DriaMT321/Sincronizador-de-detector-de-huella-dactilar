namespace AsistenciaSync.Models;

public sealed record Holiday(DateTime Date, string Description, string? EmployeeId = null, bool CountsAsWorkday = false);
