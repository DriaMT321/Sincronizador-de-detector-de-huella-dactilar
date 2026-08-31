namespace AsistenciaSync.Models;

public sealed record WorkSegment(TimeSpan Entry, TimeSpan Exit);

public sealed record LunchWindow(TimeSpan Start, TimeSpan End);
