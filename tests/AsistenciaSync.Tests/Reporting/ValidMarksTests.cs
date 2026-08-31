using AsistenciaSync.Services;

namespace AsistenciaSync.Tests.Reporting;

public class ValidMarksTests
{
    static readonly DateTime Day = new(2026, 1, 5);

    static CsvPunch At(TimeSpan time) => new("1", "Ana", Day.Add(time), "in", "device");

    [Fact]
    public void Marcas_dentro_de_30_min_se_consideran_repeticion_accidental()
    {
        var marks = new List<CsvPunch>
        {
            At(new TimeSpan(8, 0, 0)),
            At(new TimeSpan(8, 20, 0)),
        };

        var valid = ReportService.ValidMarks(marks, int.MaxValue);

        Assert.Single(valid);
        Assert.Equal(new TimeSpan(8, 0, 0), valid[0].Timestamp.TimeOfDay);
    }

    [Fact]
    public void Marca_despues_de_30_min_es_valida()
    {
        var marks = new List<CsvPunch>
        {
            At(new TimeSpan(8, 0, 0)),
            At(new TimeSpan(8, 31, 0)),
        };

        var valid = ReportService.ValidMarks(marks, int.MaxValue);

        Assert.Equal(2, valid.Count);
    }

    [Fact]
    public void Respeta_el_maximo_de_marcas_solicitado()
    {
        var marks = new List<CsvPunch>
        {
            At(new TimeSpan(8, 0, 0)),
            At(new TimeSpan(9, 0, 0)),
            At(new TimeSpan(10, 0, 0)),
        };

        var valid = ReportService.ValidMarks(marks, 2);

        Assert.Equal(2, valid.Count);
    }
}
