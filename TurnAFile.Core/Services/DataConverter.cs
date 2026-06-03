using ClosedXML.Excel;
using System;
using System.Data;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace TurnAFile.Core.Services;

public class DataConverter
{
    private readonly ConversionLogger _logger;

    public DataConverter(ConversionLogger? logger = null)
    {
        _logger = logger ?? new ConversionLogger();
    }
    public async Task<ConversionResult> ConvertExcelToCsvAsync(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var result = new ConversionResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInfo($"DATA: ExcelToCsv starting: {Path.GetFileName(inputPath)}");
        _logger.LogInfo($"DATA: Input: {inputPath} (exists: {File.Exists(inputPath)})");
        _logger.LogInfo($"DATA: Output: {outputPath}");

        try
        {
            if (!File.Exists(inputPath))
            {
                result.Success = false;
                result.ErrorMessage = $"Archivo de entrada no encontrado: {inputPath}";
                return result;
            }

            using var workbook = new XLWorkbook(inputPath);
            var worksheet = workbook.Worksheet(1);

            using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);

            // Get the actual last row number safely
            int lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
            int lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;

            System.Diagnostics.Debug.WriteLine($"Excel rows: {lastRow}, cols: {lastCol}");

            for (int row = 1; row <= lastRow; row++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.Success = false;
                    result.ErrorMessage = "Cancelado por el usuario";
                    return result;
                }

                var lineParts = new List<string>();
                for (int col = 1; col <= lastCol; col++)
                {
                    var cell = worksheet.Cell(row, col);
                    if (cell != null && !cell.IsEmpty())
                    {
                        string cellValue = cell.GetString();
                        // Escape quotes and wrap in quotes
                        lineParts.Add($"\"{cellValue.Replace("\"", "\"\"")}\"");
                    }
                    else
                    {
                        lineParts.Add("\"\"");
                    }
                }
                await writer.WriteLineAsync(string.Join(",", lineParts));
            }

            result.Success = true;
            _logger.LogInfo($"DATA: ExcelToCsv SUCCESS");
        }
        catch (Exception ex)
        {
            result.Success = false;
            
            // Detect unsupported format (e.g., .xls renamed to .xlsx)
            string errorMessage = ex.Message;
            if (errorMessage.Contains("not a valid OpenXml") || 
                errorMessage.Contains("InvalidDataException") ||
                errorMessage.Contains("File contains corrupted data") ||
                errorMessage.Contains("The file is not") ||
                errorMessage.Contains("EndOfCentralDirectory"))
            {
                string ext = Path.GetExtension(inputPath).ToLower();
                result.ErrorMessage = $"Formato no soportado: El archivo tiene extensi�n {ext} pero no es un archivo {ext.ToUpper()} v�lido. Es posible que el archivo tenga un formato antiguo (como .xls) renombrado a {ext.ToUpper()}.";
            }
            else
            {
                result.ErrorMessage = errorMessage;
            }
            
            _logger.LogError($"DATA: ExcelToCsv ERROR - {result.ErrorMessage}");
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        return result;
    }

    public async Task<ConversionResult> ConvertCsvToExcelAsync(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var result = new ConversionResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInfo($"DATA: CsvToExcel starting: {Path.GetFileName(inputPath)}");
        _logger.LogInfo($"DATA: Input: {inputPath} (exists: {File.Exists(inputPath)})");
        _logger.LogInfo($"DATA: Output: {outputPath}");

        try
        {
            if (!File.Exists(inputPath))
            {
                result.Success = false;
                result.ErrorMessage = $"Archivo de entrada no encontrado: {inputPath}";
                return result;
            }

            var lines = await File.ReadAllLinesAsync(inputPath, cancellationToken);
            System.Diagnostics.Debug.WriteLine($"CSV lines: {lines.Length}");
            
            if (lines.Length == 0)
            {
                result.Success = false;
                result.ErrorMessage = "El archivo CSV est� vac�o";
                return result;
            }
            
            using var workbook = new XLWorkbook();
            var worksheet = workbook.AddWorksheet("Sheet1");

            for (int i = 0; i < lines.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.Success = false;
                    result.ErrorMessage = "Cancelado por el usuario";
                    return result;
                }

                var values = ParseCsvLine(lines[i]);
                System.Diagnostics.Debug.WriteLine($"CSV row {i + 1}: {values.Count} columns");
                
                for (int j = 0; j < values.Count; j++)
                {
                    worksheet.Cell(i + 1, j + 1).Value = values[j];
                }
            }

            System.Diagnostics.Debug.WriteLine($"Saving workbook to: {outputPath}");
            workbook.SaveAs(outputPath);
            
            // Verify file was created
            if (!File.Exists(outputPath))
            {
                result.Success = false;
                result.ErrorMessage = $"No se pudo crear el archivo Excel: {outputPath}";
                System.Diagnostics.Debug.WriteLine($"ERROR: Output file not created: {outputPath}");
                return result;
            }
            
            var fileInfo = new FileInfo(outputPath);
            _logger.LogInfo($"DATA: CsvToExcel SUCCESS - {fileInfo.Length} bytes");
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            
            // Detect common CSV format issues
            if (ex.Message.Contains("encoding") || ex.Message.Contains("UTF"))
            {
                result.ErrorMessage = $"Error de codificaci�n: El archivo CSV podr�a tener una codificaci�n no soportada. Intente guardarlo como UTF-8.";
            }
            
            _logger.LogError($"DATA: CsvToExcel ERROR - {result.ErrorMessage}");
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        return result;
    }

    /// <summary>
    /// Parses a CSV line handling quoted fields with commas
    /// </summary>
    private List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;
        
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            
            if (c == '"')
            {
                // Check for escaped quote ("")
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++; // Skip next quote
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                values.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        
        // Add last value
        values.Add(current.ToString().Trim());
        return values;
    }

    public async Task<ConversionResult> ConvertJsonToCsvAsync(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var result = new ConversionResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInfo($"DATA: JsonToCsv starting: {Path.GetFileName(inputPath)}");
        _logger.LogInfo($"DATA: Input: {inputPath} (exists: {File.Exists(inputPath)})");
        _logger.LogInfo($"DATA: Output: {outputPath}");

        try
        {
            if (!File.Exists(inputPath))
            {
                result.Success = false;
                result.ErrorMessage = $"Archivo de entrada no encontrado: {inputPath}";
                return result;
            }

            var json = await File.ReadAllTextAsync(inputPath, cancellationToken);
            System.Diagnostics.Debug.WriteLine($"JSON length: {json.Length}");

            // Try to deserialize as array of objects
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            };

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            List<Dictionary<string, string>>? data = null;

            if (root.ValueKind == JsonValueKind.Array)
            {
                // Convert JSON array to flat dictionary structure
                data = new List<Dictionary<string, string>>();
                foreach (var element in root.EnumerateArray())
                {
                    var flatDict = FlattenJsonElement(element);
                    data.Add(flatDict);
                }
                System.Diagnostics.Debug.WriteLine($"JSON array: {data.Count} items");
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                // Single object - wrap in list
                data = new List<Dictionary<string, string>>
                {
                    FlattenJsonElement(root)
                };
                System.Diagnostics.Debug.WriteLine($"JSON object with {data[0].Count} keys");
            }
            else
            {
                result.Success = false;
                result.ErrorMessage = "JSON debe ser un objeto o array de objetos";
                return result;
            }

            if (data == null || data.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "JSON vac�o o formato inv�lido";
                return result;
            }

            // Extract all unique headers maintaining order
            var headers = new List<string>();
            var headerSet = new HashSet<string>();
            foreach (var row in data)
            {
                foreach (var key in row.Keys)
                {
                    if (headerSet.Add(key))
                    {
                        headers.Add(key);
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"CSV headers: {string.Join(", ", headers)}");

            // Write CSV
            using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);

            // Write header row
            await writer.WriteLineAsync(string.Join(",", headers.Select(h => $"\"{h.Replace("\"", "\"\"")}\"")));

            // Write data rows
            foreach (var row in data)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.Success = false;
                    result.ErrorMessage = "Cancelado por el usuario";
                    return result;
                }

                var values = headers.Select(h =>
                {
                    if (row.ContainsKey(h))
                    {
                        string val = row[h] ?? "";
                        return $"\"{val.Replace("\"", "\"\"")}\"";
                    }
                    return "\"\"";
                });
                await writer.WriteLineAsync(string.Join(",", values));
            }

            result.Success = true;
            _logger.LogInfo($"DATA: JsonToCsv SUCCESS");
        }
        catch (JsonException ex)
        {
            result.Success = false;
            result.ErrorMessage = $"JSON inv�lido: {ex.Message}";
            _logger.LogError($"DATA: JsonToCsv JSON ERROR - {ex.Message}");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError($"DATA: JsonToCsv ERROR - {ex.Message}");
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        return result;
    }

    /// <summary>
    /// Flattens a JSON object into a dictionary with dot-notation keys for nested values
    /// </summary>
    private Dictionary<string, string> FlattenJsonElement(JsonElement element)
    {
        var result = new Dictionary<string, string>();
        
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                string key = property.Name;
                string value = FlattenJsonValue(property.Value);
                result[key] = value;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var items = new List<string>();
            foreach (var item in element.EnumerateArray())
            {
                items.Add(FlattenJsonValue(item));
            }
            result["items"] = string.Join("; ", items);
        }
        else
        {
            result["value"] = element.ToString();
        }
        
        return result;
    }

    /// <summary>
    /// Converts a JSON value to a string representation
    /// </summary>
    private string FlattenJsonValue(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return "";
            case JsonValueKind.String:
                return element.GetString() ?? "";
            case JsonValueKind.Number:
                return element.ToString();
            case JsonValueKind.True:
                return "true";
            case JsonValueKind.False:
                return "false";
            case JsonValueKind.Object:
                // For nested objects, return a compact representation
                var props = new List<string>();
                foreach (var prop in element.EnumerateObject())
                {
                    props.Add($"{prop.Name}: {FlattenJsonValue(prop.Value)}");
                }
                return string.Join(", ", props);
            case JsonValueKind.Array:
                // For arrays, join items with semicolon
                var items = new List<string>();
                foreach (var item in element.EnumerateArray())
                {
                    items.Add(FlattenJsonValue(item));
                }
                return string.Join("; ", items);
            default:
                return element.ToString();
        }
    }

    public async Task<ConversionResult> ConvertJsonToExcelAsync(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var result = new ConversionResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInfo($"DATA: JsonToExcel starting: {Path.GetFileName(inputPath)}");
        _logger.LogInfo($"DATA: Input: {inputPath} (exists: {File.Exists(inputPath)})");
        _logger.LogInfo($"DATA: Output: {outputPath}");

        try
        {
            if (!File.Exists(inputPath))
            {
                result.Success = false;
                result.ErrorMessage = $"Archivo de entrada no encontrado: {inputPath}";
                return result;
            }

            var json = await File.ReadAllTextAsync(inputPath, cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            List<Dictionary<string, string>>? data = null;

            if (root.ValueKind == JsonValueKind.Array)
            {
                data = new List<Dictionary<string, string>>();
                foreach (var element in root.EnumerateArray())
                {
                    data.Add(FlattenJsonElement(element));
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                data = new List<Dictionary<string, string>> { FlattenJsonElement(root) };
            }
            else
            {
                result.Success = false;
                result.ErrorMessage = "JSON debe ser un objeto o array de objetos";
                return result;
            }

            if (data == null || data.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "JSON vac�o o formato inv�lido";
                return result;
            }

            var headers = new List<string>();
            var headerSet = new HashSet<string>();
            foreach (var row in data)
            {
                foreach (var key in row.Keys)
                {
                    if (headerSet.Add(key)) headers.Add(key);
                }
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.AddWorksheet("Data");

            for (int j = 0; j < headers.Count; j++)
                worksheet.Cell(1, j + 1).Value = headers[j];

            for (int i = 0; i < data.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    result.Success = false;
                    result.ErrorMessage = "Cancelado por el usuario";
                    return result;
                }

                var row = data[i];
                for (int j = 0; j < headers.Count; j++)
                {
                    worksheet.Cell(i + 2, j + 1).Value = row.ContainsKey(headers[j]) ? row[headers[j]] : "";
                }
            }

            workbook.SaveAs(outputPath);
            result.Success = true;
            _logger.LogInfo($"DATA: JsonToExcel SUCCESS - {new FileInfo(outputPath).Length} bytes");
        }
        catch (JsonException ex)
        {
            result.Success = false;
            result.ErrorMessage = $"JSON inv�lido: {ex.Message}";
            _logger.LogError($"DATA: JsonToExcel JSON ERROR - {ex.Message}");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError($"DATA: JsonToExcel ERROR - {ex.Message}");
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        return result;
    }

    public async Task<ConversionResult> ConvertExcelToJsonAsync(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var result = new ConversionResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInfo($"DATA: ExcelToJson starting: {Path.GetFileName(inputPath)}");

        try
        {
            if (!File.Exists(inputPath))
            {
                result.Success = false;
                result.ErrorMessage = $"Archivo de entrada no encontrado: {inputPath}";
                return result;
            }

            using var workbook = new XLWorkbook(inputPath);
            var worksheet = workbook.Worksheet(1);

            int lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
            int lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;

            // Extract headers
            var headers = new List<string>();
            for (int col = 1; col <= lastCol; col++)
            {
                headers.Add(worksheet.Cell(1, col).GetString());
            }

            // Build data
            var data = new List<Dictionary<string, string>>();
            for (int row = 2; row <= lastRow; row++)
            {
                var rowDict = new Dictionary<string, string>();
                for (int col = 1; col <= lastCol; col++)
                {
                    var cell = worksheet.Cell(row, col);
                    rowDict[headers[col - 1]] = cell.IsEmpty() ? "" : cell.GetString();
                }
                data.Add(rowDict);
            }

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(outputPath, json, cancellationToken);

            result.Success = true;
            _logger.LogInfo($"DATA: ExcelToJson SUCCESS - {new FileInfo(outputPath).Length} bytes");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError($"DATA: ExcelToJson ERROR - {ex.Message}");
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        return result;
    }

    public async Task<ConversionResult> ConvertCsvToJsonAsync(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var result = new ConversionResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInfo($"DATA: CsvToJson starting: {Path.GetFileName(inputPath)}");

        try
        {
            if (!File.Exists(inputPath))
            {
                result.Success = false;
                result.ErrorMessage = $"Archivo de entrada no encontrado: {inputPath}";
                return result;
            }

            var lines = await File.ReadAllLinesAsync(inputPath, cancellationToken);
            if (lines.Length == 0)
            {
                result.Success = false;
                result.ErrorMessage = "El archivo CSV est� vac�o";
                return result;
            }

            var headers = ParseCsvLine(lines[0]);
            var data = new List<Dictionary<string, string>>();

            for (int i = 1; i < lines.Length; i++)
            {
                var values = ParseCsvLine(lines[i]);
                var rowDict = new Dictionary<string, string>();
                for (int j = 0; j < headers.Count; j++)
                {
                    rowDict[headers[j]] = j < values.Count ? values[j] : "";
                }
                data.Add(rowDict);
            }

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(outputPath, json, cancellationToken);

            result.Success = true;
            _logger.LogInfo($"DATA: CsvToJson SUCCESS - {new FileInfo(outputPath).Length} bytes");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError($"DATA: CsvToJson ERROR - {ex.Message}");
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        return result;
    }

    public async Task<ConversionResult> ConvertExcelToPdfAsync(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var result = new ConversionResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInfo($"DATA: ExcelToPdf starting: {Path.GetFileName(inputPath)}");

        try
        {
            if (!File.Exists(inputPath))
            {
                result.Success = false;
                result.ErrorMessage = $"Archivo de entrada no encontrado: {inputPath}";
                return result;
            }

            using var workbook = new XLWorkbook(inputPath);
            int sheetCount = 0;

            foreach (var worksheet in workbook.Worksheets)
            {
                int lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
                int lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
                if (lastRow == 0 || lastCol == 0) continue;

                var rows = new List<List<string>>();
                for (int r = 1; r <= lastRow; r++)
                {
                    var row = new List<string>();
                    for (int c = 1; c <= lastCol; c++)
                    {
                        var cell = worksheet.Cell(r, c);
                        row.Add(cell.IsEmpty() ? "" : cell.GetString());
                    }
                    rows.Add(row);
                }

                string? title = sheetCount > 0 ? worksheet.Name : null;
                DocumentPdfGenerator.ConvertTableToPdf(rows, outputPath, title, landscape: true);
                sheetCount++;
            }

            result.Success = true;
            _logger.LogInfo($"DATA: ExcelToPdf SUCCESS - {new FileInfo(outputPath).Length} bytes");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError($"DATA: ExcelToPdf ERROR - {ex.Message}");
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        return result;
    }

    public async Task<ConversionResult> ConvertCsvToPdfAsync(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var result = new ConversionResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInfo($"DATA: CsvToPdf starting: {Path.GetFileName(inputPath)}");

        try
        {
            if (!File.Exists(inputPath))
            {
                result.Success = false;
                result.ErrorMessage = $"Archivo de entrada no encontrado: {inputPath}";
                return result;
            }

            var lines = await File.ReadAllLinesAsync(inputPath, cancellationToken);
            if (lines.Length == 0)
            {
                result.Success = false;
                result.ErrorMessage = "El archivo CSV está vacío";
                return result;
            }

            var allRows = new List<List<string>>();
            for (int i = 0; i < lines.Length; i++)
                allRows.Add(ParseCsvLine(lines[i]));

            if (allRows.Count == 0 || allRows.Max(r => r.Count) == 0)
            {
                result.Success = false;
                result.ErrorMessage = "El CSV no tiene columnas";
                return result;
            }

            DocumentPdfGenerator.ConvertTableToPdf(allRows, outputPath, title: null, landscape: true);

            result.Success = true;
            _logger.LogInfo($"DATA: CsvToPdf SUCCESS - {new FileInfo(outputPath).Length} bytes");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError($"DATA: CsvToPdf ERROR - {ex.Message}");
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        return result;
    }

    public async Task<ConversionResult> ConvertJsonToPdfAsync(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var result = new ConversionResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInfo($"DATA: JsonToPdf starting: {Path.GetFileName(inputPath)}");

        try
        {
            if (!File.Exists(inputPath))
            {
                result.Success = false;
                result.ErrorMessage = $"Archivo de entrada no encontrado: {inputPath}";
                return result;
            }

            var json = await File.ReadAllTextAsync(inputPath, cancellationToken);

            // Parse JSON into a flat table structure
            var rows = JsonToTableRows(json, Path.GetFileNameWithoutExtension(inputPath));
            DocumentPdfGenerator.ConvertTableToPdf(rows, outputPath, title: "JSON Data", landscape: true);

            result.Success = true;
            _logger.LogInfo($"DATA: JsonToPdf SUCCESS - {new FileInfo(outputPath).Length} bytes");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError($"DATA: JsonToPdf ERROR - {ex.Message}");
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        return result;
    }

    private static List<List<string>> JsonToTableRows(string json, string rootName)
    {
        var rows = new List<List<string>>();
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(json);
            FlattenJson(doc.RootElement, "", rows, rootName);
        }
        catch { rows.Add(new List<string> { "Error parsing JSON" }); }
        return rows;
    }

    private static void FlattenJson(System.Text.Json.JsonElement element, string prefix, List<List<string>> rows, string rootName)
    {
        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    string key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                    FlattenJson(prop.Value, key, rows, rootName);
                }
                break;

            case System.Text.Json.JsonValueKind.Array:
                int idx = 0;
                foreach (var item in element.EnumerateArray())
                {
                    string itemPrefix = $"{prefix}[{idx}]";
                    FlattenJson(item, itemPrefix, rows, rootName);
                    idx++;
                }
                break;

            case System.Text.Json.JsonValueKind.String:
                AddValue(rows, prefix, element.GetString() ?? "");
                break;

            case System.Text.Json.JsonValueKind.Number:
                AddValue(rows, prefix, element.GetRawText());
                break;

            case System.Text.Json.JsonValueKind.True:
                AddValue(rows, prefix, "true");
                break;

            case System.Text.Json.JsonValueKind.False:
                AddValue(rows, prefix, "false");
                break;

            case System.Text.Json.JsonValueKind.Null:
                AddValue(rows, prefix, "");
                break;
        }
    }

    private static void AddValue(List<List<string>> rows, string key, string value)
    {
        int existing = rows.FindIndex(r => r.Count > 0 && r[0] == key);
        if (existing >= 0)
            rows[existing].Add(value);
        else
            rows.Add(new List<string> { key, value });
    }

    public static bool IsExcelFile(string extension) => extension is ".xlsx" or ".xls";
    public static bool IsCsvFile(string extension) => extension == ".csv";
    public static bool IsJsonFile(string extension) => extension == ".json";
}