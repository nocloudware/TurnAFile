using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace TurnAFile.Windows.Services;

public static class ShellMenuInstaller
{
    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private const uint SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    private const string AppName = "TurnAFile";
    private const string AddEntryName = "TurnAFileAdd";
    private const string MUIVerbKey = "MUIVerb";
    private const string IconKey = "Icon";
    private const string SubCmdsKey = "SubCommands";

    private static readonly string[] VideoExtensions =
        { ".mp4", ".avi", ".mkv", ".webm", ".mov", ".wmv", ".flv" };

    private static readonly string[] AudioExtensions =
        { ".mp3", ".wav", ".m4a", ".flac", ".ogg", ".aac", ".wma" };

    private static readonly string[] ImageExtensions =
        { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff" };

    private static readonly string[] DocumentExtensions =
        { ".docx", ".pdf", ".md", ".markdown", ".html", ".htm", ".epub", ".txt" };

    private static readonly string[] DataExtensions =
        { ".xlsx", ".csv", ".json" };

    private static readonly string[] VideoFormats = { "MP4", "AVI", "MKV", "WEBM", "MOV", "MP3", "M4A" };
    private static readonly string[] AudioFormats = { "MP3", "WAV", "M4A", "FLAC", "OGG" };
    private static readonly string[] ImageFormats = { "JPG", "PNG", "WEBP", "GIF", "BMP", "PDF" };
    private static readonly string[] DocumentFormats = { "DOCX", "PDF", "MD", "HTML", "EPUB", "TXT" };
    private static readonly string[] DataFormats = { "XLSX", "CSV", "JSON", "PDF" };
    private static readonly string[] OcrFormats = { "TXT", "DOCX", "XLSX" };

    // Resuelve Assets\Icons\ relativa al exe en tiempo de ejecución
    private static string IconsDirectory =>
        Path.Combine(
            Path.GetDirectoryName(
                System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName)!,
            "Assets", "Icons");

    public static void Install()
    {
        string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName;

        foreach (var ext in VideoExtensions)
        {
            RegisterAddEntry(ext, exePath);
            RegisterExtension(ext, VideoFormats, exePath);
        }

        foreach (var ext in AudioExtensions)
        {
            RegisterAddEntry(ext, exePath);
            RegisterExtension(ext, AudioFormats, exePath);
        }

        foreach (var ext in ImageExtensions)
        {
            RegisterAddEntry(ext, exePath);
            RegisterExtensionWithOcr(ext, ImageFormats, exePath);
        }

        foreach (var ext in DocumentExtensions)
        {
            RegisterAddEntry(ext, exePath);
            RegisterSimpleExtension(ext, DocumentFormats, exePath);
        }

        foreach (var ext in DataExtensions)
        {
            RegisterAddEntry(ext, exePath);
            RegisterSimpleExtension(ext, DataFormats, exePath);
        }

        // Force Windows Explorer to refresh context menu cache
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
    }

    public static void Uninstall()
    {
        var allExtensions = VideoExtensions
            .Concat(AudioExtensions)
            .Concat(ImageExtensions)
            .Concat(DocumentExtensions)
            .Concat(DataExtensions);

        foreach (var ext in allExtensions)
        {
            UnregisterExtension(ext, AppName);
            UnregisterExtension(ext, AddEntryName);
        }
    }

    public static void Reinstall()
    {
        Uninstall();
        Thread.Sleep(500);
        Install(); // Re-extracts icons + reinstalls registry
    }

    public static bool IsInstalled()
    {
        string rootPath = @$"Software\Classes\SystemFileAssociations\.mp4\shell\{AppName}";
        using var key = Registry.CurrentUser.OpenSubKey(rootPath);
        return key != null;
    }

    // ---------------------------------------------------------------------

    private static void RegisterAddEntry(string ext, string exePath)
    {
        string path = $@"Software\Classes\SystemFileAssociations\{ext}\shell\{AddEntryName}";

        using var key = Registry.CurrentUser.CreateSubKey(path);
        key.SetValue(MUIVerbKey, "Agregar a TurnAFile");
        key.SetValue(IconKey, $"\"{exePath}\",0");
        key.SetValue("MultiSelectModel", "Player");

        using var cmdKey = Registry.CurrentUser.CreateSubKey($@"{path}\command");
        cmdKey.SetValue("", $"\"{exePath}\" --add \"%1\"");
    }

    private static void RegisterExtension(
        string ext, string[] formats, string exePath)
    {
        string basePath = $@"Software\Classes\SystemFileAssociations\{ext}\shell\{AppName}";

        using var rootKey = Registry.CurrentUser.CreateSubKey(basePath);
        rootKey.SetValue(MUIVerbKey, "Convertir con TurnAFile");
        rootKey.SetValue(IconKey, $"\"{exePath}\",0");
        rootKey.SetValue(SubCmdsKey, string.Empty);

        var category = GetCategoryFromExtension(ext);

        int order = 1;
        foreach (var format in formats)
        {
            string formatPath = $@"{basePath}\shell\{order:D2}_{format}";
            order++;

            using var formatItem = Registry.CurrentUser.CreateSubKey(formatPath);
            formatItem.SetValue(MUIVerbKey, $"Convertir a {format}");
            formatItem.SetValue(SubCmdsKey, string.Empty);

            RegisterQualityItem(formatPath, "1_Alta", "Calidad Alta", exePath, format, "high", category);
            RegisterQualityItem(formatPath, "2_Media", "Calidad Media", exePath, format, "medium", category);
            RegisterQualityItem(formatPath, "3_Baja", "Calidad Baja", exePath, format, "low", category);
        }
    }

    private static void RegisterExtensionWithOcr(
        string ext, string[] formats, string exePath)
    {
        string basePath = $@"Software\Classes\SystemFileAssociations\{ext}\shell\{AppName}";

        using var rootKey = Registry.CurrentUser.CreateSubKey(basePath);
        rootKey.SetValue(MUIVerbKey, "Convertir con TurnAFile");
        rootKey.SetValue(IconKey, $"\"{exePath}\",0");
        rootKey.SetValue(SubCmdsKey, string.Empty);

        var category = GetCategoryFromExtension(ext);

        int order = 1;
        foreach (var format in formats)
        {
            string formatPath = $@"{basePath}\shell\{order:D2}_{format}";
            order++;

            using var formatItem = Registry.CurrentUser.CreateSubKey(formatPath);
            formatItem.SetValue(MUIVerbKey, $"Convertir a {format}");
            formatItem.SetValue(SubCmdsKey, string.Empty);

            RegisterQualityItem(formatPath, "1_Alta", "Calidad Alta", exePath, format, "high", category);
            RegisterQualityItem(formatPath, "2_Media", "Calidad Media", exePath, format, "medium", category);
            RegisterQualityItem(formatPath, "3_Baja", "Calidad Baja", exePath, format, "low", category);
        }

        // Agregar submenú OCR
        string ocrPath = $@"{basePath}\shell\{order:D2}_OCR";
        using var ocrItem = Registry.CurrentUser.CreateSubKey(ocrPath);
        ocrItem.SetValue(MUIVerbKey, "Extraer texto (OCR)");
        ocrItem.SetValue(SubCmdsKey, string.Empty);

        // Opciones de formato OCR
        RegisterOcrFormatItem(ocrPath, "1_TXT", "Guardar como TXT", exePath, "txt");
        RegisterOcrFormatItem(ocrPath, "2_DOCX", "Guardar como DOCX", exePath, "docx");
        RegisterOcrFormatItem(ocrPath, "3_XLSX", "Guardar como XLSX", exePath, "xlsx");
    }

    private static void RegisterSimpleExtension(
        string ext, string[] formats, string exePath)
    {
        string basePath = $@"Software\Classes\SystemFileAssociations\{ext}\shell\{AppName}";

        using var rootKey = Registry.CurrentUser.CreateSubKey(basePath);
        rootKey.SetValue(MUIVerbKey, "Convertir con TurnAFile");
        rootKey.SetValue(IconKey, $"\"{exePath}\",0");
        rootKey.SetValue(SubCmdsKey, string.Empty);

        var category = GetCategoryFromExtension(ext);

        int order = 1;
        foreach (var format in formats)
        {
            string formatPath = $@"{basePath}\shell\{order:D2}_{format}";
            order++;

            // Register command directly on the format item (no quality submenus)
            string cmd = $"\"{exePath}\" --convert \"%1\" --format \"{format.ToLower()}\" --quality \"medium\" --type \"{category}\"";

            using var formatItem = Registry.CurrentUser.CreateSubKey(formatPath);
            formatItem.SetValue(MUIVerbKey, $"Convertir a {format}");
            using var cmdKey = Registry.CurrentUser.CreateSubKey($@"{formatPath}\command");
            cmdKey.SetValue("", cmd);
        }
    }

    private static void RegisterQualityItem(
        string parentPath, string keyName, string label,
        string exePath, string format, string quality, string category)
    {
        string fullPath = $@"{parentPath}\shell\{keyName}";
        using var item = Registry.CurrentUser.CreateSubKey(fullPath);
        item.SetValue(MUIVerbKey, label);

        // Register command to launch conversion
        string cmd = $"\"{exePath}\" --convert \"%1\" --format \"{format.ToLower()}\" --quality \"{quality}\" --type \"{category}\"";
        using var cmdKey = Registry.CurrentUser.CreateSubKey($@"{fullPath}\command");
        cmdKey.SetValue("", cmd);
    }

    private static void RegisterOcrFormatItem(
        string parentPath, string keyName, string label, string exePath, string format)
    {
        string fullPath = $@"{parentPath}\shell\{keyName}";
        using var item = Registry.CurrentUser.CreateSubKey(fullPath);
        item.SetValue(MUIVerbKey, label);

        // Register command to launch OCR conversion
        string cmd = $"\"{exePath}\" --convert \"%1\" --format \"{format.ToLower()}\" --quality \"medium\" --type \"ocr\"";
        using var cmdKey = Registry.CurrentUser.CreateSubKey($@"{fullPath}\command");
        cmdKey.SetValue("", cmd);
    }

    private static string GetCategoryFromExtension(string ext)
    {
        var lowerExt = ext.TrimStart('.').ToLowerInvariant();
        if (VideoExtensions.Any(e => e.TrimStart('.') == lowerExt))
            return "video";
        if (AudioExtensions.Any(e => e.TrimStart('.') == lowerExt))
            return "audio";
        if (ImageExtensions.Any(e => e.TrimStart('.') == lowerExt))
            return "image";
        if (DocumentExtensions.Any(e => e.TrimStart('.') == lowerExt))
            return "document";
        if (DataExtensions.Any(e => e.TrimStart('.') == lowerExt))
            return "data";
        return "unknown";
    }

    private static void UnregisterExtension(string ext, string keyName)
    {
        string path = $@"Software\Classes\SystemFileAssociations\{ext}\shell";
        try
        {
            using var shellKey = Registry.CurrentUser.OpenSubKey(path, writable: true);
            shellKey?.DeleteSubKeyTree(keyName, throwOnMissingSubKey: false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Error al desregistrar {ext}\\{keyName}: {ex.Message}");
        }
    }
}