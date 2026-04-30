using CommunityToolkit.Mvvm.ComponentModel;

namespace FileEncryptor.Models;

public partial class EncryptionFileItem : ObservableObject
{
    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _fullPath = string.Empty;

    [ObservableProperty]
    private string _size = string.Empty;

    [ObservableProperty]
    private string _status = "待处理";

    [ObservableProperty]
    private string _typeIcon = "\U0001F4C4";

    [ObservableProperty]
    private string _typeLabel = "文件";

    [ObservableProperty]
    private bool _isSelected = true;

    public static string DetectTypeIcon(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            // 视频
            ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".flv" or ".webm" or ".m4v" or ".ts" or ".rmvb" => "\U0001F3AC",
            // 音频
            ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".wma" or ".m4a" or ".ape" => "\U0001F3B5",
            // 图片
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg" or ".ico" or ".tiff" or ".psd" => "\U0001F5BC\uFE0F",
            // 文档
            ".pdf" => "\U0001F4D5",
            ".doc" or ".docx" => "\U0001F4DD",
            ".xls" or ".xlsx" => "\U0001F4CA",
            ".ppt" or ".pptx" => "\U0001F4CA",
            ".txt" or ".md" or ".log" => "\U0001F4DD",
            // 压缩包
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".bz2" => "\U0001F4E6",
            // 代码
            ".cs" or ".js" or ".py" or ".java" or ".cpp" or ".h" or ".xaml" or ".json" or ".xml" => "\U0001F4BB",
            // 可执行
            ".exe" or ".msi" or ".dll" => "\U0001F4E5",
            // 加密后
            ".encrypted" or ".enc" or ".lock" => "\U0001F512",
            // 默认
            _ => "\U0001F4C4"
        };
    }

    public static string DetectTypeLabel(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".flv" or ".webm" or ".m4v" or ".ts" or ".rmvb" => "视频",
            ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".wma" or ".m4a" or ".ape" => "音频",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg" or ".ico" or ".tiff" or ".psd" => "图片",
            ".pdf" => "PDF",
            ".doc" or ".docx" => "Word",
            ".xls" or ".xlsx" => "Excel",
            ".ppt" or ".pptx" => "PPT",
            ".txt" or ".md" or ".log" => "文本",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".bz2" => "压缩包",
            ".cs" or ".js" or ".py" or ".java" or ".cpp" or ".h" or ".xaml" or ".json" or ".xml" => "代码",
            ".exe" or ".msi" or ".dll" => "程序",
            ".encrypted" or ".enc" or ".lock" => "已加密",
            _ => "文件"
        };
    }
}
