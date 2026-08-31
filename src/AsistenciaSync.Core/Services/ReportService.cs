using AsistenciaSync.Configuration;
using AsistenciaSync.Models;

namespace AsistenciaSync.Services;

public static partial class ReportService
{
    /// <summary>Genera el reporte del periodo configurado leyendo los CSV del almacén local.</summary>
    public static ReportDocument Build(AppSettings settings)
    {
        var from = settings.ReportFrom.Date;
        var to = settings.ReportTo.Date;
        if (from > to) throw new InvalidOperationException("La fecha inicial no puede ser posterior a la fecha final.");

        return Build(Load(settings, from, to), DateTime.Now, Path.Combine(CsvStore.Folder(settings), "reportes"));
    }

    static ReportInputs Load(AppSettings settings, DateTime from, DateTime to) => new(
        from,
        to,
        CsvStore.ReadEmployees(settings),
        CsvStore.ReadSchedules(settings),
        CsvStore.ReadWorkdayTypes(settings),
        CsvStore.ReadIncidents(settings, from, to),
        CsvStore.ReadPunches(settings, from, to),
        CsvStore.ReadFirstPunchDates(settings),
        CsvStore.ReadHolidays(settings),
        CsvStore.ReadToleranceMinutes(settings));

    /// <summary>Genera el reporte a partir de datos ya materializados (sin IO). Testeable de forma determinista.</summary>
    public static ReportDocument Build(ReportInputs inputs, DateTime generatedAt, string downloadFolder)
    {
        var details = BuildRows(inputs, generatedAt);
        return new ReportDocument(WriteDetails(details), WriteSummary(details), downloadFolder, generatedAt, inputs.From.Date, inputs.To.Date, details.Count);
    }

    /// <summary>Días/tramos del periodo indicado (por defecto el configurado) sin justificar para el empleado indicado (o todos).</summary>
    public static IReadOnlyList<PendingJustification> Pending(AppSettings settings, string? employeeId = null, DateTime? from = null, DateTime? to = null)
    {
        var start = (from ?? settings.ReportFrom).Date;
        var end = (to ?? settings.ReportTo).Date;
        if (start > end) return Array.Empty<PendingJustification>();
        var rows = BuildRows(Load(settings, start, end), DateTime.Now);
        var result = new List<PendingJustification>();
        foreach (var row in rows)
        {
            if (employeeId is not null && !row.EmpleadoId.Equals(employeeId, StringComparison.OrdinalIgnoreCase)) continue;
            var pendingSegments = row.Segments.Where(s => s.UnjustifiedMinutes > 0).ToList();
            if (pendingSegments.Count > 0)
            {
                var multi = row.Segments.Count > 1;
                foreach (var s in pendingSegments)
                {
                    var label = multi ? $"Tramo {s.Number} ({s.ExpectedEntry:hh\\:mm}–{s.ExpectedExit:hh\\:mm})" : "Día completo";
                    result.Add(new PendingJustification(row.EmpleadoId, row.Nombre, row.Fecha, multi ? s.Number : null, label, s.UnjustifiedMinutes, row.Estado));
                }
            }
        }
        return result;
    }

