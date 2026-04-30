using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using FileEncryptor.ViewModels;

namespace FileEncryptor;

public partial class MainWindow : Window
{
    // 缓存密码，防止 PasswordBox 在 UI 更新时丢失状态
    public string CachedPassword { get; private set; } = string.Empty;
    public string CachedConfirmPassword { get; private set; } = string.Empty;

    // DWM Backdrop API (Windows 11 22H2+)
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMSBT_MAINWINDOW = 2;   // Mica
    private const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic

    [DllImport("dwmapi.dll", SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        TryEnableAcrylic();
    }

    private void TryEnableAcrylic()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int backdropType = DWMSBT_TRANSIENTWINDOW;
        DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));

        // 启用深色模式标题栏
        int darkMode = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            if (DataContext is MainViewModel vm)
                vm.IsDragOver = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.IsDragOver = false;

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[]? paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length == 0) return;

            if (DataContext is MainViewModel vm2)
            {
                vm2.LoadDroppedFiles(paths);
            }
        }
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ActualWidth > 1100)
            LeftColumn.Width = new GridLength(300);
        else if (ActualWidth > 900)
            LeftColumn.Width = new GridLength(270);
        else
            LeftColumn.Width = new GridLength(240);
    }

    private void RemoveFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn &&
            btn.DataContext is Models.EncryptionFileItem item &&
            DataContext is MainViewModel vm)
        {
            vm.RemoveFileCommand.Execute(item);
        }
    }

    private void PasswordBoxMain_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox pb)
            CachedPassword = pb.Password;
    }

    private void PasswordBoxConfirm_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox pb)
            CachedConfirmPassword = pb.Password;
    }
}
