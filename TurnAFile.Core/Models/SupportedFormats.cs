using System.Collections.Generic;

namespace TurnAFile.Core.Models;

public static class SupportedFormats
{
    public static List<string> DocumentFormats { get; } = new()
    {
        "DOCX", "PDF", "MD", "HTML", "EPUB", "TXT"
    };

    public static List<string> DataFormats { get; } = new()
    {
        "XLSX", "CSV", "JSON", "PDF"
    };

    public static List<string> OcrFormats { get; } = new()
    {
        "TXT", "DOCX", "XLSX", "PDF"
    };

    public static List<string> VideoFormats { get; } = new()
    {
        "MP4", "AVI", "MKV", "WEBM", "MOV"
    };

    public static List<string> AudioFormats { get; } = new()
    {
        "MP3", "WAV", "M4A", "FLAC", "OGG"
    };

    public static List<string> ImageFormats { get; } = new()
    {
        "JPG", "PNG", "WEBP", "GIF", "BMP"
    };
}