    internal static List<DailyRow> BuildRows(ReportInputs inputs, DateTime generatedAt)
    {
        var from = inputs.From.Date;
        var to = inputs.To.Date;
        if (from > to) throw new InvalidOperationException("La fecha inicial no puede ser posterior a la fecha final.");

        var employees = inputs.Employees;
        var schedules = inputs.Schedules;
        var workdayTypes = inputs.WorkdayTypes;
        var incidents = inputs.Incidents;
        var punches = inputs.Punches;
        var firstPunchDates = inputs.FirstPunchDates;
        var holidays = inputs.Holidays;
        var toleranceMinutes = inputs.ToleranceMinutes;
        var details = new List<DailyRow>();

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            foreach (var employee in employees)
            {
                var configured = schedules.TryGetValue(employee.Key, out var saved)
                    ? saved
                    : new EmployeeSchedule(employee.Key, true, true, true, true, true, false, false, false, new TimeSpan(8, 30, 0), new TimeSpan(17, 0, 0), TimeSpan.Zero, TimeSpan.Zero, "continua");
                var schedule = ResolveSchedule(configured, workdayTypes);
                var workdayType = ResolveWorkdayType(configured, workdayTypes);
                var definitions = workdayType?.Segments ?? LegacySegments(schedule);
                var lunch = workdayType?.Lunch;
                var dayIncidents = incidents
                    .Where(i => i.EmployeeId.Equals(employee.Key, StringComparison.OrdinalIgnoreCase) && i.Date.Date == day)
                    .ToList();
                var fullDayIncident = dayIncidents.FirstOrDefault(i => i.Segment is null);
                var fullDayPermissionRemaining = fullDayIncident?.JustifiesPermission == true
                    ? (fullDayIncident.PermissionMinutes > 0 ? fullDayIncident.PermissionMinutes : int.MaxValue)
                    : 0;
                var rawMarks = punches.Where(p => p.EmployeeId.Equals(employee.Key, StringComparison.OrdinalIgnoreCase) && p.Timestamp.Date == day).OrderBy(p => p.Timestamp).ToList();
                var marks = ValidMarks(rawMarks, int.MaxValue);
                var expectedSegments = definitions.Select(x => Expected(day, x)).ToList();
                var buckets = AssignMarks(marks, expectedSegments);

                var employmentStarted = firstPunchDates.TryGetValue(employee.Key, out var firstPunchDate) && day.Date >= firstPunchDate;
                var holiday = holidays.Where(x => x.Date == day.Date && (x.EmployeeId is null || x.EmployeeId.Equals(employee.Key, StringComparison.OrdinalIgnoreCase))).OrderBy(x => x.EmployeeId is null ? 1 : 0).FirstOrDefault();
                var holidayBlocksWork = holiday is not null && !holiday.CountsAsWorkday;
                var workingDay = employmentStarted && schedule.WorksOn(day.DayOfWeek) && !holidayBlocksWork;
                var dayClosed = day.Date < generatedAt.Date;
                var isToday = day.Date == generatedAt.Date;
                var segmentRows = new List<SegmentAttendance>();
                LunchAttendance? lunchRow = null;
                var late = 0;
                var early = 0;
                var worked = 0;
                var expectedForReport = 0;
                var absenceMinutes = 0;
                var justifiedAbsence = 0;
                var unjustifiedAbsence = 0;
                var workedMorning = 0;
                var workedAfternoon = 0;
                var expectedMorning = 0;
                var expectedAfternoon = 0;

                for (var index = 0; index < expectedSegments.Count; index++)
                {
                    var expectedSegment = expectedSegments[index];
                    var bucket = buckets[index];
                    var segmentIncident = dayIncidents.FirstOrDefault(i => i.Segment == index + 1) ?? fullDayIncident;
                    var lunchInside = lunch is not null && lunch.Start >= definitions[index].Entry && lunch.End <= definitions[index].Exit;
                    var permissionBreak = lunchInside || segmentIncident?.JustifiesPermission == true;
                    DateTime? actualEntry = bucket.ElementAtOrDefault(0)?.Timestamp;
                    DateTime? actualExit = bucket.ElementAtOrDefault(1)?.Timestamp;
                    DateTime? breakExit = null;
                    DateTime? breakReturn = null;
                    if (lunchInside)
                    {
                        if (bucket.Count >= 4) { actualExit = bucket[^1].Timestamp; breakExit = bucket[1].Timestamp; breakReturn = bucket[^2].Timestamp; }
                        else if (bucket.Count == 3) { actualExit = null; breakExit = bucket[1].Timestamp; breakReturn = bucket[2].Timestamp; }
                        else if (bucket.Count == 2 && isToday && generatedAt < expectedSegment.Exit && bucket[1].Timestamp.TimeOfDay <= lunch!.End.Add(TimeSpan.FromMinutes(toleranceMinutes))) { actualExit = null; breakExit = bucket[1].Timestamp; }
                    }
                    else if (permissionBreak && bucket.Count >= 3)
                    {
                        actualExit = bucket[^1].Timestamp; breakExit = bucket[1].Timestamp; if (bucket.Count >= 4) breakReturn = bucket[^2].Timestamp;
                    }
                    if (lunchInside) lunchRow = new LunchAttendance(lunch!.Start, lunch.End, breakExit, breakReturn);

                    var laterMarkExists = buckets.Skip(index + 1).Any(x => x.Count > 0);
                    var closed = employmentStarted && (dayClosed || isToday && (generatedAt >= expectedSegment.Exit || actualExit.HasValue || laterMarkExists));
                    var lunchWasTaken = lunchInside && breakExit.HasValue && breakReturn.HasValue;
                    var scheduledLunch = lunchInside ? lunch : null;
                    var scheduledMinutes = workingDay && closed ? ScheduledMinutes(expectedSegment.Entry, expectedSegment.Exit, day, scheduledLunch) : 0;
                    var periodStarted = workingDay && (closed || isToday && generatedAt >= expectedSegment.Entry);
                    var plannedMinutes = periodStarted ? ScheduledMinutes(expectedSegment.Entry, expectedSegment.Exit, day, scheduledLunch) : 0;
                    var segmentWorked = closed && actualEntry.HasValue && actualExit.HasValue ? Math.Max(0, (int)(actualExit.Value - actualEntry.Value).TotalMinutes) : 0;
                    if (segmentWorked > 0 && breakExit.HasValue && breakReturn.HasValue) segmentWorked = Math.Max(0, segmentWorked - (int)(breakReturn.Value - breakExit.Value).TotalMinutes);

                    var (coveredStart, coveredEnd) = CoveredInterval(actualEntry, actualExit, expectedSegment.Entry, expectedSegment.Exit, toleranceMinutes);
                    int segExpMorning;
                    int segExpAfternoon;
                    int segCoveredMorning;
                    int segCoveredAfternoon;

                    if (definitions.Count >= 2)
                    {
                        // En jornadas discontinuas el tramo define el periodo: el primero es
                        // mañana y los siguientes son tarde. No se vuelve a cortar a las 12:00.
                        var coveredMinutes = workingDay && closed && coveredEnd > coveredStart
                            ? Math.Max(0, (int)(coveredEnd - coveredStart).TotalMinutes)
                            : 0;
                        if (coveredMinutes > 0 && breakExit.HasValue && breakReturn.HasValue)
                            coveredMinutes = Math.Max(0, coveredMinutes - IntervalOverlap(coveredStart, coveredEnd, breakExit.Value, breakReturn.Value));

                        segExpMorning = index == 0 ? plannedMinutes : 0;
                        segExpAfternoon = index == 0 ? 0 : plannedMinutes;
                        segCoveredMorning = index == 0 ? coveredMinutes : 0;
                        segCoveredAfternoon = index == 0 ? 0 : coveredMinutes;
                    }
                    else
                    {
                        var splitDivider = scheduledLunch?.Start ?? new TimeSpan(12, 0, 0);
                        DateTime? expectedLunchStart = scheduledLunch is null ? null : day.Add(scheduledLunch.Start);
                        DateTime? expectedLunchEnd = scheduledLunch is null ? null : day.Add(scheduledLunch.End);
                        var coveredBreakStart = lunchWasTaken ? breakExit : expectedLunchStart;
                        var coveredBreakEnd = lunchWasTaken ? breakReturn : expectedLunchEnd;
                        (segExpMorning, segExpAfternoon) = periodStarted
                            ? SplitInterval(expectedSegment.Entry, expectedSegment.Exit, day, splitDivider, expectedLunchStart, expectedLunchEnd)
                            : (0, 0);
                        (segCoveredMorning, segCoveredAfternoon) = workingDay && closed && coveredEnd > coveredStart
                            ? SplitInterval(coveredStart, coveredEnd, day, splitDivider, coveredBreakStart, coveredBreakEnd)
                            : (0, 0);
                    }
                    var covered = segCoveredMorning + segCoveredAfternoon;
                    var missing = Math.Max(0, scheduledMinutes - covered);

                    int justifiedPortion;
                    if (segmentIncident?.JustifiesAbsence == true) justifiedPortion = missing;
                    else if (segmentIncident?.JustifiesPermission == true)
                    {
                        if (segmentIncident.Segment is null)
                        {
                            justifiedPortion = Math.Min(missing, fullDayPermissionRemaining);
                            fullDayPermissionRemaining -= justifiedPortion;
                        }
                        else
                        {
                            var segmentPermission = segmentIncident.PermissionMinutes > 0 ? segmentIncident.PermissionMinutes : int.MaxValue;
                            justifiedPortion = Math.Min(missing, segmentPermission);
                        }
                    }
                    else justifiedPortion = 0;
                    var unjustifiedPortion = missing - justifiedPortion;

                    // Neutralización: quitar las horas justificadas también de lo esperado (reparto proporcional mañana/tarde).
                    if (justifiedPortion > 0)
                    {
                        var expTotal = segExpMorning + segExpAfternoon;
                        var reported = Math.Max(0, expTotal - justifiedPortion);
                        var newMorning = expTotal == 0 ? 0 : (int)Math.Round(reported * (double)segExpMorning / expTotal);
                        segExpMorning = newMorning;
                        segExpAfternoon = reported - newMorning;
                    }

                    if (actualEntry.HasValue && segmentIncident?.JustifiesLateness != true) late += Math.Max(0, (int)Math.Ceiling((actualEntry.Value - expectedSegment.Entry.AddMinutes(toleranceMinutes)).TotalMinutes));
                    if (actualExit.HasValue) early += Math.Max(0, (int)(expectedSegment.Exit - actualExit.Value).TotalMinutes);
                    worked += segmentWorked;
                    expectedForReport += Math.Max(0, scheduledMinutes - justifiedPortion);
                    absenceMinutes += missing;
                    justifiedAbsence += justifiedPortion;
                    unjustifiedAbsence += unjustifiedPortion;
                    workedMorning += segCoveredMorning;
                    workedAfternoon += segCoveredAfternoon;
                    expectedMorning += segExpMorning;
                    expectedAfternoon += segExpAfternoon;
                    segmentRows.Add(new SegmentAttendance(index + 1, definitions[index].Entry, definitions[index].Exit, actualEntry, actualExit, segmentWorked, scheduledMinutes, closed, segCoveredMorning, segCoveredAfternoon, segExpMorning, segExpAfternoon, justifiedPortion, unjustifiedPortion));
                }

                if (lunch is not null && lunchRow is null) lunchRow = new LunchAttendance(lunch.Start, lunch.End, null, null);
                var hasAnyMark = segmentRows.Any(x => x.ActualEntry.HasValue || x.ActualExit.HasValue);
                var allFinished = segmentRows.All(x => x.Closed);
                var allComplete = segmentRows.Where(x => x.Closed).All(x => x.ActualEntry.HasValue && x.ActualExit.HasValue) && (allFinished || !dayClosed);
                var extra = Math.Max(0, worked - expectedForReport);
                var descanso = definitions.Count >= 2 ? BreakLabel(definitions) : null;
                var representativeIncident = dayIncidents.FirstOrDefault(i => i.JustifiesAbsence)
                    ?? dayIncidents.FirstOrDefault(i => i.JustifiesPermission)
                    ?? dayIncidents.FirstOrDefault();
                var state = State(employmentStarted, workingDay, holiday, holidayBlocksWork, representativeIncident, dayClosed, allFinished, allComplete, hasAnyMark, late, early, justifiedAbsence, unjustifiedAbsence);
                var scheduleText = string.Join(" / ", definitions.Select(x => $"{x.Entry:hh\\:mm}–{x.Exit:hh\\:mm}"));
                var scheduleType = definitions.Count == 1 ? "1 tramo" : $"{definitions.Count} tramos";
                var first = segmentRows.ElementAtOrDefault(0);
                var second = segmentRows.ElementAtOrDefault(1);
                details.Add(new DailyRow(
                    employee.Key,
                    employee.Value,
                    day,
                    first?.ActualEntry,
                    first?.ActualExit,
                    second?.ActualEntry,
                    second?.ActualExit,
                    worked,
                    expectedForReport,
                    late,
                    early,
                    absenceMinutes,
                    extra,
                    state,
                    scheduleType,
                    scheduleText,
                    allComplete && allFinished,
                    justifiedAbsence,
                    unjustifiedAbsence,
                    segmentRows,
                    lunchRow,
                    workedMorning,
                    workedAfternoon,
                    expectedMorning,
                    expectedAfternoon,
                    descanso));
            }
        }

