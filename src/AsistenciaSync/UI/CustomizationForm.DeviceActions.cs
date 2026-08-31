using AsistenciaSync.Services;

namespace AsistenciaSync.UI;

internal sealed partial class CustomizationForm
{
    void CreateEmployee()
    {
        using var form = new CreateEmployeeForm(); if (form.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            using var device = new ZkDeviceClient(settings.DeviceIp, settings.DevicePort); device.CreateUser(form.EmployeeId, form.EmployeeName); var map = CsvStore.ReadEmployees(settings); map[form.EmployeeId] = form.EmployeeName; CsvStore.SaveEmployeesMap(settings, map); LoadEmployees(); MessageBox.Show(this, "Trabajador creado en el reloj y en el reporte local.", "Trabajadores", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "No se pudo crear el trabajador", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    void RunDeviceAction(string title, Action<ZkDeviceClient> action) { try { using var device = new ZkDeviceClient(settings.DeviceIp, settings.DevicePort); action(device); MessageBox.Show(this, "Operación completada correctamente.", title, MessageBoxButtons.OK, MessageBoxIcon.Information); } catch (Exception ex) { MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error); } }
    bool Confirm(string message) => MessageBox.Show(this, message, "Confirmar operación", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
}
