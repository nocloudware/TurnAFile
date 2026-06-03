using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace TurnAFile.Core.Services;

public class DocumentConverter
{
    private readonly ConversionLogger _logger;

    public DocumentConverter(ConversionLogger? logger = null)
    {
        _logger = logger ?? new ConversionLogger();
    }

    public async Task<ConversionResult> ConvertAsync(
        string inputPath,
        string outputPath,
        string targetFormat,
        CancellationToken cancellationToken)
    {
        var result = new ConversionResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInfo($"DOCUMENT: Starting conversion: {Path.GetFileName(inputPath)} -> {targetFormat.ToUpper()}");
        _logger.LogInfo($"DOCUMENT: Input file: {inputPath} (exists: {File.Exists(inputPath)})");
        _logger.LogInfo($"DOCUMENT: Output file: {outputPath}");

        // Verify input file exists
        if (!File.Exists(inputPath))
        {
            _logger.LogError($"DOCUMENT: Input file not found: {inputPath}");
            result.Success = false;
            result.ErrorMessage = $"Archivo de entrada no encontrado: {inputPath}";
            return result;
        }

        try
        {
            string inputExt = Path.GetExtension(inputPath).ToLower();
            string outputExt = Path.GetExtension(outputPath).ToLower();
            string target = targetFormat.ToLower();

            // DOCX -> TXT
            if (inputExt == ".docx" && target == "txt")
            {
                await ConvertDocxToTextAsync(inputPath, outputPath);
            }
            // DOCX -> HTML
            else if (inputExt == ".docx" && target == "html")
            {
                await ConvertDocxToHtmlAsync(inputPath, outputPath);
            }
            // DOCX -> Markdown
            else if (inputExt == ".docx" && (target == "md" || target == "markdown"))
            {
                await ConvertDocxToMarkdownAsync(inputPath, outputPath);
            }
            // DOCX -> PDF (preserve full formatting)
            else if (inputExt == ".docx" && target == "pdf")
            {
                DocumentPdfGenerator.RenderDocxToPdf(inputPath, outputPath);
            }
            // TXT -> DOCX
            else if (inputExt == ".txt" && target == "docx")
            {
                await ConvertTextToDocxAsync(inputPath, outputPath);
            }
            // HTML -> PDF (preserve formatting, headings, lists, etc.)
            else if ((inputExt == ".html" || inputExt == ".htm") && target == "pdf")
            {
                string html = await File.ReadAllTextAsync(inputPath);
                DocumentPdfGenerator.RenderHtmlToPdf(html, outputPath);
            }
            // HTML -> DOCX
            else if ((inputExt == ".html" || inputExt == ".htm") && target == "docx")
            {
                await ConvertHtmlToDocxAsync(inputPath, outputPath);
            }
            // HTML -> TXT
            else if ((inputExt == ".html" || inputExt == ".htm") && target == "txt")
            {
                string html = await File.ReadAllTextAsync(inputPath);
                string text = StripHtmlTags(html);
                await File.WriteAllTextAsync(outputPath, text, cancellationToken);
            }
            // Markdown -> DOCX
            else if ((inputExt == ".md" || inputExt == ".markdown") && target == "docx")
            {
                await ConvertMarkdownToDocxAsync(inputPath, outputPath);
            }
            // Markdown -> HTML
            else if ((inputExt == ".md" || inputExt == ".markdown") && target == "html")
            {
                string md = await File.ReadAllTextAsync(inputPath);
                string html = ConvertMarkdownToHtml(md);
                await File.WriteAllTextAsync(outputPath, html, cancellationToken);
            }
            // Markdown -> TXT
            else if ((inputExt == ".md" || inputExt == ".markdown") && target == "txt")
            {
                string md = await File.ReadAllTextAsync(inputPath);
                string text = StripMarkdown(md);
                await File.WriteAllTextAsync(outputPath, text, cancellationToken);
            }
            // TXT -> HTML
            else if (inputExt == ".txt" && target == "html")
            {
                string text = await File.ReadAllTextAsync(inputPath);
                string html = ConvertTextToHtml(text);
                await File.WriteAllTextAsync(outputPath, html, cancellationToken);
            }
            // TXT -> PDF
            else if (inputExt == ".txt" && target == "pdf")
            {
                string text = await File.ReadAllTextAsync(inputPath, cancellationToken);
                DocumentPdfGenerator.ConvertTextToPdf(text, outputPath);
            }
            // RTF -> TXT
            else if (inputExt == ".rtf" && target == "txt")
            {
                string rtf = await File.ReadAllTextAsync(inputPath);
                string text = ExtractTextFromRtf(rtf);
                await File.WriteAllTextAsync(outputPath, text, cancellationToken);
            }
            // RTF -> DOCX
            else if (inputExt == ".rtf" && target == "docx")
            {
                // For now, extract text then create DOCX
                string rtf = await File.ReadAllTextAsync(inputPath);
                string text = ExtractTextFromRtf(rtf);
                await CreateDocxFromTextAsync(outputPath, text);
            }
            // ODT -> TXT
            else if (inputExt == ".odt" && target == "txt")
            {
                string text = await ExtractTextFromOdtAsync(inputPath);
                await File.WriteAllTextAsync(outputPath, text, cancellationToken);
            }
            // EPUB -> TXT (basic extraction)
            else if (inputExt == ".epub" && target == "txt")
            {
                string text = await ExtractTextFromEpubAsync(inputPath);
                await File.WriteAllTextAsync(outputPath, text, cancellationToken);
            }
            // PDF -> TXT (text extraction using PdfPig)
            else if (inputExt == ".pdf" && target == "txt")
            {
                string text = await ExtractTextFromPdfAsync(inputPath);
                await File.WriteAllTextAsync(outputPath, text, cancellationToken);
            }
            // PDF -> DOCX (extract text then create DOCX)
            else if (inputExt == ".pdf" && target == "docx")
            {
                string text = await ExtractTextFromPdfAsync(inputPath);
                await CreateDocxFromTextAsync(outputPath, text);
            }
            // PDF -> HTML (extract text then wrap in HTML)
            else if (inputExt == ".pdf" && target == "html")
            {
                string text = await ExtractTextFromPdfAsync(inputPath);
                string html = ConvertTextToHtml(text);
                await File.WriteAllTextAsync(outputPath, html, cancellationToken);
            }
            // PDF -> MD (extract text then convert)
            else if (inputExt == ".pdf" && target == "md")
            {
                string text = await ExtractTextFromPdfAsync(inputPath);
                await File.WriteAllTextAsync(outputPath, text, cancellationToken);
            }
            // PDF -> EPUB (extract text then create EPUB)
            else if (inputExt == ".pdf" && target == "epub")
            {
                string text = await ExtractTextFromPdfAsync(inputPath);
                await CreateEpubAsync(outputPath, text, Path.GetFileNameWithoutExtension(inputPath));
            }
            // EPUB -> DOCX (extract text then create DOCX)
            else if (inputExt == ".epub" && target == "docx")
            {
                string text = await ExtractTextFromEpubAsync(inputPath);
                await CreateDocxFromTextAsync(outputPath, text);
            }
            // EPUB -> HTML (extract text then wrap in HTML)
            else if (inputExt == ".epub" && target == "html")
            {
                string text = await ExtractTextFromEpubAsync(inputPath);
                string html = ConvertTextToHtml(text);
                await File.WriteAllTextAsync(outputPath, html, cancellationToken);
            }
            // EPUB -> MD (extract text)
            else if (inputExt == ".epub" && (target == "md" || target == "markdown"))
            {
                string text = await ExtractTextFromEpubAsync(inputPath);
                await File.WriteAllTextAsync(outputPath, text, cancellationToken);
            }
            // EPUB -> PDF (extract XHTML → render HTML)
            else if (inputExt == ".epub" && target == "pdf")
            {
                string html = await ExtractHtmlFromEpubAsync(inputPath);
                DocumentPdfGenerator.RenderHtmlDocumentToPdf(html, outputPath);
            }
            // DOCX -> EPUB (generate simple EPUB from DOCX text)
            else if (inputExt == ".docx" && (target == "epub"))
            {
                string text = await ExtractTextFromDocxAsync(inputPath);
                await CreateEpubAsync(outputPath, text, Path.GetFileNameWithoutExtension(inputPath));
            }
            else
            {
                result.Success = false;
                result.ErrorMessage = $"Conversion no soportada: {inputExt} -> {targetFormat}";
                return result;
            }

            // Verify output was created
            if (File.Exists(outputPath))
            {
                result.Success = true;
                result.OutputPath = outputPath;
                _logger.LogInfo($"DOCUMENT: SUCCESS - {outputPath} ({new FileInfo(outputPath).Length} bytes)");
            }
            else
            {
                result.Success = false;
                result.ErrorMessage = "No se pudo generar el archivo de salida";
                _logger.LogError($"DOCUMENT: Output file not created: {outputPath}");
            }
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
            _logger.LogError($"DOCUMENT: ERROR - {ex.Message}");
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        return result;
    }

    #region DOCX Conversions

    private async Task<string> ExtractTextFromDocxAsync(string inputPath)
    {
        return await Task.Run(() =>
        {
            var text = new StringBuilder();
            using (WordprocessingDocument doc = WordprocessingDocument.Open(inputPath, false))
            {
                var body = doc.MainDocumentPart?.Document?.Body;
                if (body != null)
                {
                    foreach (var para in body.Elements<Paragraph>())
                    {
                        text.AppendLine(para.InnerText);
                    }
                }
            }
            return text.ToString().Trim();
        });
    }

    private async Task ConvertDocxToTextAsync(string inputPath, string outputPath)
    {
        string text = await ExtractTextFromDocxAsync(inputPath);
        await File.WriteAllTextAsync(outputPath, text);
    }

    private async Task<string> ConvertDocxToHtmlPreservingFormattingAsync(string inputPath)
    {
        return await Task.Run(() =>
        {
            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html><head><meta charset='UTF-8'></head><body>");

            using (WordprocessingDocument doc = WordprocessingDocument.Open(inputPath, false))
            {
                var body = doc.MainDocumentPart?.Document?.Body;
                if (body == null) return "<html><body><p>Empty document</p></body></html>";

                foreach (var element in body.Elements())
                {
                    if (element is Paragraph para)
                        html.AppendLine(FormatParagraphToHtml(para, doc));
                    else if (element is Table table)
                        html.AppendLine(FormatTableToHtml(table));
                    else if (element is OpenXmlUnknownElement)
                        html.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(element.InnerText)}</p>");
                }
            }

            html.AppendLine("</body></html>");
            return html.ToString();
        });
    }

