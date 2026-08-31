using AsistenciaSync.Models;
using AsistenciaSync.Services;

namespace AsistenciaSync.Tests.Reporting;

/// <summary>Constructor mínimo de <see cref="ReportInputs"/> para un solo empleado.</summary>
internal sealed class ReportScenario
{
    const string EmployeeId = "1";
    const string EmployeeName = "Ana";

    public TimeSpan Entry { get; set; } = new(8, 0, 0);
    public TimeSpan Exit { get; set; } = new(17, 0, 0);
    public TimeSpan? SecondEntry { get; set; }
    public TimeSpan? SecondExit { get; set; }
    public LunchWindow? Lunch { get; set; }
    public int ToleranceMinutes { get; set; } = 5;
    public DateTime EmploymentStart { get; set; } = new(2026, 1, 1);
    public bool WorksSaturday { get; set; }
    public bool WorksSunday { get; set; }

    public List<CsvPunch> Punches { get; } = new();
    public List<DailyIncident> Incidents { get; } = new();
    public List<Holiday> Holidays { get; } = new();

    bool DoubleShift => SecondEntry.HasValue && SecondExit.HasValue;

    public ReportScenario Punch(DateTime day, TimeSpan time)
    {
        Punches.Add(new CsvPunch(EmployeeId, EmployeeName, day.Date.Add(time), "in", "device"));
        return this;
    }

    public ReportScenario Incident(DateTime day, string type, bool absence = false, bool lateness = false, bool permission = false, int permissionMinutes = 0, int? segment = null)
    {
        Incidents.Add(new DailyIncident(Incidents.Count + 1, EmployeeId, day.Date, type, "prueba", absence, lateness, permission, permissionMinutes, segment));
        return this;
    }

    public ReportScenario Holiday(DateTime day, string description, bool countsAsWorkday = false)
    {
        Holidays.Add(new Holiday(day.Date, description, null, countsAsWorkday));
        return this;
    }

    public ReportInputs Build(DateTime day)
    {
        var segments = DoubleShift
            ? new[] { new WorkSegment(Entry, Exit), new WorkSegment(SecondEntry!.Value, SecondExit!.Value) }
            : new[] { new WorkSegment(Entry, Exit) };
        var typeId = DoubleShift ? "discontinua" : "continua";
        var workdayType = new WorkdayType(typeId, DoubleShift ? "Discontinua" : "Continua", Entry, Exit, SecondEntry ?? TimeSpan.Zero, SecondExit ?? TimeSpan.Zero, segments, Lunch);
        var schedule = new EmployeeSchedule(EmployeeId, true, true, true, true, true, WorksSaturday, WorksSunday, DoubleShift, Entry, Exit, SecondEntry ?? TimeSpan.Zero, SecondExit ?? TimeSpan.Zero, typeId);

        return new ReportInputs(
            day.Date,
            day.Date,
            new Dictionary<string, string> { [EmployeeId] = EmployeeName },
            new Dictionary<string, EmployeeSchedule> { [EmployeeId] = schedule },
            new[] { workdayType },
            Incidents,
            Punches,
            new Dictionary<string, DateTime> { [EmployeeId] = EmploymentStart },
            Holidays,
            ToleranceMinutes);
    }

    public DailyRow Row(DateTime day, DateTime generatedAt) => ReportService.BuildRows(Build(day), generatedAt).Single();
}
