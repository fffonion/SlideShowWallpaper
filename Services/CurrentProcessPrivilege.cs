using System.Security.Principal;

namespace SlideShowWallpaper.Services;

public static class CurrentProcessPrivilege
{
    public static bool IsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