    private static string FormatParagraphToHtml(Paragraph para, WordprocessingDocument doc)
    {
        var sb = new StringBuilder();
        var pPr = para.ParagraphProperties;
        var styleId = pPr?.ParagraphStyleId?.Val?.Value ?? "";

        string tag = "p";
        if (styleId.StartsWith("Heading") && int.TryParse(styleId.Replace("Heading", ""), out int level))
            tag = $"h{Math.Min(level, 6)}";

        bool isListItem = pPr?.NumberingProperties != null;
        if (isListItem) tag = "li";

        string align = "";
        if (pPr?.Justification?.Val?.Value != null)
            align = $" style='text-align:{pPr.Justification.Val.Value.ToString().ToLower()}'";

        if (isListItem) sb.Append("<ul>");
        sb.Append($"<{tag}{align}>");

        foreach (var run in para.Elements<Run>())
        {
            string text = System.Net.WebUtility.HtmlEncode(run.InnerText);
            var rPr = run.RunProperties;
            if (rPr?.Bold != null) text = $"<strong>{text}</strong>";
            if (rPr?.Italic != null) text = $"<em>{text}</em>";
            if (rPr?.Underline != null) text = $"<u>{text}</u>";
            if (rPr?.Strike != null) text = $"<s>{text}</s>";

            string styles = "";
            if (rPr?.Color?.Val?.Value != null) styles += $" color:#{rPr.Color.Val.Value};";
            if (rPr?.FontSize?.Val?.Value != null)
                styles += $" font-size:{int.Parse(rPr.FontSize.Val.Value) / 2}pt;";
            if (rPr?.RunFonts?.Ascii?.Value != null)
                styles += $" font-family:{rPr.RunFonts.Ascii.Value};";
            if (!string.IsNullOrEmpty(styles))
                text = $"<span style='{styles.Trim()}'>{text}</span>";

            if (run.Parent?.Parent is Hyperlink hyperlink && hyperlink.Id?.Value != null)
            {
                var rel = doc.MainDocumentPart?.HyperlinkRelationships
                    .FirstOrDefault(r => r.Id == hyperlink.Id.Value);
                if (rel?.Uri != null)
                    text = $"<a href='{System.Net.WebUtility.HtmlEncode(rel.Uri.AbsoluteUri)}'>{text}</a>";
            }

            sb.Append(text);
        }

        // Images
        foreach (var drawing in para.Descendants<Drawing>())
        {
            var blip = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().FirstOrDefault();
            if (blip?.Embed?.Value != null)
                sb.Append($"<p>[Image]</p>");
        }

        sb.Append($"</{tag}>");
        if (isListItem) sb.Append("</ul>");
        return sb.ToString();
    }

