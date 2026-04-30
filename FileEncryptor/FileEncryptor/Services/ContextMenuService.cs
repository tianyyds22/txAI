using System.Security.Principal;
using System.Windows;
using Microsoft.Win32;

namespace FileEncryptor.Services;

public static class ContextMenuService
{
    private const string EncryptKeyName = @"*\shell\FileEncryptor_Encrypt";
    private const string EncryptCommandKeyName = @"*\shell\FileEncryptor_Encrypt\command";
    private const string DecryptKeyName = @"*\shell\FileEncryptor_Decrypt";
    private const string DecryptCommandKeyName = @"*\shell\FileEncryptor_Decrypt\command";

    private static string ExePath =>
        Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";

    public static bool IsRegistered()
    {
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(EncryptKeyName);
            return key != null;
        }
        catch
        {
            return false;
        }
    }

    public static bool Register()
    {
        if (!IsAdministrator())
        {
            MessageBox.Show("需要管理员权限才能注册右键菜单，请以管理员身份运行后重试。",
                "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        try
        {
            // 加密菜单项
            using (var key = Registry.ClassesRoot.CreateSubKey(EncryptKeyName))
            {
                key.SetValue("", "使用 FileEncryptor 加密");
                key.SetValue("Icon", $"\"{ExePath}\",0");
            }
            using (var key = Registry.ClassesRoot.CreateSubKey(EncryptCommandKeyName))
            {
                key.SetValue("", $"\"{ExePath}\" --encrypt \"%1\"");
            }

            // 解密菜单项
            using (var key = Registry.ClassesRoot.CreateSubKey(DecryptKeyName))
            {
                key.SetValue("", "使用 FileEncryptor 解密");
                key.SetValue("Icon", $"\"{ExePath}\",0");
            }
            using (var key = Registry.ClassesRoot.CreateSubKey(DecryptCommandKeyName))
            {
                key.SetValue("", $"\"{ExePath}\" --decrypt \"%1\"");
            }

            // 刷新资源管理器
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"注册失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    public static bool Unregister()
    {
        if (!IsAdministrator())
        {
            MessageBox.Show("需要管理员权限才能卸载右键菜单，请以管理员身份运行后重试。",
                "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        try
        {
            Registry.ClassesRoot.DeleteSubKeyTree(EncryptKeyName, false);
            Registry.ClassesRoot.DeleteSubKeyTree(DecryptKeyName, false);

            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"卸载失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);

    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const int SHCNF_IDLIST = 0x0000;
}
