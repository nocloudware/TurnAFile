namespace TurnAFile.Core.Services;

public enum VideoQuality
{
    High,
    Medium,
    Low
}

public enum AudioQuality
{
    High,
    Medium,
    Low
}

public enum ImageScaling
{
    Original,
    SeventyFivePercent,
    FiftyPercent
}

public class FfmpegCommandBuilder
{
    private bool _useVP8ForWebM = true;

    public string BuildCommand(string inputPath, string outputPath,
        string targetFormat, VideoQuality videoQuality,
        AudioQuality audioQuality, ImageScaling imageScaling)
    {
        var ext = targetFormat.ToLower();

        if (ext == "mp4" || ext == "avi" || ext == "mkv" || ext == "webm" || ext == "mov")
            return BuildVideoCommand(inputPath, outputPath, ext, videoQuality, audioQuality);

        if (ext == "mp3" || ext == "wav" || ext == "m4a" || ext == "flac" || ext == "ogg")
            return BuildAudioCommand(inputPath, outputPath, ext, audioQuality);

        if (ext == "jpg" || ext == "png" || ext == "webp" || ext == "gif" || ext == "bmp")
            return BuildImageCommand(inputPath, outputPath, ext, imageScaling);

        throw new NotSupportedException($"Formato no soportado: {targetFormat}");
    }

    private string BuildVideoCommand(string input, string output, string format,
        VideoQuality videoQuality, AudioQuality audioQuality)
    {
        if (format == "webm" && _useVP8ForWebM)
        {
            var qualityValue = videoQuality switch
            {
                VideoQuality.High => 10,
                VideoQuality.Medium => 18,
                VideoQuality.Low => 30,
                _ => 18
            };

            var audioBitrate = audioQuality switch
            {
                AudioQuality.High => "192k",
                AudioQuality.Medium => "128k",
                AudioQuality.Low => "96k",
                _ => "128k"
            };

            return $"-i \"{input}\" -c:v libvpx -crf {qualityValue} -b:v 0 " +
                   $"-c:a libopus -b:a {audioBitrate} \"{output}\" -y";
        }

        if (format == "webm")
        {
            var qualityValue = videoQuality switch
            {
                VideoQuality.High => 10,
                VideoQuality.Medium => 23,
                VideoQuality.Low => 35,
                _ => 23
            };

            var audioBitrate = audioQuality switch
            {
                AudioQuality.High => "192k",
                AudioQuality.Medium => "128k",
                AudioQuality.Low => "96k",
                _ => "128k"
            };

            return $"-i \"{input}\" -c:v libvpx-vp9 -crf {qualityValue} -b:v 0 " +
                   $"-c:a libopus -b:a {audioBitrate} \"{output}\" -y";
        }

        var (codec, preset, crf) = videoQuality switch
        {
            VideoQuality.High => ("libx264", "slow", 18),
            VideoQuality.Medium => ("libx264", "medium", 23),
            VideoQuality.Low => ("libx264", "ultrafast", 28),
            _ => ("libx264", "medium", 23)
        };

        var audioBitrateDefault = audioQuality switch
        {
            AudioQuality.High => "192k",
            AudioQuality.Medium => "128k",
            AudioQuality.Low => "96k",
            _ => "128k"
        };

        return $"-i \"{input}\" -c:v {codec} -preset {preset} -crf {crf} " +
               $"-pix_fmt yuv420p -c:a aac -b:a {audioBitrateDefault} \"{output}\" -y";
    }

    private string BuildAudioCommand(string input, string output, string format, AudioQuality quality)
    {
        if (format == "wav")
            return $"-i \"{input}\" -vn -c:a pcm_s16le \"{output}\" -y";

        if (format == "flac")
            return $"-i \"{input}\" -vn -c:a flac -compression_level 5 \"{output}\" -y";

        if (format == "ogg")
        {
            var qualityValue = quality switch
            {
                AudioQuality.High => "10",
                AudioQuality.Medium => "5",
                AudioQuality.Low => "3",
                _ => "5"
            };
            return $"-i \"{input}\" -vn -c:a libvorbis -q:a {qualityValue} \"{output}\" -y";
        }

        var bitrate = quality switch
        {
            AudioQuality.High => "320k",
            AudioQuality.Medium => "192k",
            AudioQuality.Low => "128k",
            _ => "192k"
        };

        var codec = format == "mp3" ? "libmp3lame" : "aac";

        return $"-i \"{input}\" -vn -c:a {codec} -b:a {bitrate} \"{output}\" -y";
    }

    private string BuildImageCommand(string input, string output, string format, ImageScaling scaling)
    {
        // Special case: Image to PDF
        if (format.ToLower() == "pdf")
        {
            return $"-i \"{input}\" -vf \"format=rgb24\" \"{output}\" -y";
        }
        
        var scaleFilter = scaling switch
        {
            ImageScaling.Original => "",
            ImageScaling.SeventyFivePercent => "-vf \"scale=iw*0.75:ih*0.75\"",
            ImageScaling.FiftyPercent => "-vf \"scale=iw*0.5:ih*0.5\"",
            _ => ""
        };

        var quality = format == "jpg" ? "-q:v 2" : "";

        return $"-i \"{input}\" {scaleFilter} {quality} \"{output}\" -y".Trim();
    }
}