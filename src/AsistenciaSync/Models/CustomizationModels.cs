namespace AsistenciaSync.Models;

public sealed record Holiday(DateTime Date, string Description, string? EmployeeId = null);

public sealed class StatusOption
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#415066";
}
