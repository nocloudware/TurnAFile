using System.Diagnostics;
using System.Text.RegularExpressions;

namespace TurnAFile.Core.Services;

public class ConversionProgressEventArgs : EventArgs
{
    public string FileName { get; set; } = string.Empty;
    public double Percentage { get; set; }
    public TimeSpan? CurrentTime { get; set; }
    public TimeSpan? Duration { get; set; }
    public int FileIndex { get; set; }
    public int TotalFiles { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
}

public class ConversionService
{
    private readonly FfmpegCommandBuilder _commandBuilder = new();
    private Process? _currentProcess;
    private TimeSpan? _totalDuration;
    private double _lastReportedPercentage = -1;
    private DateTime _startTime;
    private bool _isAudioOrImage = false;
    private bool _isCancelling = false;
    private int _lastFrame = 0;
    private int _totalFrames = 0;

    public event EventHandler<ConversionProgressEventArgs>? ProgressChanged;

    public async Task<bool> ConvertAsync(string inputPath, string outputPath,
        string targetFormat, VideoQuality videoQuality,
        AudioQuality audioQuality, ImageScaling imageScaling,
        CancellationToken cancellationToken, string ffmpegPath)
    {
        var args = _commandBuilder.BuildCommand(inputPath, outputPath,
            targetFormat, videoQuality, audioQuality, imageScaling);

        var processInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        _currentProcess = new Process { StartInfo = processInfo };
        _totalDuration = null;
        _lastReportedPercentage = -1;
        _startTime = DateTime.Now;
        _isCancelling = false;
        _lastFrame = 0;
        _totalFrames = 0;

        string ext = Path.GetExtension(inputPath).ToLower();
        _isAudioOrImage = ext is ".mp3" or ".wav" or ".m4a" or ".flac" or ".ogg" or
                          ".aac" or ".wma" or ".jpg" or ".jpeg" or ".png" or
                          ".gif" or ".bmp" or ".webp" or ".tiff";

        // Para imágenes, intentar obtener el número total de frames si es un GIF animado
        if (ext == ".gif")
        {
            await GetTotalFramesForGif(inputPath, ffmpegPath);
        }

        using (cancellationToken.Register(() => Cancel()))
        {
            _currentProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data) && !_isCancelling)
                    ParseProgress(e.Data, inputPath);
            };

            _currentProcess.Start();
            _currentProcess.BeginErrorReadLine();

            await _currentProcess.WaitForExitAsync(cancellationToken);

            var success = !_isCancelling && _currentProcess.ExitCode == 0;
            _currentProcess.Dispose();
            _currentProcess = null;

