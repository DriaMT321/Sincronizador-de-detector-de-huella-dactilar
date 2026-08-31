using AsistenciaSync.Models;

namespace AsistenciaSync.Services;

public static partial class ReportService
{
    const int AccidentalMarkWindowMinutes = 30;

    internal static List<CsvPunch> ValidMarks(List<CsvPunch> rawMarks, int maximum)
    {
        var valid = new List<CsvPunch>(); foreach (var mark in rawMarks) { if (valid.Count == 0 || (mark.Timestamp - valid[^1].Timestamp).TotalMinutes >= AccidentalMarkWindowMinutes) valid.Add(mark); if (valid.Count == maximum) break; } return valid;
    }

    static EmployeeSchedule ResolveSchedule(EmployeeSchedule schedule, IReadOnlyCollection<WorkdayType> types)
    {
        var typeId = string.IsNullOrWhiteSpace(schedule.WorkdayTypeId) ? (schedule.Discontinuous ? "discontinua" : "continua") : schedule.WorkdayTypeId; var type = types.FirstOrDefault(x => x.Id.Equals(typeId, StringComparison.OrdinalIgnoreCase)); if (type is null) return schedule; return schedule with { WorkdayTypeId = type.Id, Discontinuous = type.Discontinuous, Entry = type.Entry, Exit = type.Exit, SecondEntry = type.SecondEntry, SecondExit = type.SecondExit };
    }

    static WorkdayType? ResolveWorkdayType(EmployeeSchedule schedule, IReadOnlyCollection<WorkdayType> types)
    {
        var typeId = string.IsNullOrWhiteSpace(schedule.WorkdayTypeId) ? (schedule.Discontinuous ? "discontinua" : "continua") : schedule.WorkdayTypeId;
        return types.FirstOrDefault(x => x.Id.Equals(typeId, StringComparison.OrdinalIgnoreCase));
    }

    internal static TimeSpan MiddayDivider(LunchWindow? lunch) => lunch?.Start ?? new TimeSpan(12, 0, 0);

    /// <summary>Divide un intervalo del día en minutos de mañana y tarde, excluyendo el almuerzo.</summary>
    internal static (int Morning, int Afternoon) SplitByPeriod(DateTime start, DateTime end, DateTime day, LunchWindow? lunch)
    {
        return SplitInterval(start, end, day, MiddayDivider(lunch), lunch is null ? null : day.Add(lunch.Start), lunch is null ? null : day.Add(lunch.End));
    }

    /// <summary>Rótulo "HH:mm–HH:mm" del descanso entre el primer y el segundo tramo de una jornada doble; null si hay un solo tramo o no hay hueco.</summary>
    internal static string? BreakLabel(IReadOnlyList<WorkSegment> definitions)
    {
        if (definitions.Count < 2) return null;
        var end = definitions[0].Exit;
        var start = definitions[1].Entry;
        return start > end ? $"{end:hh\\:mm}–{start:hh\\:mm}" : null;
    }
}
