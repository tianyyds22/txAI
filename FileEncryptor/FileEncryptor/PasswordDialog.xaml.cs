using System.Windows;

namespace FileEncryptor;

public partial class PasswordDialog : Window
{
    public string Password => PasswordBoxMain.Password;
    public string CustomExtension => ".encrypted";
    public bool IsEncryptMode { get; }

    public string FileInfo
    {
        get
        {
            string mode = IsEncryptMode ? "加密" : "解密";
            return $"文件：{FileName}  |  模式：{mode}";
        }
    }

    public string FileName { get; }

    public PasswordDialog(bool encrypt, string fileName)
    {
        InitializeComponent();
        IsEncryptMode = encrypt;
        FileName = fileName;
        DataContext = this;

        Title = encrypt ? "加密文件" : "解密文件";
        OkButton.Content = encrypt ? "加密" : "解密";

        if (!encrypt)
        {
            ConfirmLabel.Visibility = Visibility.Collapsed;
            PasswordBoxConfirm.Visibility = Visibility.Collapsed;
            Height = 260;
        }

        Loaded += (_, _) => PasswordBoxMain.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(PasswordBoxMain.Password))
        {
            MessageBox.Show("请输入密码", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (PasswordBoxMain.Password.Length < 6)
        {
            MessageBox.Show("密码长度不能少于 6 位", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (IsEncryptMode && PasswordBoxMain.Password != PasswordBoxConfirm.Password)
        {
            MessageBox.Show("两次输入的密码不一致", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