    private static string FormatTableToHtml(Table table)
    {
        var sb = new StringBuilder();
        sb.Append("<table border='1' cellpadding='4' cellspacing='0' style='border-collapse:collapse;width:100%'>");
        foreach (var row in table.Elements<TableRow>())
        {
            sb.Append("<tr>");
            foreach (var cell in row.Elements<TableCell>())
            {
                string text = string.Join(" ", cell.Elements<Paragraph>().Select(p => p.InnerText));
                sb.Append($"<td style='border:1px solid #ccc;padding:4px'>{System.Net.WebUtility.HtmlEncode(text)}</td>");
            }
            sb.Append("</tr>");
        }
        sb.Append("</table>");
        return sb.ToString();
    }

    private async Task ConvertDocxToHtmlAsync(string inputPath, string outputPath)
    {
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html><head><meta charset=\"UTF-8\"></head><body>");

        using (WordprocessingDocument doc = WordprocessingDocument.Open(inputPath, false))
        {
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body != null)
            {
                foreach (var para in body.Elements<Paragraph>())
                {
                    var runProperties = para.Elements<Run>().FirstOrDefault()?.RunProperties;
                    bool isBold = runProperties?.Bold != null;
                    bool isItalic = runProperties?.Italic != null;

                    // Check paragraph style for headings
                    var pStyle = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "";
                    string tag = "p";
                    if (pStyle.StartsWith("Heading") && int.TryParse(pStyle.Replace("Heading", ""), out int level))
                    {
                        tag = $"h{level}";
                    }

                    string text = System.Net.WebUtility.HtmlEncode(para.InnerText);
                    if (isBold) text = $"<strong>{text}</strong>";
                    if (isItalic) text = $"<em>{text}</em>";

                    html.AppendLine($"<{tag}>{text}</{tag}>");
                }
            }
        }

        html.AppendLine("</body></html>");
        await File.WriteAllTextAsync(outputPath, html.ToString());
    }

    private async Task ConvertDocxToMarkdownAsync(string inputPath, string outputPath)
    {
        var md = new StringBuilder();

        using (WordprocessingDocument doc = WordprocessingDocument.Open(inputPath, false))
        {
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body != null)
            {
                foreach (var para in body.Elements<Paragraph>())
                {
                    var pStyle = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "";
                    string text = para.InnerText;

                    if (pStyle.StartsWith("Heading") && int.TryParse(pStyle.Replace("Heading", ""), out int level))
                    {
                        md.AppendLine(new string('#', level) + " " + text);
                    }
                    else
                    {
                        md.AppendLine(text);
                    }
                    md.AppendLine();
                }
            }
        }

        await File.WriteAllTextAsync(outputPath, md.ToString());
    }

    #endregion

    #region TXT to DOCX

    private async Task ConvertTextToDocxAsync(string inputPath, string outputPath)
    {
        string text = await File.ReadAllTextAsync(inputPath);
        await CreateDocxFromTextAsync(outputPath, text);
    }

    private async Task CreateDocxFromTextAsync(string outputPath, string text)
    {
        await Task.Run(() =>
        {
            using (WordprocessingDocument doc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = mainPart.Document.AppendChild(new Body());

                var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    var para = new Paragraph();

                    // Check for heading markers
                    if (trimmedLine.StartsWith("### "))
                    {
                        var run = new Run(new Text(trimmedLine[4..]));
                        run.RunProperties = new RunProperties(new Bold(), new FontSize { Val = "24" });
                        para.Append(run);
                    }
                    else if (trimmedLine.StartsWith("## "))
                    {
                        var run = new Run(new Text(trimmedLine[3..]));
                        run.RunProperties = new RunProperties(new Bold(), new FontSize { Val = "28" });
                        para.Append(run);
                    }
                    else if (trimmedLine.StartsWith("# "))
                    {
                        var run = new Run(new Text(trimmedLine[2..]));
                        run.RunProperties = new RunProperties(new Bold(), new FontSize { Val = "36" });
                        para.Append(run);
                    }
                    else
                    {
                        var run = new Run(new Text(trimmedLine));
                        para.Append(run);
                    }

                    body.Append(para);
                }

                mainPart.Document.Save();
            }
        });
    }

    #endregion

    #region HTML Conversions

    private async Task ConvertHtmlToDocxAsync(string inputPath, string outputPath)
    {
        string html = await File.ReadAllTextAsync(inputPath);
        string text = StripHtmlTags(html);
        await CreateDocxFromTextAsync(outputPath, text);
    }

    private static string StripHtmlTags(string html)
    {
        // Remove script and style content
        html = Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<style[^>]*>[\s\S]*?</style>", "", RegexOptions.IgnoreCase);
        // Replace common block elements with newlines
        html = Regex.Replace(html, @"</?(p|div|br|h[1-6]|li|tr|table)[^>]*>", "\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"</?(b|strong)[^>]*>", "", RegexOptions.IgnoreCase); // Bold markers, keep text
        html = Regex.Replace(html, @"</?(i|em)[^>]*>", "", RegexOptions.IgnoreCase); // Italic markers
        // Remove remaining tags
        html = Regex.Replace(html, @"<[^>]+>", "");
        // Decode HTML entities
        html = System.Net.WebUtility.HtmlDecode(html);
        // Collapse multiple newlines
        html = Regex.Replace(html, @"\n{3,}", "\n\n");
        return html.Trim();
    }

    private static string ConvertTextToHtml(string text)
    {
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html><head><meta charset=\"UTF-8\"></head><body>");

        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                html.AppendLine("<br>");
            }
            else
            {
                html.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(trimmed)}</p>");
            }
        }

        html.AppendLine("</body></html>");
        return html.ToString();
    }

    #endregion

    #region Markdown Conversions

    private async Task ConvertMarkdownToDocxAsync(string inputPath, string outputPath)
    {
        string md = await File.ReadAllTextAsync(inputPath);
        await Task.Run(() =>
        {
            using (WordprocessingDocument doc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = mainPart.Document.AppendChild(new Body());

                var lines = md.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    var para = new Paragraph();

                    if (trimmedLine.StartsWith("###### "))
                    {
                        var run = new Run(new Text(trimmedLine[7..]));
                        run.RunProperties = new RunProperties(new Bold(), new FontSize { Val = "16" });
                        para.Append(run);
                    }
                    else if (trimmedLine.StartsWith("##### "))
                    {
                        var run = new Run(new Text(trimmedLine[6..]));
                        run.RunProperties = new RunProperties(new Bold(), new FontSize { Val = "18" });
                        para.Append(run);
                    }
                    else if (trimmedLine.StartsWith("#### "))
                    {
                        var run = new Run(new Text(trimmedLine[5..]));
                        run.RunProperties = new RunProperties(new Bold(), new FontSize { Val = "20" });
                        para.Append(run);
                    }
                    else if (trimmedLine.StartsWith("### "))
                    {
                        var run = new Run(new Text(trimmedLine[4..]));
                        run.RunProperties = new RunProperties(new Bold(), new FontSize { Val = "24" });
                        para.Append(run);
                    }
                    else if (trimmedLine.StartsWith("## "))
                    {
                        var run = new Run(new Text(trimmedLine[3..]));
                        run.RunProperties = new RunProperties(new Bold(), new FontSize { Val = "28" });
                        para.Append(run);
                    }
                    else if (trimmedLine.StartsWith("# "))
                    {
                        var run = new Run(new Text(trimmedLine[2..]));
                        run.RunProperties = new RunProperties(new Bold(), new FontSize { Val = "36" });
                        para.Append(run);
                    }
                    else
                    {
                        var run = new Run(new Text(trimmedLine));
                        para.Append(run);
                    }

                    body.Append(para);
                }

                mainPart.Document.Save();
            }
        });
    }

    private static string ConvertMarkdownToHtml(string md)
    {
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html><head><meta charset=\"UTF-8\"></head><body>");

        var lines = md.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        bool inCodeBlock = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("```"))
            {
                if (inCodeBlock)
                {
                    html.AppendLine("</pre>");
                    inCodeBlock = false;
                }
                else
                {
                    html.AppendLine("<pre><code>");
                    inCodeBlock = true;
                }
                continue;
            }

            if (inCodeBlock)
            {
                html.AppendLine(System.Net.WebUtility.HtmlEncode(line));
                continue;
            }

            if (trimmed.StartsWith("###### "))
                html.AppendLine($"<h6>{System.Net.WebUtility.HtmlEncode(trimmed[7..])}</h6>");
            else if (trimmed.StartsWith("##### "))
                html.AppendLine($"<h5>{System.Net.WebUtility.HtmlEncode(trimmed[6..])}</h5>");
            else if (trimmed.StartsWith("#### "))
                html.AppendLine($"<h4>{System.Net.WebUtility.HtmlEncode(trimmed[5..])}</h4>");
            else if (trimmed.StartsWith("### "))
                html.AppendLine($"<h3>{System.Net.WebUtility.HtmlEncode(trimmed[4..])}</h3>");
            else if (trimmed.StartsWith("## "))
                html.AppendLine($"<h2>{System.Net.WebUtility.HtmlEncode(trimmed[3..])}</h2>");
            else if (trimmed.StartsWith("# "))
                html.AppendLine($"<h1>{System.Net.WebUtility.HtmlEncode(trimmed[2..])}</h1>");
            else if (string.IsNullOrEmpty(trimmed))
                html.AppendLine("<br>");
            else
                html.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(trimmed)}</p>");
        }

        if (inCodeBlock) html.AppendLine("</pre>");

        html.AppendLine("</body></html>");
        return html.ToString();
    }

    private static string StripMarkdown(string md)
    {
        // Remove code blocks
        md = Regex.Replace(md, @"```[\s\S]*?```", "");
        // Remove inline code markers
        md = Regex.Replace(md, @"`([^`]+)`", "$1");
        // Remove heading markers
        md = Regex.Replace(md, @"^#{1,6}\s+", "", RegexOptions.Multiline);
        // Remove bold/italic markers
        md = Regex.Replace(md, @"\*{1,3}([^*]+)\*{1,3}", "$1");
        md = Regex.Replace(md, @"_{1,3}([^_]+)_{1,3}", "$1");
        // Remove links
        md = Regex.Replace(md, @"\[([^\]]+)\]\([^)]+\)", "$1");
        // Remove images
        md = Regex.Replace(md, @"!\[([^\]]*)\]\([^)]+\)", "$1");
        return md.Trim();
    }

    #endregion

    #region RTF Conversion

    private static string ExtractTextFromRtf(string rtf)
    {
        // Basic RTF text extraction - removes RTF control words
        var result = new StringBuilder();
        bool inControl = false;
        bool inGroup = false;
        var skipStack = new System.Collections.Generic.Stack<bool>();

        foreach (char c in rtf)
        {
            if (c == '{')
            {
                skipStack.Push(inGroup);
                inGroup = true;
                inControl = false;
                continue;
            }
            if (c == '}')
            {
                if (skipStack.Count > 0)
                    inGroup = skipStack.Pop();
                inControl = false;
                continue;
            }
            if (c == '\\')
            {
                inControl = true;
                continue;
            }
            if (inControl)
            {
                if (char.IsLetter(c) || c == '*' || c == '-' || char.IsDigit(c))
                    continue;
                inControl = false;
                if (c == ' ')
                    continue;
            }
            if (!inGroup && !inControl)
            {
                result.Append(c);
            }
        }

        return result.ToString().Trim();
    }

    #endregion

    #region ODT Conversion

    private async Task<string> ExtractTextFromOdtAsync(string inputPath)
    {
        // ODT is a ZIP file containing content.xml
        // Simple extraction using System.IO.Compression
        return await Task.Run(() =>
        {
            try
            {
                using (var archive = System.IO.Compression.ZipFile.OpenRead(inputPath))
                {
                    var contentEntry = archive.GetEntry("content.xml");
                    if (contentEntry == null)
                        return string.Empty;

                    using var stream = contentEntry.Open();
                    using var reader = new StreamReader(stream);
                    string xml = reader.ReadToEnd();

                    // Extract text from XML tags
                    return Regex.Replace(xml, @"<[^>]+>", " ")
                        .Replace("&lt;", "<")
                        .Replace("&gt;", ">")
                        .Replace("&amp;", "&")
                        .Replace("&quot;", "\"")
                        .Replace("&apos;", "'")
                        .Trim();
                }
            }
            catch
            {
                return string.Empty;
            }
        });
    }

    #endregion

    #region EPUB Conversion

    private async Task<string> ExtractTextFromEpubAsync(string inputPath)
    {
        // EPUB is a ZIP file containing XHTML files
        return await Task.Run(() =>
        {
            try
            {
                using (var archive = System.IO.Compression.ZipFile.OpenRead(inputPath))
                {
                    var text = new StringBuilder();
                    // Get all XHTML files from OEBPS or EPUB folder
                    var xhtmlEntries = archive.Entries
                        .Where(e => e.FullName.EndsWith(".xhtml") || e.FullName.EndsWith(".html"))
                        .OrderBy(e => e.FullName);

                    foreach (var entry in xhtmlEntries)
                    {
                        using var stream = entry.Open();
                        using var reader = new StreamReader(stream);
                        string html = reader.ReadToEnd();
                        string extracted = StripHtmlTags(html);
                        if (!string.IsNullOrWhiteSpace(extracted))
                        {
                            text.AppendLine(extracted);
                            text.AppendLine();
                        }
                    }

                    return text.ToString().Trim();
                }
            }
            catch
            {
                return string.Empty;
            }
        });
    }

    private async Task<string> ExtractHtmlFromEpubAsync(string inputPath)
    {
        return await Task.Run(() =>
        {
            try
            {
                using (var archive = ZipFile.OpenRead(inputPath))
                {
                    var merged = new StringBuilder();
                    merged.AppendLine("<!DOCTYPE html><html><head><meta charset='UTF-8'></head><body>");

                    var xhtmlEntries = archive.Entries
                        .Where(e => e.FullName.EndsWith(".xhtml") || e.FullName.EndsWith(".html"))
                        .OrderBy(e => e.FullName);

                    foreach (var entry in xhtmlEntries)
                    {
                        using var stream = entry.Open();
                        using var reader = new StreamReader(stream);
                        string content = reader.ReadToEnd();

                        // Extract body content
                        var bodyMatch = Regex.Match(content, @"<body[^>]*>(.*?)</body>",
                            RegexOptions.IgnoreCase | RegexOptions.Singleline);
                        if (bodyMatch.Success)
                            merged.AppendLine(bodyMatch.Groups[1].Value);
                    }

                    merged.AppendLine("</body></html>");
                    return merged.ToString();
                }
            }
            catch { return "<html><body><p>Error extracting EPUB</p></body></html>"; }
        });
    }

    #endregion

    #region PDF Text Extraction

    private async Task<string> ExtractTextFromPdfAsync(string inputPath)
    {
        return await Task.Run(() =>
        {
            var text = new StringBuilder();
            try
            {
                using (var pdf = PdfDocument.Open(inputPath))
                {
                    foreach (var page in pdf.GetPages())
                    {
                        var pageText = page.Text;
                        if (!string.IsNullOrWhiteSpace(pageText))
                        {
                            text.AppendLine(pageText);
                            text.AppendLine();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error extrayendo texto del PDF: {ex.Message}", ex);
            }
            return text.ToString().Trim();
        });
    }

    #endregion

    #region EPUB Generation

    private async Task CreateEpubAsync(string outputPath, string textContent, string title)
    {
        await Task.Run(() =>
        {
            // EPUB is essentially a ZIP file with specific structure
            using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);

            // mimetype file (must be first, uncompressed)
            var mimetypeEntry = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (var writer = new StreamWriter(mimetypeEntry.Open()))
            {
                writer.Write("application/epub+zip");
            }

            // META-INF/container.xml
            var containerEntry = archive.CreateEntry("META-INF/container.xml");
            using (var writer = new StreamWriter(containerEntry.Open()))
            {
                writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<container version=""1.0"" xmlns=""urn:oasis:names:tc:opendocument:xmlns:container"">
  <rootfiles>
    <rootfile full-path=""OEBPS/content.xhtml"" media-type=""application/xhtml+xml""/>
  </rootfiles>
</container>");
            }

            // OEBPS/content.opf (package file)
            var opfEntry = archive.CreateEntry("OEBPS/content.opf");
            using (var writer = new StreamWriter(opfEntry.Open()))
            {
                writer.Write($@"<?xml version=""1.0"" encoding=""UTF-8""?>
<package version=""3.0"" xmlns=""http://www.idpf.org/2007/opf"" unique-identifier=""uid"">
  <metadata xmlns:dc=""http://purl.org/dc/elements/1.1/"">
    <dc:identifier id=""uid"">urn:uuid:{Guid.NewGuid()}</dc:identifier>
    <dc:title>{System.Net.WebUtility.HtmlEncode(title)}</dc:title>
    <dc:language>es</dc:language>
    <meta property=""dcterms:modified"">{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss}Z</meta>
  </metadata>
  <manifest>
    <item id=""content"" href=""content.xhtml"" media-type=""application/xhtml+xml""/>
    <item id=""nav"" href=""nav.xhtml"" media-type=""application/xhtml+xml"" properties=""nav""/>
  </manifest>
  <spine>
    <itemref idref=""content""/>
  </spine>
</package>");
            }

            // OEBPS/nav.xhtml (navigation)
            var navEntry = archive.CreateEntry("OEBPS/nav.xhtml");
            using (var writer = new StreamWriter(navEntry.Open()))
            {
                writer.Write($@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE html>
<html xmlns=""http://www.w3.org/1999/xhtml"" xmlns:epub=""http://www.idpf.org/2007/ops"">
<head><title>{System.Net.WebUtility.HtmlEncode(title)}</title></head>
<body>
<nav epub:type=""toc"">
  <h1>Tabla de contenido</h1>
  <ol><li><a href=""content.xhtml"">{System.Net.WebUtility.HtmlEncode(title)}</a></li></ol>
</nav>
</body>
</html>");
            }

            // OEBPS/content.xhtml (main content)
            var contentEntry = archive.CreateEntry("OEBPS/content.xhtml");
            using (var writer = new StreamWriter(contentEntry.Open()))
            {
                writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE html>
<html xmlns=""http://www.w3.org/1999/xhtml"">
<head><title>Content</title>
<style>
body { font-family: sans-serif; margin: 2em; line-height: 1.6; }
h1 { font-size: 1.8em; margin-top: 1em; }
h2 { font-size: 1.4em; margin-top: 0.8em; }
h3 { font-size: 1.2em; margin-top: 0.6em; }
p { margin: 0.5em 0; }
</style>
</head>
<body>
");
                var lines = textContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed))
                    {
                        writer.Write("<br/>\n");
                        continue;
                    }
                    if (trimmed.StartsWith("### "))
                        writer.Write($"<h3>{System.Net.WebUtility.HtmlEncode(trimmed[4..])}</h3>\n");
                    else if (trimmed.StartsWith("## "))
                        writer.Write($"<h2>{System.Net.WebUtility.HtmlEncode(trimmed[3..])}</h2>\n");
                    else if (trimmed.StartsWith("# "))
                        writer.Write($"<h1>{System.Net.WebUtility.HtmlEncode(trimmed[2..])}</h1>\n");
                    else
                        writer.Write($"<p>{System.Net.WebUtility.HtmlEncode(trimmed)}</p>\n");
                }

                writer.Write(@"</body>
</html>");
            }
        });
    }

    #endregion

    public static bool IsSupported(string extension)
    {
        return extension.ToLower() switch
        {
            ".docx" or ".pdf" or ".md" or ".markdown" or ".html" or ".htm" or ".epub" or ".txt" => true,
            _ => false
        };
    }
}
