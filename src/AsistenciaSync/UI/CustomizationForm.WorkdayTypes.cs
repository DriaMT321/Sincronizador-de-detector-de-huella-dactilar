using AsistenciaSync.Models;
using AsistenciaSync.Services;

namespace AsistenciaSync.UI;

internal sealed partial class CustomizationForm
{
    TabPage WorkdayTypesPage()
    {
        var page = new TabPage("Tipo de jornada") { Padding = new Padding(18) }; workdayTypes.Columns.Add("Id", "ID"); workdayTypes.Columns.Add("Nombre", "Nombre"); workdayTypes.Columns.Add("Tipo", "Tipo"); workdayTypes.Columns.Add("Horario", "Horario"); workdayTypes.Dock = DockStyle.Fill;
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, Padding = new Padding(0, 8, 0, 0) }; var add = Button("Añadir jornada", Color.FromArgb(80, 145, 112)); add.Click += (_, _) => EditWorkdayType(null); var edit = Button("Editar jornada", Color.FromArgb(35, 91, 151)); edit.Click += (_, _) => EditWorkdayType(SelectedWorkdayType()); var remove = Button("Eliminar jornada", Color.FromArgb(170, 75, 75)); remove.Click += (_, _) => DeleteWorkdayType(); bar.Controls.AddRange(new Control[] { add, edit, remove }); page.Controls.Add(workdayTypes); page.Controls.Add(bar); LoadWorkdayTypes(); return page;
    }

    void LoadWorkdayTypes()
    {
        workdayTypes.Rows.Clear(); foreach (var type in AttendanceConfigurationStore.ReadWorkdayTypes(settings)) workdayTypes.Rows.Add(type.Id, type.Name, type.Segments.Count == 1 ? "1 tramo" : $"{type.Segments.Count} tramos", FormatHours(type));
        if (employees.Columns.Contains("Tipo")) employees.Rows.Clear(); RefreshJourneyOptions(); if (employees.Columns.Contains("Tipo")) LoadEmployees();
    }

    WorkdayType? SelectedWorkdayType()
    {
        var id = workdayTypes.CurrentRow?.Cells["Id"].Value?.ToString(); return string.IsNullOrWhiteSpace(id) ? null : AttendanceConfigurationStore.ReadWorkdayTypes(settings).FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    void EditWorkdayType(WorkdayType? current)
    {
        var id = current?.Id ?? $"jornada-{Guid.NewGuid():N}"; using var form = new WorkdayTypeEditForm(id, current); if (form.ShowDialog(this) != DialogResult.OK || form.Result is null) return;
        try
        {
            var duplicate = AttendanceConfigurationStore.ReadWorkdayTypes(settings).Any(x => !x.Id.Equals(form.Result.Id, StringComparison.OrdinalIgnoreCase) && x.Name.Equals(form.Result.Name, StringComparison.OrdinalIgnoreCase)); if (duplicate) throw new InvalidOperationException("Ya existe otra jornada con ese nombre.");
            AttendanceConfigurationStore.SaveWorkdayType(settings, form.Result); var saved = AttendanceConfigurationStore.ReadWorkdayTypes(settings).FirstOrDefault(x => x.Id.Equals(form.Result.Id, StringComparison.OrdinalIgnoreCase)); if (saved is null) throw new InvalidOperationException("La jornada no pudo verificarse después de guardarla."); LoadWorkdayTypes(); MessageBox.Show(this, $"La jornada '{saved.Name}' se guardó correctamente.", "Tipo de jornada", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "No se pudo guardar la jornada", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    void DeleteWorkdayType()
    {
        var selected = SelectedWorkdayType(); if (selected is null) return; if (!Confirm($"¿Eliminar la jornada '{selected.Name}'?")) return;
        try { AttendanceConfigurationStore.DeleteWorkdayType(settings, selected.Id); LoadWorkdayTypes(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "No se pudo eliminar la jornada", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    static string FormatHours(WorkdayType type)
    {
        var schedule = string.Join(" / ", type.Segments.Select(x => $"{x.Entry:hh\\:mm}–{x.Exit:hh\\:mm}"));
        return type.Lunch is null ? schedule : $"{schedule} · Almuerzo opcional {type.Lunch.Start:hh\\:mm}–{type.Lunch.End:hh\\:mm}";
    }
}