        return details;
    }

    static string State(bool employmentStarted, bool workingDay, Holiday? holiday, bool holidayBlocksWork, DailyIncident? incident, bool dayClosed, bool allFinished, bool allComplete, bool hasAnyMark, int late, int early, int justifiedAbsence, int unjustifiedAbsence)
    {
        if (!employmentStarted) return "Antes del primer marcado";
        if (holidayBlocksWork) return $"Festivo ({holiday!.Description})";
        if (!workingDay) return hasAnyMark ? "Trabajo fuera de jornada" : "No laborable";
        if (!dayClosed && !allFinished) return hasAnyMark ? "En curso" : "Pendiente";
        if (!hasAnyMark && unjustifiedAbsence == 0 && justifiedAbsence > 0) return $"Ausencia justificada ({incident?.Type ?? "justificada"})";
        if (!hasAnyMark) return "Ausencia sin justificación";
        if (!allComplete) return unjustifiedAbsence == 0 && justifiedAbsence > 0 ? "Incompleto (justificado)" : "Incompleto";
        if (late > 0 && early > 0) return "Tarde y salida anticipada";
        if (late > 0) return "Tarde";
        if (early > 0) return "Salida anticipada";
        if (incident is not null) return $"Completo ({incident.Type})";
        if (holiday is not null) return "Completo (Festivo trabajado)";
        return "Completo";
    }

    static IReadOnlyList<WorkSegment> LegacySegments(EmployeeSchedule schedule) => schedule.Discontinuous
        ? new[] { new WorkSegment(schedule.Entry, schedule.Exit), new WorkSegment(schedule.SecondEntry, schedule.SecondExit) }
        : new[] { new WorkSegment(schedule.Entry, schedule.Exit) };

    internal static ExpectedSegment Expected(DateTime day, WorkSegment segment)
    {
        var entry = day.Add(segment.Entry); var exit = day.Add(segment.Exit); if (exit <= entry) exit = exit.AddDays(1); return new ExpectedSegment(entry, exit);
    }

    internal static List<List<CsvPunch>> AssignMarks(IReadOnlyList<CsvPunch> marks, IReadOnlyList<ExpectedSegment> segments)
    {
        var result = segments.Select(_ => new List<CsvPunch>()).ToList();
        if (segments.Count == 0) return result;
        var boundaries = new List<DateTime>();
        for (var index = 0; index < segments.Count - 1; index++) boundaries.Add(segments[index].Exit + TimeSpan.FromTicks((segments[index + 1].Entry - segments[index].Exit).Ticks / 2));
        foreach (var mark in marks)
        {
            var index = 0; while (index < boundaries.Count && mark.Timestamp >= boundaries[index]) index++; result[index].Add(mark);
        }
        return result;
    }

    static int ScheduledMinutes(DateTime expectedEntry, DateTime expectedExit, DateTime day, LunchWindow? lunch)
    {
        var minutes = Math.Max(0, (int)(expectedExit - expectedEntry).TotalMinutes);
        return Math.Max(0, minutes - LunchOverlap(expectedEntry, expectedExit, day, lunch));
    }

    static int LunchOverlap(DateTime start, DateTime end, DateTime day, LunchWindow? lunch)
    {
        if (lunch is null) return 0; var lunchStart = day.Add(lunch.Start); var lunchEnd = day.Add(lunch.End); var overlapStart = start > lunchStart ? start : lunchStart; var overlapEnd = end < lunchEnd ? end : lunchEnd; return overlapEnd > overlapStart ? (int)(overlapEnd - overlapStart).TotalMinutes : 0;
    }

    static int IntervalOverlap(DateTime firstStart, DateTime firstEnd, DateTime secondStart, DateTime secondEnd)
    {
        var start = firstStart > secondStart ? firstStart : secondStart; var end = firstEnd < secondEnd ? firstEnd : secondEnd; return end > start ? (int)(end - start).TotalMinutes : 0;
    }

    /// <summary>Reparte [start, end] en minutos antes/después del divisor del día (hora), restando de cada lado el solape con el intervalo excluido (almuerzo/descanso).</summary>
    internal static (int Morning, int Afternoon) SplitInterval(DateTime start, DateTime end, DateTime day, TimeSpan divider, DateTime? excludeStart, DateTime? excludeEnd)
    {
        if (end <= start) return (0, 0);
        var mid = day.Add(divider);
        if (mid < start) mid = start; else if (mid > end) mid = end;
        var morning = (int)(mid - start).TotalMinutes;
        var afternoon = (int)(end - mid).TotalMinutes;
        if (excludeStart.HasValue && excludeEnd.HasValue)
        {
            morning -= IntervalOverlap(start, mid, excludeStart.Value, excludeEnd.Value);
            afternoon -= IntervalOverlap(mid, end, excludeStart.Value, excludeEnd.Value);
        }
        return (Math.Max(0, morning), Math.Max(0, afternoon));
    }

    static (DateTime Start, DateTime End) CoveredInterval(DateTime? actualEntry, DateTime? actualExit, DateTime expectedEntry, DateTime expectedExit, int toleranceMinutes)
    {
        if (!actualEntry.HasValue || !actualExit.HasValue) return (expectedEntry, expectedEntry);
        var toleratedEntry = actualEntry.Value <= expectedEntry.AddMinutes(toleranceMinutes) ? expectedEntry : actualEntry.Value;
        var start = toleratedEntry > expectedEntry ? toleratedEntry : expectedEntry;
        var end = actualExit.Value < expectedExit ? actualExit.Value : expectedExit;
        return (start, end < start ? start : end);
    }

    internal sealed record ExpectedSegment(DateTime Entry, DateTime Exit);
}
