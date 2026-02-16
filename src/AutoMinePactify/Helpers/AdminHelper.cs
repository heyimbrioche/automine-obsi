using System.Runtime.Versioning;
using System.Security.Principal;

namespace AutoMinePactify.Helpers;

[SupportedOSPlatform("windows")]
public static class AdminHelper
{
    public static bool IsRunAsAdmin()
    {
        try
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
