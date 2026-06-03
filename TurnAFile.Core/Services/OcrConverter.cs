using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tesseract;

namespace TurnAFile.Core.Services;

public class OcrConverter
{
    public async Task<ConversionResult> ConvertImageToTextAsync(
        string inputPath,
        string outputPath,
        string targetFormat,
        CancellationToken cancellationToken)
    {
        var result = new ConversionResult();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (!File.Exists(inputPath))
            {
                result.Success = false;
                result.ErrorMessage = "El archivo de imagen no existe";
                return result;
            }

            string extractedText = await PerformOcrAsync(inputPath, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                result.Success = false;
                result.ErrorMessage = "Cancelado por el usuario";
                return result;
            }

            if (string.IsNullOrEmpty(extractedText))
            {
                result.Success = false;
                result.ErrorMessage = "No se pudo extraer texto de la imagen";
                return result;
            }

            string target = targetFormat.ToLower();
            if (target != "txt")
            {
                result.Success = false;
                result.ErrorMessage = $"OCR solo admite salida a TXT (recibido: {targetFormat})";
                return result;
            }

            await File.WriteAllTextAsync(outputPath, extractedText, cancellationToken);

            result.Success = true;
            result.OutputPath = outputPath;
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.ErrorMessage = "Cancelado por el usuario";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        return result;
    }

    private async Task<string> PerformOcrAsync(string inputPath, CancellationToken ct)
    {
        try
        {
            return await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                string tessdataPath = FindTessdataPath()!;

                if (string.IsNullOrEmpty(tessdataPath) || !Directory.Exists(tessdataPath))
                {
                    throw new InvalidOperationException(
                        "Datos de idioma OCR no encontrados.\n\n" +
                        "Los archivos de idioma (.traineddata) deben estar en: tools\\tessdata\\\n\n" +
                        "Idiomas soportados: spa, eng, fra, deu, ita, por, rus, ara, kor, " +
                        "jpn, nld, pol, tur, vie\n\n" +
                        "Descargue desde: https://github.com/tesseract-ocr/tessdata");
                }

                var languages = new[] { "spa", "eng", "fra", "deu", "ita", "por", "rus", "ara", "kor", "jpn", "nld", "pol", "tur", "vie" };

                string text = string.Empty;

                foreach (var lang in languages)
                {
                    var trainedDataFile = Path.Combine(tessdataPath, $"{lang}.traineddata");
                    if (!File.Exists(trainedDataFile))
                        continue;

                    try
                    {
                        using var engine = new TesseractEngine(tessdataPath, lang, EngineMode.Default);
                        using var img = Pix.LoadFromFile(inputPath);
                        using var page = engine.Process(img);
                        text = page.GetText();

                        if (!string.IsNullOrWhiteSpace(text) && text.Trim().Length > 2)
                        {
                            System.Diagnostics.Debug.WriteLine($"OCR success with language: {lang}");
                            return text.Trim();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"OCR failed with {lang}: {ex.Message}");
                    }
                }

                return string.Empty;
            }, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error en OCR: {ex.Message}", ex);
        }
    }

    private string? FindTessdataPath()
    {
        string[] searchPaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "tessdata"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TurnAFile", "tessdata"),
        };

        foreach (var path in searchPaths)
        {
            if (Directory.Exists(path))
            {
                var files = Directory.GetFiles(path, "*.traineddata");
                if (files.Length > 0)
                {
                    return path;
                }
            }
        }

        return null;
    }
}
