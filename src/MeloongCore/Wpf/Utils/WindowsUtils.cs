using System.Security.Principal;

namespace MeloongCore;
public static class WindowsUtils {

    /// <summary>
    /// Whether the current program has administrator privileges.
    /// </summary>
    public static bool HasAdminRole() {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

}
