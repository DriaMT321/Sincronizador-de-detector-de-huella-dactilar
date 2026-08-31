namespace AsistenciaSync.Models;

public sealed record WorkdayType(
    string Id,
    string Name,
    TimeSpan Entry,
    TimeSpan Exit,
    TimeSpan SecondEntry,
    TimeSpan SecondExit,
    IReadOnlyList<WorkSegment>? DefinedSegments = null,
    LunchWindow? Lunch = null)
{
    public IReadOnlyList<WorkSegment> Segments => DefinedSegments is { Count: > 0 }
        ? DefinedSegments
        : SecondEntry != TimeSpan.Zero && SecondExit != TimeSpan.Zero
            ? new[] { new WorkSegment(Entry, Exit), new WorkSegment(SecondEntry, SecondExit) }
            : new[] { new WorkSegment(Entry, Exit) };

    public bool Discontinuous => Segments.Count > 1;
}
