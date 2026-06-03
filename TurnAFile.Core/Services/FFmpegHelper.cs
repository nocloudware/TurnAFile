using System.Diagnostics;
using System.Text.RegularExpressions;

namespace TurnAFile.Core.Services;

public static class FFmpegHelper
{
    public static string GetVersion(string ffmpegPath)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = "-version",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
                return "desconocida";

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            var match = Regex.Match(output ?? "", @"ffmpeg version ([0-9.]+)");
            return match.Success ? match.Groups[1].Value : "desconocida";
        }
        catch
        {
            return "desconocida";
        }
    }

    public static string GetCodecForFormat(string targetFormat, bool isVideo)
    {
        return targetFormat.ToUpper() switch
        {
            "MP4" => "libx264",
            "AVI" => "libx264",
            "MKV" => "libx264",
            "WEBM" => "libvpx-vp9",
            "MOV" => "libx264",
            "MP3" => "libmp3lame",
            "WAV" => "pcm_s16le",
            "M4A" => "aac",
            "FLAC" => "flac",
            "OGG" => "libvorbis",
            "JPG" => "mjpeg",
            "PNG" => "png",
            "WEBP" => "libwebp",
            "GIF" => "gif",
            "BMP" => "bmp",
            _ => "desconocido"
        };
    }

    public static string GetQualityParam(string targetFormat, string quality)
    {
        return targetFormat.ToUpper() switch
        {
            "MP4" or "AVI" or "MKV" or "MOV" => quality switch
            {
                "Alta" => "crf=18",
                "Media" => "crf=23",
                "Baja" => "crf=28",
                _ => "crf=23"
            },
            "WEBM" => quality switch
            {
                "Alta" => "crf=10",
                "Media" => "crf=23",
                "Baja" => "crf=35",
                _ => "crf=23"
            },
            "MP3" => quality switch
            {
                "Alta" => "bitrate=320k",
                "Media" => "bitrate=192k",
                "Baja" => "bitrate=128k",
                _ => "bitrate=192k"
            },
            "JPG" => quality switch
            {
                "Alta" => "q=2",
                "Media" => "q=5",
                "Baja" => "q=10",
                _ => "q=5"
            },
            "WEBP" => quality switch
            {
                "Alta" => "q=10",
                "Media" => "q=25",
                "Baja" => "q=40",
                _ => "q=25"
            },
            "PNG" => quality switch
            {
                "Alta" => "compression=1",
                "Media" => "compression=6",
                "Baja" => "compression=9",
                _ => "compression=6"
            },
            _ => "parámetro por defecto"
        };
    }
}