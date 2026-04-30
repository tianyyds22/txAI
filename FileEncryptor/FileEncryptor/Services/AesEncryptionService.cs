using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FileEncryptor.Services;

public class EncryptionProgressInfo
{
    public int Current { get; set; }
    public int Total { get; set; }
    public string FileName { get; set; } = string.Empty;
    public double Percentage => Total > 0 ? (double)Current / Total * 100 : 0;
}

public class EncryptionResult
{
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public List<string> Errors { get; set; } = [];
}

public class AesEncryptionService
{
    private const int KeySize = 256;
    private const int BlockSize = 128;
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int IvSize = 16;

    private static (byte[] key, byte[] salt) DeriveKey(string password, byte[]? existingSalt = null)
    {
        byte[] salt = existingSalt ?? RandomNumberGenerator.GetBytes(SaltSize);
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, KeySize / 8);
        return (key, salt);
    }

    public async Task EncryptFileAsync(
        string inputPath, string outputPath, string password,
        IProgress<EncryptionProgressInfo> progress,
        CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            byte[] passwordSalt = RandomNumberGenerator.GetBytes(SaltSize);
            var (key, _) = DeriveKey(password, passwordSalt);
            byte[] iv = RandomNumberGenerator.GetBytes(IvSize);

            using var inputStream = File.OpenRead(inputPath);
            using var outputStream = File.Create(outputPath);

            byte[] originalNameBytes = Encoding.UTF8.GetBytes(Path.GetFileName(inputPath));
            outputStream.Write(passwordSalt, 0, SaltSize);
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
            {
                cancellationToken.ThrowIfCancellationRequested();
                cryptoStream.Write(buffer, 0, bytesRead);
            }

            cryptoStream.FlushFinalBlock();
        }, cancellationToken);
    }

    public async Task DecryptFileAsync(
        string inputPath, string outputPath, string password,
        IProgress<EncryptionProgressInfo> progress,
        CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
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

            var (key, _) = DeriveKey(password, salt);

            using var aes = Aes.Create();
            aes.KeySize = KeySize;
            aes.BlockSize = BlockSize;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key;
            aes.IV = iv;

            using var cryptoStream = new CryptoStream(inputStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
            string finalOutputPath = Path.Combine(Path.GetDirectoryName(outputPath) ?? outputPath,
                                                   originalFileName);
            using var outputStream = File.Create(finalOutputPath);
            cryptoStream.CopyTo(outputStream);
        }, cancellationToken);
    }

    public async Task<EncryptionResult> ProcessFilesAsync(
        IEnumerable<string> filePaths,
        bool encrypt,
        string password,
        string outputMode,
        string encryptedExtension,
        IProgress<EncryptionProgressInfo> progress,
        CancellationToken cancellationToken)
    {
        var result = new EncryptionResult();
        var files = filePaths.ToList();
        int total = files.Count;
        int current = 0;

        foreach (string filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            current++;

            string fileName = Path.GetFileName(filePath);
            progress?.Report(new EncryptionProgressInfo
            {
                Current = current,
                Total = total,
                FileName = fileName
            });

            try
            {
                string outputDir;
                if (outputMode == "覆盖原文件")
                {
                    outputDir = Path.GetDirectoryName(filePath) ?? Path.GetTempPath();
                }
                else
                {
                    outputDir = Path.Combine(
                        Path.GetDirectoryName(filePath) ?? Path.GetTempPath(),
                        encrypt ? "Encrypted" : "Decrypted");
                    Directory.CreateDirectory(outputDir);
                }

                string extension = encrypt ? encryptedExtension : "";
                string outputPath = Path.Combine(outputDir, fileName + extension);

                if (encrypt)
                {
                    await EncryptFileAsync(filePath, outputPath, password, progress!, cancellationToken);
                }
                else
                {
                    await DecryptFileAsync(filePath, outputPath, password, progress!, cancellationToken);
                }

                // 覆盖原文件模式：成功后删除源文件
                if (outputMode == "覆盖原文件" && File.Exists(filePath))
                {
                    try { File.Delete(filePath); }
                    catch { /* 保留原文件，不阻断流程 */ }
                }

                result.SuccessCount++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (CryptographicException)
            {
                result.FailCount++;
                result.Errors.Add($"{fileName}: 密码错误或文件已损坏");
            }
            catch (Exception ex)
            {
                result.FailCount++;
                result.Errors.Add($"{fileName}: {ex.Message}");
            }
        }

        return result;
    }
}
