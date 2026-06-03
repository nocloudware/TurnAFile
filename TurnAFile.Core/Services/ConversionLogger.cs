using System;
using System.IO;
using System.Text;

namespace TurnAFile.Core.Services;

public class ConversionLogger
{
    private readonly string _logPath;
    private readonly string _logFolder;
    private readonly string _sessionId;
    private readonly object _lock = new object();
    private const long MaxLogSize = 1024 * 1024; // 1 MB

    public ConversionLogger()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _logFolder = Path.Combine(appData, "TurnAFile", "logs");

        if (!Directory.Exists(_logFolder))
            Directory.CreateDirectory(_logFolder);

        _logPath = Path.Combine(_logFolder, "TurnAFile.log");

        RotateLogIfNeeded();

        _sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
    }

    public string SessionId => _sessionId;

    private void RotateLogIfNeeded()
    {
        try
        {
            if (File.Exists(_logPath))
            {
                var fileInfo = new FileInfo(_logPath);
                if (fileInfo.Length >= MaxLogSize)
                {
                    string timestamp = DateTime.Now.ToString("ddMMyy_HHmmss");
                    string backupPath = Path.Combine(_logFolder, $"TurnAFile_{timestamp}.log");
                    File.Move(_logPath, backupPath);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error rotando log: {ex.Message}");
        }
    }

    private void WriteLog(string message)
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_logPath))
                {
                    var fileInfo = new FileInfo(_logPath);
                    if (fileInfo.Length >= MaxLogSize)
                    {
                        string timestamp = DateTime.Now.ToString("ddMMyy_HHmmss");
                        string backupPath = Path.Combine(_logFolder, $"TurnAFile_{timestamp}.log");
                        File.Move(_logPath, backupPath);
                    }
                }

                File.AppendAllText(_logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | SESSION: {_sessionId} | {message}{Environment.NewLine}", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error escribiendo log: {ex.Message}");
            }
        }
    }

    public void LogBatchStart(int totalFiles)
    {
        WriteLog($"Inicio lote | {totalFiles} archivos");
    }

    public void LogConversionStart(int index, int total, string sourceFormat, string targetFormat,
                                    string quality, long fileSize, string ffmpegVersion,
                                    string codec, string qualityParam)
    {
        WriteLog($"[{index}/{total}] Inicio | {sourceFormat} → {targetFormat} | Calidad {quality} | " +
                 $"Tamaño: {FormatFileSize(fileSize)} | FFmpeg: {ffmpegVersion} | Codec: {codec} | {qualityParam}");
    }

    public void LogConversionComplete(int index, int total, bool success, TimeSpan duration, long outputSize, long inputSize)
    {
        if (success)
        {
            double compression = (1 - (double)outputSize / inputSize) * 100;
            WriteLog($"[{index}/{total}] Fin | Éxito | Duración: {duration.TotalSeconds:F1}s | " +
                     $"Tamaño destino: {FormatFileSize(outputSize)} | Compresión: {compression:F0}%");
        }
        else
        {
            WriteLog($"[{index}/{total}] Fin | ERROR | Duración: {duration.TotalSeconds:F1}s");
        }
    }

    public void LogBatchComplete(int successCount, int total, TimeSpan totalDuration)
    {
        WriteLog($"Fin lote | {successCount}/{total} exitosos | Tiempo total: {totalDuration.TotalSeconds:F0}s");
    }

    public void LogError(string message, Exception? ex = null)
    {
        WriteLog($"ERROR | {message}");
        if (ex != null)
            WriteLog($"       | Detalle: {ex.Message}");
    }

    public void LogInfo(string message)
    {
        WriteLog($"INFO | {message}");
    }

    public void LogWarning(string message)
    {
        WriteLog($"WARN | {message}");
    }

    private string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}