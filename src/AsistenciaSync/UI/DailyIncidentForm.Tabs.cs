using AsistenciaSync.Models;
using AsistenciaSync.Services;

namespace AsistenciaSync.UI;

internal sealed partial class DailyIncidentForm
{
    static readonly string[] IncidentTypes = { "Enfermedad", "Inconveniente", "Permiso", "Otra" };

    // ── Tab "Sin justificar" ─────────────────────────────────────────────
    readonly DataGridView pendingGrid = MakeGrid();
    readonly ComboBox pendingType = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    readonly TextBox pendingReason = new() { Width = 460 };

    // ── Tab "Programar falta futura" ─────────────────────────────────────
    readonly DateTimePicker futureDate = new() { Format = DateTimePickerFormat.Short, Width = 160 };
    readonly ComboBox futureSegment = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    readonly ComboBox futureType = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    readonly TextBox futureReason = new() { Width = 460 };

    // ── Tab "Registradas" ───────────────────────────────────────────────
    readonly DataGridView registeredGrid = MakeGrid();

    TabPage BuildPendingTab()
    {
        var page = new TabPage("Sin justificar") { BackColor = Color.White, Padding = new Padding(12) };

        pendingGrid.Columns.Add("Fecha", "Fecha");
        pendingGrid.Columns.Add("Tramo", "Tramo");
        pendingGrid.Columns.Add("Estado", "Estado");
        pendingGrid.Columns.Add("Horas", "Horas esperadas");
        var gridHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, 8) };
        gridHost.Controls.Add(pendingGrid);

        pendingType.Items.AddRange(IncidentTypes); pendingType.SelectedIndex = 0;

        var form = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 2, Padding = new Padding(16, 12, 16, 12), BackColor = Color.FromArgb(247, 249, 252) };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(form, "", SectionLabel("JUSTIFICAR LA INCIDENCIA SELECCIONADA"));
        AddRow(form, "Tipo", pendingType);
        AddRow(form, "Motivo / observación", pendingReason);
        var justify = MakeButton("Justificar fila seleccionada", Primary);
        justify.Margin = new Padding(0, 6, 0, 0);
        justify.Click += (_, _) => JustifySelected();
        AddRow(form, "", justify);

        var layout = new Panel { Dock = DockStyle.Fill };
        layout.Controls.Add(gridHost);
        layout.Controls.Add(form);
        page.Controls.Add(layout);
        return page;
    }

    TabPage BuildFutureTab()
    {
        var page = new TabPage("Programar falta futura") { BackColor = Color.White, Padding = new Padding(12) };
        futureDate.MinDate = DateTime.Today; futureDate.Value = DateTime.Today.AddDays(1);
        futureType.Items.AddRange(IncidentTypes); futureType.SelectedIndex = 0;

        var form = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(18), BackColor = Color.FromArgb(247, 249, 252) };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(form, "", SectionLabel("PROGRAMAR UNA JUSTIFICACIÓN FUTURA"));
        AddRow(form, "Fecha", futureDate);
        AddRow(form, "Tramo", futureSegment);
        AddRow(form, "Tipo", futureType);
        AddRow(form, "Motivo / observación", futureReason);
        var schedule = MakeButton("Programar", Primary);
        schedule.Margin = new Padding(0, 6, 0, 0);
        schedule.Click += (_, _) => ScheduleFuture();
        AddRow(form, "", schedule);

        AddRow(form, "", new Label { Text = "El reporte la tomará en cuenta automáticamente cuando la fecha entre en el periodo consultado.", AutoSize = true, ForeColor = Muted, Font = new Font("Segoe UI", 8) });
        page.Controls.Add(form);
        return page;
    }

    TabPage BuildRegisteredTab()
    {
        var page = new TabPage("Registradas") { BackColor = Color.White, Padding = new Padding(12) };
        registeredGrid.Columns.Add("Fecha", "Fecha");
        registeredGrid.Columns.Add("Tramo", "Tramo");
        registeredGrid.Columns.Add("Tipo", "Tipo");
        registeredGrid.Columns.Add("Motivo", "Motivo");
        var gridHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, 8) };
        gridHost.Controls.Add(registeredGrid);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
        var remove = MakeButton("Quitar fila seleccionada", Color.FromArgb(170, 75, 75));
        remove.Margin = new Padding(0);
        remove.Click += (_, _) => RemoveSelected();
        bar.Controls.Add(remove);
        var layout = new Panel { Dock = DockStyle.Fill };
        layout.Controls.Add(gridHost);
        layout.Controls.Add(bar);
        page.Controls.Add(layout);
        return page;
    }

    void RefreshSegmentChoices()
    {
        futureSegment.Items.Clear();
        futureSegment.Items.Add("Día completo");
        if (Selected is { } option)
            for (var i = 1; i <= SegmentCount(option.Id); i++) futureSegment.Items.Add($"Tramo {i}");
        futureSegment.SelectedIndex = 0;
    }

    void LoadPending()
    {
        pendingGrid.Rows.Clear();
        if (Selected is not { } option) return;
        foreach (var item in ReportService.Pending(settings, option.Id, periodFrom, periodTo))
        {
            var index = pendingGrid.Rows.Add(item.Date.ToString("dd/MM/yyyy"), item.SegmentLabel, item.State, Hours(item.ExpectedMinutes));
            pendingGrid.Rows[index].Tag = item;
        }
    }

    void LoadRegistered()
    {
        registeredGrid.Rows.Clear();
        if (Selected is not { } option) return;
        var incidents = AttendanceConfigurationStore.ReadIncidents(settings, DateTime.Today.AddYears(-1), DateTime.Today.AddYears(1))
            .Where(x => x.EmployeeId.Equals(option.Id, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Date);
        foreach (var incident in incidents)
        {
            var index = registeredGrid.Rows.Add(incident.Date.ToString("dd/MM/yyyy"), SegmentLabel(incident.Segment), incident.Type, incident.Reason);
            registeredGrid.Rows[index].Tag = incident;
        }
    }

    void JustifySelected()
    {
        try
        {
            if (Selected is not { } option) throw new InvalidOperationException("Seleccione un empleado.");
            if (pendingGrid.CurrentRow?.Tag is not PendingJustification pending) throw new InvalidOperationException("Seleccione una fila de la lista.");
            AttendanceConfigurationStore.SaveIncident(settings, option.Id, pending.Date, pendingType.Text, pendingReason.Text.Trim(),
                absence: true, lateness: false, permission: false, permissionHours: 0, segment: pending.Segment);
            pendingReason.Clear();
            ReloadForEmployee();
            MessageBox.Show(this, "Justificación aplicada. El reporte la tomará en cuenta.", "Justificaciones / Faltas", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "No se pudo guardar", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    void ScheduleFuture()
    {
        try
        {
            if (Selected is not { } option) throw new InvalidOperationException("Seleccione un empleado.");
            int? segment = futureSegment.SelectedIndex <= 0 ? null : futureSegment.SelectedIndex;
            AttendanceConfigurationStore.SaveIncident(settings, option.Id, futureDate.Value.Date, futureType.Text, futureReason.Text.Trim(),
                absence: true, lateness: false, permission: false, permissionHours: 0, segment: segment);
            futureReason.Clear();
            ReloadForEmployee();
            tabs.SelectedIndex = 2;
            MessageBox.Show(this, $"Falta programada para el {futureDate.Value:dd/MM/yyyy}.", "Justificaciones / Faltas", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "No se pudo programar", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    void RemoveSelected()
    {
        if (registeredGrid.CurrentRow?.Tag is not DailyIncident incident) return;
        if (MessageBox.Show(this, $"¿Quitar la incidencia del {incident.Date:dd/MM/yyyy} ({SegmentLabel(incident.Segment)})?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        AttendanceConfigurationStore.DeleteIncident(settings, incident.EmployeeId, incident.Date, incident.Segment);
        ReloadForEmployee();
    }

    static void AddRow(TableLayoutPanel panel, string label, Control control)
    {
        var row = panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label { Text = label, AutoSize = true, ForeColor = Ink, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 8, 0) }, 0, row);
        control.Margin = new Padding(0, 5, 0, 5);
        panel.Controls.Add(control, 1, row);
    }
}
