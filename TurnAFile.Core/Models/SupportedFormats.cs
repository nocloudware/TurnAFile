using System.Collections.Generic;

namespace TurnAFile.Core.Models;

public static class SupportedFormats
{
    public static List<string> DocumentFormats { get; } = new()
    {
        "DOCX", "PDF", "MD", "MARKDOWN", "HTML", "HTM", "EPUB", "TXT", "RTF", "ODT"
    };

    public static List<string> DataFormats { get; } = new()
    {
        "XLSX", "CSV", "JSON", "PDF"
    };

    public static List<string> VideoFormats { get; } = new()
    {
        "MP4", "AVI", "MKV", "WEBM", "MOV", "WMV", "FLV"
    };

    public static List<string> AudioFormats { get; } = new()
    {
        "MP3", "WAV", "M4A", "FLAC", "OGG", "AAC", "WMA"
    };

    public static List<string> ImageFormats { get; } = new()
    {
        "JPG", "JPEG", "PNG", "WEBP", "GIF", "BMP", "TIFF"
    };
}
