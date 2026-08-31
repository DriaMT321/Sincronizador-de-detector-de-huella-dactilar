namespace AsistenciaSync.Models;

public sealed record EmployeeSchedule(string EmployeeId, bool Monday, bool Tuesday, bool Wednesday, bool Thursday, bool Friday, bool Saturday, bool Sunday, bool Discontinuous, TimeSpan Entry, TimeSpan Exit, TimeSpan SecondEntry, TimeSpan SecondExit, string WorkdayTypeId = "")
{
    public bool WorksOn(DayOfWeek day) => day switch { DayOfWeek.Monday => Monday, DayOfWeek.Tuesday => Tuesday, DayOfWeek.Wednesday => Wednesday, DayOfWeek.Thursday => Thursday, DayOfWeek.Friday => Friday, DayOfWeek.Saturday => Saturday, _ => Sunday };
}
