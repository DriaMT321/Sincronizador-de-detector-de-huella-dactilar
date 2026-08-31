using AsistenciaSync.Models;
using AsistenciaSync.Services;

namespace AsistenciaSync.Tests.Reporting;

public class AssignMarksTests
{
    static readonly DateTime Day = new(2026, 1, 5);

    static CsvPunch At(TimeSpan time) => new("1", "Ana", Day.Add(time), "in", "device");

    [Fact]
    public void Reparte_cada_marca_en_el_tramo_mas_cercano()
    {
        var segments = new[]
        {
            ReportService.Expected(Day, new WorkSegment(new TimeSpan(8, 0, 0), new TimeSpan(12, 0, 0))),
            ReportService.Expected(Day, new WorkSegment(new TimeSpan(14, 0, 0), new TimeSpan(18, 0, 0))),
        };

        // Punto medio entre tramos = 13:00.
        var marks = new[]
        {
            At(new TimeSpan(8, 5, 0)),
            At(new TimeSpan(12, 30, 0)),
            At(new TimeSpan(13, 30, 0)),
            At(new TimeSpan(18, 2, 0)),
        };

        var buckets = ReportService.AssignMarks(marks, segments);

        Assert.Equal(2, buckets[0].Count);
        Assert.Equal(2, buckets[1].Count);
        Assert.All(buckets[0], m => Assert.True(m.Timestamp.TimeOfDay < new TimeSpan(13, 0, 0)));
        Assert.All(buckets[1], m => Assert.True(m.Timestamp.TimeOfDay > new TimeSpan(13, 0, 0)));
    }

    [Fact]
    public void Sin_tramos_no_hay_buckets()
    {
        var buckets = ReportService.AssignMarks(new[] { At(new TimeSpan(9, 0, 0)) }, Array.Empty<ReportService.ExpectedSegment>());

        Assert.Empty(buckets);
    }
}
