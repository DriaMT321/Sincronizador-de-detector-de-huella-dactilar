namespace AsistenciaSync.Tests.Reporting;

public class AbsenceRulesTests
{
    static readonly DateTime Monday = new(2026, 1, 5);
    static readonly DateTime GeneratedAt = new(2026, 2, 1, 9, 0, 0);

    [Fact]
    public void Ausencia_justificada_completa_neutraliza_el_dia()
    {
        var row = new ReportScenario { Entry = new(8, 0, 0), Exit = new(17, 0, 0) }
            .Incident(Monday, "Enfermedad", absence: true)
            .Row(Monday, GeneratedAt);

        Assert.Equal(0, row.Esperadas);                 // sin deuda
        Assert.Equal(540, row.AusenciaJustificada);     // solo registro
        Assert.Equal(0, row.AusenciaSinJustificar);
        Assert.Equal("Ausencia justificada (Enfermedad)", row.Estado);
    }

    [Fact]
    public void Salida_temprana_sin_justificacion_resta()
    {
        var row = new ReportScenario { Entry = new(8, 0, 0), Exit = new(17, 0, 0) }
            .Punch(Monday, new(8, 0, 0)).Punch(Monday, new(16, 0, 0))
            .Row(Monday, GeneratedAt);

        Assert.Equal(60, row.AusenciaSinJustificar);
        Assert.Equal(0, row.AusenciaJustificada);
        Assert.Equal(540, row.Esperadas);
    }

    [Fact]
    public void Permiso_parcial_justifica_solo_los_minutos_del_permiso()
    {
        var row = new ReportScenario { Entry = new(8, 0, 0), Exit = new(17, 0, 0) }
            .Punch(Monday, new(8, 0, 0)).Punch(Monday, new(14, 0, 0))
            .Incident(Monday, "Permiso", permission: true, permissionMinutes: 120)
            .Row(Monday, GeneratedAt);

        Assert.Equal(120, row.AusenciaJustificada);
        Assert.Equal(60, row.AusenciaSinJustificar);
    }

    [Fact]
    public void Incidencia_por_tramo_solo_neutraliza_ese_tramo()
    {
        var row = new ReportScenario { Entry = new(8, 0, 0), Exit = new(12, 0, 0), SecondEntry = new(14, 0, 0), SecondExit = new(18, 0, 0) }
            .Punch(Monday, new(14, 0, 0)).Punch(Monday, new(18, 0, 0))
            .Incident(Monday, "Enfermedad", absence: true, segment: 1)
            .Row(Monday, GeneratedAt);

        Assert.Equal(240, row.AusenciaJustificada);   // tramo 1
        Assert.Equal(0, row.AusenciaSinJustificar);   // tramo 2 cubierto
    }

    [Fact]
    public void Dos_incidencias_del_mismo_dia_se_aplican_a_sus_tramos_correspondientes()
    {
        var row = new ReportScenario { Entry = new(8, 30, 0), Exit = new(12, 30, 0), SecondEntry = new(14, 30, 0), SecondExit = new(18, 0, 0) }
            .Incident(Monday, "Permiso mañana", absence: true, segment: 1)
            .Incident(Monday, "Permiso tarde", absence: true, segment: 2)
            .Row(Monday, GeneratedAt);

        Assert.Equal(450, row.AusenciaJustificada);
        Assert.Equal(0, row.AusenciaSinJustificar);
        Assert.Equal(0, row.Esperadas);
    }
}
