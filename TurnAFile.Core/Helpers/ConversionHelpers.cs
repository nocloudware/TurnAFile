using System;
using System.IO;
using System.Diagnostics;
using TurnAFile.Core.Services;

namespace TurnAFile.Core.Helpers;

public static class ConversionHelpers
{
    public static string GetFFmpegPath()
    {
        return Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "tools",
            "FFmpeg",
            "ffmpeg.exe");
    }

    public static string GetOutputPath(string inputPath, string targetFormat, string? customOutputFolder = null)
    {
        string directory = !string.IsNullOrEmpty(customOutputFolder)
            ? customOutputFolder
            : Path.GetDirectoryName(inputPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        string targetExt = NormalizeExtension(targetFormat);

        string outputPath = Path.Combine(directory, $"{fileNameWithoutExt}.{targetExt}");
        int counter = 1;
        while (File.Exists(outputPath))
        {
            outputPath = Path.Combine(directory, $"{fileNameWithoutExt} ({counter++}).{targetExt}");
        }
        return outputPath;
    }

    public static string NormalizeExtension(string targetFormat)
    {
        return targetFormat.ToLower() switch
        {
            "jpg" or "jpeg" => "jpg",
            "docx" or "xlsx" or "md" or "html" or "epub" or "txt" or "csv" or "json" or "png" or "webp" or "gif" or "bmp" or "mp4" or "avi" or "mkv" or "webm" or "mov" or "mp3" or "wav" or "m4a" or "flac" or "ogg" or "pdf" => targetFormat.ToLower(),
            "ocr" => "txt",
            _ => targetFormat.ToLower()
        };
    }

    public static VideoQuality MapVideoQuality(ToggleState s) => s switch
    {
        ToggleState.High => VideoQuality.High,
        ToggleState.Low => VideoQuality.Low,
        _ => VideoQuality.Medium
    };

    public static AudioQuality MapAudioQuality(ToggleState s) => s switch
    {
        ToggleState.High => AudioQuality.High,
        ToggleState.Low => AudioQuality.Low,
        _ => AudioQuality.Medium
    };

    public static ImageScaling MapImageScaling(ToggleState s) => s switch
    {
        ToggleState.High => ImageScaling.Original,
        ToggleState.Low => ImageScaling.FiftyPercent,
        _ => ImageScaling.SeventyFivePercent
    };

    public static VideoQuality MapVideoQuality(string q) => q switch
    {
        "high" => VideoQuality.High,
        "low" => VideoQuality.Low,
        _ => VideoQuality.Medium
    };

    public static AudioQuality MapAudioQuality(string q) => q switch
    {
        "high" => AudioQuality.High,
        "low" => AudioQuality.Low,
        _ => AudioQuality.Medium
    };

    public static ImageScaling MapImageScaling(string q) => q switch
    {
        "high" => ImageScaling.Original,
        "low" => ImageScaling.FiftyPercent,
        _ => ImageScaling.SeventyFivePercent
    };

    public static ToggleState MapToggleStateFromVideoQuality(VideoQuality q) => q switch
    {
        VideoQuality.High => ToggleState.High,
        VideoQuality.Low => ToggleState.Low,
        _ => ToggleState.Medium
    };

    public static ToggleState MapToggleStateFromAudioQuality(AudioQuality q) => q switch
    {
        AudioQuality.High => ToggleState.High,
        AudioQuality.Low => ToggleState.Low,
        _ => ToggleState.Medium
    };

    public static ToggleState MapToggleStateFromImageScaling(ImageScaling s) => s switch
    {
        ImageScaling.Original => ToggleState.High,
        ImageScaling.FiftyPercent => ToggleState.Low,
        _ => ToggleState.Medium
    };
}
