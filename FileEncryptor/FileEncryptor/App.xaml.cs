using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace FileEncryptor;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var args = e.Args;
        if (args.Length >= 2 && (args[0] == "--encrypt" || args[0] == "--decrypt"))
        {
            bool encrypt = args[0] == "--encrypt";
            string filePath = args[1];

            if (!File.Exists(filePath))
            {
                MessageBox.Show($"文件不存在：{filePath}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            var dialog = new PasswordDialog(encrypt, Path.GetFileName(filePath));
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string password = dialog.Password;
                    string dir = Path.GetDirectoryName(filePath) ?? Path.GetTempPath();
                    string fileName = Path.GetFileName(filePath);

                    if (encrypt)
                    {
                        string ext = dialog.CustomExtension;
                        if (string.IsNullOrWhiteSpace(ext)) ext = ".encrypted";
                        if (!ext.StartsWith('.')) ext = "." + ext;

                        string outputPath = Path.Combine(dir, fileName + ext);
                        EncryptFile(filePath, outputPath, password);

                        // 覆盖原文件模式：删除源文件
                        try { File.Delete(filePath); } catch { }

                        MessageBox.Show($"加密完成！\n{outputPath}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        string outputPath = Path.Combine(dir, fileName);
                        DecryptFile(filePath, outputPath, password);

                        // 覆盖原文件模式：删除加密文件
                        try { File.Delete(filePath); } catch { }

                        MessageBox.Show($"解密完成！\n{outputPath}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (CryptographicException)
                {
                    MessageBox.Show("密码错误或文件已损坏", "解密失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"操作失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            Shutdown();
            return;
        }

        // 正常启动 GUI
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }

    private static void EncryptFile(string inputPath, string outputPath, string password)
    {
        const int KeySize = 256;
        const int BlockSize = 128;
        const int Iterations = 100_000;
        const int SaltSize = 16;
        const int IvSize = 16;

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, KeySize / 8);
        byte[] iv = RandomNumberGenerator.GetBytes(IvSize);

        using var inputStream = File.OpenRead(inputPath);
        using var outputStream = File.Create(outputPath);

        byte[] originalNameBytes = Encoding.UTF8.GetBytes(Path.GetFileName(inputPath));
        outputStream.Write(salt, 0, SaltSize);
        outputStream.Write(iv, 0, IvSize);
        outputStream.Write(BitConverter.GetBytes(originalNameBytes.Length), 0, 4);
        outputStream.Write(originalNameBytes, 0, originalNameBytes.Length);

        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.BlockSize = BlockSize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        using var cryptoStream = new CryptoStream(outputStream, aes.CreateEncryptor(), CryptoStreamMode.Write);
        byte[] buffer = new byte[81920];
        int bytesRead;
        while ((bytesRead = inputStream.Read(buffer, 0, buffer.Length)) > 0)
            cryptoStream.Write(buffer, 0, bytesRead);
        cryptoStream.FlushFinalBlock();
    }

    private static void DecryptFile(string inputPath, string outputPath, string password)
    {
        const int KeySize = 256;
        const int BlockSize = 128;
        const int Iterations = 100_000;
        const int SaltSize = 16;
        const int IvSize = 16;

        using var inputStream = File.OpenRead(inputPath);

        byte[] salt = new byte[SaltSize];
        byte[] iv = new byte[IvSize];
        inputStream.ReadExactly(salt);
        inputStream.ReadExactly(iv);

        byte[] nameLenBytes = new byte[4];
        inputStream.ReadExactly(nameLenBytes);
        int nameLen = BitConverter.ToInt32(nameLenBytes, 0);
        byte[] nameBytes = new byte[nameLen];
        inputStream.ReadExactly(nameBytes);
        string originalFileName = Encoding.UTF8.GetString(nameBytes);

        byte[] key = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, KeySize / 8);

        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.BlockSize = BlockSize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        using var cryptoStream = new CryptoStream(inputStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
        string finalOutputPath = Path.Combine(Path.GetDirectoryName(outputPath) ?? outputPath, originalFileName);
        using var outputStream = File.Create(finalOutputPath);
        cryptoStream.CopyTo(outputStream);
    }
}