            return success;
        }
    }

    public void Cancel()
    {
        try
        {
            _isCancelling = true;
            if (_currentProcess != null && !_currentProcess.HasExited)
            {
                _currentProcess.Kill(entireProcessTree: true);
                _currentProcess.WaitForExit(5000);
            }
            _currentProcess?.Dispose();
            _currentProcess = null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error al cancelar: {ex.Message}");
        }
    }

    private async Task GetTotalFramesForGif(string inputPath, string ffmpegPath)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-i \"{inputPath}\" -f null -",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            using var process = new Process { StartInfo = processInfo };
            process.Start();
            string output = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var match = Regex.Match(output, @"(\d+) frames");
            if (match.Success)
            {
                _totalFrames = int.Parse(match.Groups[1].Value);
                Debug.WriteLine($"Total frames para GIF: {_totalFrames}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error obteniendo frames del GIF: {ex.Message}");
        }
    }

    private void ParseProgress(string line, string fileName)
    {
        // Intentar obtener duración total (solo una vez)
        if (_totalDuration == null)
        {
            var durationMatch = Regex.Match(line, @"Duration: (\d{2}):(\d{2}):(\d{2}\.\d{2})");
            if (durationMatch.Success)
            {
                _totalDuration = TimeSpan.Parse($"{durationMatch.Groups[1]}:{durationMatch.Groups[2]}:{durationMatch.Groups[3]}");
                Debug.WriteLine($"Duración total detectada: {_totalDuration}");
            }
        }

        double percentage = -1;
        TimeSpan? currentTime = null;

        // Método 1: time= (para video)
        var timeMatch = Regex.Match(line, @"time=(\d{2}):(\d{2}):(\d{2}\.\d{2})");
        if (timeMatch.Success)
        {
            currentTime = TimeSpan.Parse($"{timeMatch.Groups[1]}:{timeMatch.Groups[2]}:{timeMatch.Groups[3]}");
            if (_totalDuration.HasValue && _totalDuration.Value.TotalSeconds > 0)
            {
                percentage = (currentTime.Value.TotalSeconds / _totalDuration.Value.TotalSeconds) * 100;
                Debug.WriteLine($"Progreso por time: {percentage:F1}%");
            }
        }

        // Método 2: out_time_ms (para audio)
        if (percentage < 0)
        {
            var altTimeMatch = Regex.Match(line, @"out_time_ms=(\d+)");
            if (altTimeMatch.Success)
            {
                long ms = long.Parse(altTimeMatch.Groups[1].Value);
                currentTime = TimeSpan.FromMilliseconds(ms);
                if (_totalDuration.HasValue && _totalDuration.Value.TotalSeconds > 0)
                {
                    percentage = (currentTime.Value.TotalSeconds / _totalDuration.Value.TotalSeconds) * 100;
                    Debug.WriteLine($"Progreso por out_time_ms: {percentage:F1}%");
                }
            }
        }

        // Método 3: frame= (para imágenes GIF animadas)
        if (percentage < 0 && _totalFrames > 0)
        {
            var frameMatch = Regex.Match(line, @"frame=\s*(\d+)");
            if (frameMatch.Success)
            {
                int currentFrame = int.Parse(frameMatch.Groups[1].Value);
                if (currentFrame > _lastFrame)
                {
                    _lastFrame = currentFrame;
                    percentage = (double)currentFrame / _totalFrames * 100;
                    percentage = Math.Min(percentage, 99);
                    Debug.WriteLine($"Progreso por frame: {percentage:F1}% (frame {currentFrame}/{_totalFrames})");
                }
            }
        }

        // Método 4: size= (para imágenes estáticas)
        if (percentage < 0 && _isAudioOrImage && _totalDuration == null)
        {
            var sizeMatch = Regex.Match(line, @"size=\s*(\d+)kB");
            if (sizeMatch.Success)
            {
                // Progreso incremental basado en actividad
                percentage = Math.Min(_lastReportedPercentage + 3, 95);
                if (_lastReportedPercentage < 0) percentage = 10;
                Debug.WriteLine($"Progreso por size: {percentage:F1}%");
            }
        }

        // Método 5: progreso basado en tiempo para archivos sin duración
        if (percentage < 0 && _isAudioOrImage && _totalDuration == null)
        {
            var elapsed = DateTime.Now - _startTime;
            if (elapsed.TotalSeconds < 30)
            {
                percentage = Math.Min(elapsed.TotalSeconds / 30 * 100, 95);
                if (percentage < 5) percentage = 5;
            }
            else
            {
                percentage = Math.Min(_lastReportedPercentage + 1, 90);
                if (_lastReportedPercentage < 0) percentage = 5;
            }
            Debug.WriteLine($"Progreso por tiempo: {percentage:F1}%");
        }

        if (percentage >= 0)
        {
            percentage = Math.Min(percentage, 100);

            // Evitar que el progreso retroceda
            if (percentage < _lastReportedPercentage && _lastReportedPercentage > 0 && percentage > 0)
            {
                percentage = _lastReportedPercentage;
            }

            _lastReportedPercentage = percentage;

            ProgressChanged?.Invoke(this, new ConversionProgressEventArgs
            {
                FileName = Path.GetFileName(fileName),
                Percentage = percentage,
                CurrentTime = currentTime,
                Duration = _totalDuration,
                StatusMessage = $"Procesando {Path.GetFileName(fileName)}..."
            });
        }
    }
}