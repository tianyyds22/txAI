using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileEncryptor.Models;
using FileEncryptor.Services;
using Microsoft.Win32;

namespace FileEncryptor.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly AesEncryptionService _encryptionService = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private List<string> _allFilePaths = [];

    [ObservableProperty]
    private string _statusText = "就绪 — 拖拽文件到窗口即可开始";

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private int _fileCount;

    [ObservableProperty]
    private bool _isDragOver;

    [ObservableProperty]
    private int _operationMode;

    [ObservableProperty]
    private int _outputMode;

    [ObservableProperty]
    private string _customExtension = ".encrypted";

    [ObservableProperty]
    private ObservableCollection<EncryptionFileItem> _files = [];

    public bool IsListEmpty => Files.Count == 0;
    public bool IsListVisible => Files.Count > 0;
    public string ExecuteButtonText => OperationMode == 0 ? "\U0001F512 开始加密" : "\U0001F513 开始解密";
    public bool IsEncryptMode => OperationMode == 0;
    public bool IsDecryptMode => OperationMode == 1;

    public MainViewModel()
    {
        Files.CollectionChanged += (_, _) =>
        {
            FileCount = Files.Count;
            OnPropertyChanged(nameof(IsListEmpty));
            OnPropertyChanged(nameof(IsListVisible));
            ExecuteCommand.NotifyCanExecuteChanged();
        };
    }

    partial void OnOperationModeChanged(int value)
    {
        OnPropertyChanged(nameof(ExecuteButtonText));
        OnPropertyChanged(nameof(IsEncryptMode));
        OnPropertyChanged(nameof(IsDecryptMode));
    }

    partial void OnIsProcessingChanged(bool value)
    {
        ExecuteCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void SelectFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择要加密/解密的文件",
            Filter = "所有文件 (*.*)|*.*",
            Multiselect = true,
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            AddFiles(dialog.FileNames);
        }
    }

    [RelayCommand]
    private void SelectFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择包含文件的文件夹"
        };

        if (dialog.ShowDialog() == true)
        {
            var files = Directory.GetFiles(dialog.FolderName);
            if (files.Length > 0)
                AddFiles(files);
            else
                StatusText = "所选文件夹内无文件";
        }
    }

    [RelayCommand]
    private void RemoveFile(EncryptionFileItem? item)
    {
        if (item == null) return;
        Files.Remove(item);
        int index = _allFilePaths.IndexOf(item.FullPath);
        if (index >= 0)
            _allFilePaths.RemoveAt(index);
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ExecuteAsync()
    {
        bool encrypt = OperationMode == 0;
        string password = GetPasswordFromUI();

        if (string.IsNullOrEmpty(password))
        {
            MessageBox.Show($"请输入{(encrypt ? "加密" : "解密")}密码", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (password.Length < 6)
        {
            MessageBox.Show("密码长度不能少于 6 位", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (encrypt)
        {
            string confirm = GetConfirmPasswordFromUI();
            if (password != confirm)
            {
                MessageBox.Show("两次输入的密码不一致", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        if (_allFilePaths.Count == 0)
        {
            MessageBox.Show("请先添加文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string modeText = encrypt ? "加密" : "解密";

        var confirmResult = MessageBox.Show(
            $"确定要{modeText} {_allFilePaths.Count} 个文件吗？",
            "确认操作",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmResult != MessageBoxResult.Yes) return;

        _cancellationTokenSource = new CancellationTokenSource();
        IsProcessing = true;
        ProgressValue = 0;
        StatusText = $"正在{modeText}...";

        try
        {
            var progress = new Progress<EncryptionProgressInfo>(info =>
            {
                ProgressValue = info.Percentage;
                ProgressText = $"正在处理：{info.FileName} ({info.Current}/{info.Total})";

                var currentFile = Files.FirstOrDefault(f => f.FileName == info.FileName);
                if (currentFile != null)
                    currentFile.Status = "处理中...";
            });

            string outputModeText = OutputMode == 0 ? "覆盖原文件" : "输出到新文件夹";
            string ext = CustomExtension;
            if (!ext.StartsWith('.'))
                ext = "." + ext;

            var result = await _encryptionService.ProcessFilesAsync(
                _allFilePaths, encrypt, password, outputModeText, ext,
                progress, _cancellationTokenSource.Token);

            for (int i = 0; i < Files.Count; i++)
            {
                if (i < _allFilePaths.Count)
                    Files[i].Status = "已完成";
            }

            string message = $"完成！成功：{result.SuccessCount}，失败：{result.FailCount}";
            if (result.Errors.Count > 0)
            {
                message += $"\n\n错误详情：\n{string.Join("\n", result.Errors.Take(5))}";
            }

            StatusText = message;
            MessageBox.Show(message, "执行结果", MessageBoxButton.OK,
                result.FailCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            StatusText = "操作已取消";
        }
        catch (Exception ex)
        {
            StatusText = $"执行失败：{ex.Message}";
            MessageBox.Show($"执行失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
            ProgressValue = 0;
            ProgressText = string.Empty;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _cancellationTokenSource?.Cancel();
        StatusText = "正在取消...";
    }

    [RelayCommand]
    private void Clear()
    {
        Files.Clear();
        _allFilePaths.Clear();
        ProgressValue = 0;
        ProgressText = string.Empty;
        StatusText = "列表已清空";
    }

    [RelayCommand]
    private void RegisterMenu()
    {
        if (Services.ContextMenuService.Register())
        {
            StatusText = "右键菜单注册成功";
            MessageBox.Show("右键菜单已注册！\n\n在资源管理器中右键任意文件即可看到加密/解密选项。",
                "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    private void UnregisterMenu()
    {
        if (Services.ContextMenuService.Unregister())
        {
            StatusText = "右键菜单已卸载";
            MessageBox.Show("右键菜单已卸载。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public void LoadDroppedFiles(string[] paths)
    {
        var files = paths.Where(File.Exists).ToList();
        var dirs = paths.Where(Directory.Exists).ToList();

        foreach (var dir in dirs)
        {
            files.AddRange(Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly));
        }

        if (files.Count > 0)
        {
            AddFiles(files.ToArray());
        }
        else
        {
            StatusText = "未找到有效文件";
        }
    }

    private void AddFiles(string[] filePaths)
    {
        int added = 0;
        foreach (string filePath in filePaths)
        {
            if (_allFilePaths.Contains(filePath)) continue;

            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists) continue;

            _allFilePaths.Add(filePath);
            string ext = Path.GetExtension(filePath);
            Files.Add(new EncryptionFileItem
            {
                FileName = fileInfo.Name,
                FullPath = fileInfo.FullName,
                Size = FormatFileSize(fileInfo.Length),
                Status = "待处理",
                TypeIcon = EncryptionFileItem.DetectTypeIcon(ext),
                TypeLabel = EncryptionFileItem.DetectTypeLabel(ext)
            });
            added++;
        }

        StatusText = added > 0
            ? $"已添加 {added} 个文件，共 {Files.Count} 个文件"
            : "没有新文件被添加（可能已存在）";
    }

    private bool CanExecute() => !IsProcessing && Files.Count > 0;

    private string GetPasswordFromUI()
    {
        if (Application.Current.MainWindow is MainWindow mainWindow)
            return mainWindow.CachedPassword;
        return string.Empty;
    }

    private string GetConfirmPasswordFromUI()
    {
        if (Application.Current.MainWindow is MainWindow mainWindow)
            return mainWindow.CachedConfirmPassword;
        return string.Empty;
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
