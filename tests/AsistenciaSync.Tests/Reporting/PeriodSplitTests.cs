using AsistenciaSync.Models;

namespace AsistenciaSync.Tests.Reporting;

public class PeriodSplitTests
{
    static readonly DateTime Monday = new(2026, 1, 5);
    static readonly DateTime GeneratedAt = new(2026, 2, 1, 9, 0, 0);

    [Fact]
    public void Jornada_continua_con_almuerzo_reparte_manana_y_tarde_por_el_almuerzo()
    {
        var row = new ReportScenario { Entry = new(8, 0, 0), Exit = new(17, 0, 0), Lunch = new LunchWindow(new(12, 0, 0), new(13, 0, 0)) }
            .Punch(Monday, new(8, 0, 0)).Punch(Monday, new(12, 0, 0)).Punch(Monday, new(13, 0, 0)).Punch(Monday, new(17, 0, 0))
            .Row(Monday, GeneratedAt);

        Assert.Equal(240, row.TrabajadasManana);
        Assert.Equal(240, row.TrabajadasTarde);
    }

    [Fact]
    public void Jornada_continua_con_descanso_configurado_mantiene_ocho_horas_aunque_no_marque_descanso()
    {
        var row = new ReportScenario { Entry = new(8, 30, 0), Exit = new(17, 0, 0), Lunch = new LunchWindow(new(12, 30, 0), new(13, 0, 0)) }
            .Punch(Monday, new(8, 30, 0)).Punch(Monday, new(17, 0, 0))
            .Row(Monday, GeneratedAt);

        Assert.Equal(240, row.EsperadasManana);
        Assert.Equal(240, row.EsperadasTarde);
        Assert.Equal(480, row.Esperadas);
        Assert.Equal(240, row.TrabajadasManana);
        Assert.Equal(240, row.TrabajadasTarde);
        Assert.Equal(30, row.MinutosExtra);
    }

    [Fact]
    public void Jornada_continua_sin_almuerzo_corta_a_las_doce()
    {
        var row = new ReportScenario { Entry = new(8, 0, 0), Exit = new(17, 0, 0) }
            .Punch(Monday, new(8, 0, 0)).Punch(Monday, new(17, 0, 0))
            .Row(Monday, GeneratedAt);

        Assert.Equal(240, row.TrabajadasManana);   // 08:00–12:00
        Assert.Equal(300, row.TrabajadasTarde);    // 12:00–17:00
    }

    [Fact]
    public void Jornada_doble_asigna_tramo_uno_a_manana_y_tramo_dos_a_tarde_con_descanso()
    {
        var row = new ReportScenario { Entry = new(8, 0, 0), Exit = new(12, 0, 0), SecondEntry = new(14, 0, 0), SecondExit = new(18, 0, 0) }
            .Punch(Monday, new(8, 0, 0)).Punch(Monday, new(12, 0, 0)).Punch(Monday, new(14, 0, 0)).Punch(Monday, new(18, 0, 0))
            .Row(Monday, GeneratedAt);

        Assert.Equal(240, row.TrabajadasManana);
        Assert.Equal(240, row.TrabajadasTarde);
        Assert.Equal("12:00–14:00", row.Descanso);
    }

    [Fact]
    public void Jornada_discontinua_que_termina_despues_de_las_doce_no_parte_el_primer_tramo()
    {
        var row = new ReportScenario { Entry = new(8, 30, 0), Exit = new(12, 30, 0), SecondEntry = new(14, 30, 0), SecondExit = new(18, 0, 0) }
            .Punch(Monday, new(8, 30, 0)).Punch(Monday, new(12, 30, 0)).Punch(Monday, new(14, 30, 0)).Punch(Monday, new(18, 0, 0))
            .Row(Monday, GeneratedAt);

        Assert.Equal(240, row.TrabajadasManana);
        Assert.Equal(210, row.TrabajadasTarde);
        Assert.Equal(240, row.EsperadasManana);
        Assert.Equal(210, row.EsperadasTarde);
        Assert.Equal("12:30–14:30", row.Descanso);
    }

    [Fact]
    public void Tramo_de_tarde_en_curso_muestra_su_horario_sin_generar_ausencia_anticipada()
    {
        var generatedAt = Monday.AddHours(17);
        var row = new ReportScenario { Entry = new(8, 30, 0), Exit = new(12, 30, 0), SecondEntry = new(14, 30, 0), SecondExit = new(18, 0, 0) }
            .Row(Monday, generatedAt);

        Assert.Equal(240, row.Esperadas);              // solo el tramo de mañana ya es exigible
        Assert.Equal(240, row.EsperadasManana);
        Assert.Equal(210, row.EsperadasTarde);         // el horario de tarde ya se muestra
        Assert.Equal(240, row.AusenciaSinJustificar);  // no penaliza todavía el tramo en curso
    }
}
