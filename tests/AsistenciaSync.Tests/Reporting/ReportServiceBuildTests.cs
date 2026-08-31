using AsistenciaSync.Services;

namespace AsistenciaSync.Tests.Reporting;

public class ReportServiceBuildTests
{
    // 2026-01-05 es lunes; 2026-01-04 es domingo. El "hoy" se fija después del periodo
    // para que el día quede cerrado (dayClosed == true).
    static readonly DateTime Monday = new(2026, 1, 5);
    static readonly DateTime Sunday = new(2026, 1, 4);
    static readonly DateTime GeneratedAt = new(2026, 2, 1, 9, 0, 0);

    [Fact]
    public void Entrada_y_salida_en_horario_marca_Completo()
    {
        var row = new ReportScenario()
            .Punch(Monday, new TimeSpan(8, 0, 0))
            .Punch(Monday, new TimeSpan(17, 0, 0))
            .Row(Monday, GeneratedAt);

        Assert.Equal("Completo", row.Estado);
        Assert.Equal(0, row.MinutosTarde);
    }

    [Fact]
    public void Entrada_pasada_la_tolerancia_marca_Tarde()
    {
        var row = new ReportScenario()
            .Punch(Monday, new TimeSpan(8, 30, 0))
            .Punch(Monday, new TimeSpan(17, 0, 0))
            .Row(Monday, GeneratedAt);

        Assert.Equal("Tarde", row.Estado);
        Assert.True(row.MinutosTarde > 0);
    }

    [Fact]
    public void Dia_laborable_cerrado_sin_marcas_es_ausencia_sin_justificacion()
    {
        var row = new ReportScenario().Row(Monday, GeneratedAt);

        Assert.Equal("Ausencia sin justificación", row.Estado);
    }

    [Fact]
    public void Ausencia_con_incidencia_justificada_se_reporta_como_justificada()
    {
        var row = new ReportScenario()
            .Incident(Monday, "Enfermedad", absence: true)
            .Row(Monday, GeneratedAt);

        Assert.Equal("Ausencia justificada (Enfermedad)", row.Estado);
    }

    [Fact]
    public void Dia_no_laborable_segun_horario_marca_No_laborable()
    {
        var row = new ReportScenario().Row(Sunday, GeneratedAt);

        Assert.Equal("No laborable", row.Estado);
    }

    [Fact]
    public void Feriado_que_no_cuenta_como_jornada_marca_Festivo()
    {
        var row = new ReportScenario()
            .Holiday(Monday, "Año Nuevo")
            .Row(Monday, GeneratedAt);

        Assert.Equal("Festivo (Año Nuevo)", row.Estado);
    }

    [Fact]
    public void Fecha_inicial_posterior_a_la_final_lanza()
    {
        var inputs = new ReportScenario().Build(Monday) with { From = Monday, To = Monday.AddDays(-1) };

        Assert.Throws<InvalidOperationException>(() => ReportService.BuildRows(inputs, GeneratedAt));
    }
}
