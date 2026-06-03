using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using A = DocumentFormat.OpenXml.Drawing;

namespace TurnAFile.Core.Services;

public static class DocumentPdfGenerator
{
    static DocumentPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static void ConvertTextToPdf(string textContent, string outputPath)
    {
        QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(50);
                page.DefaultTextStyle(x => x.FontSize(10));
                page.Content().Column(column =>
                {
                    foreach (var rawLine in textContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
                    {
                        var trimmed = rawLine.Trim();
                        if (string.IsNullOrEmpty(trimmed))
                        {
                            column.Item().PaddingVertical(3);
                            continue;
                        }
                        var leadingSpaces = rawLine.Length - rawLine.TrimStart().Length;
                        var indent = new string(' ', leadingSpaces);
                        if (trimmed.StartsWith("### "))
                            column.Item().PaddingTop(8).Text(indent + trimmed[4..]).FontSize(12).Bold();
                        else if (trimmed.StartsWith("## "))
                            column.Item().PaddingTop(12).Text(indent + trimmed[3..]).FontSize(14).Bold();
                        else if (trimmed.StartsWith("# "))
                            column.Item().PaddingTop(16).Text(indent + trimmed[2..]).FontSize(18).Bold();
                        else
                            column.Item().PaddingTop(2).Text(rawLine);
                    }
                });
            });
        }).GeneratePdf(outputPath);
    }

    public static void ConvertImageToPdf(string imagePath, string outputPath)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException($"Image not found: {imagePath}");

        QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.Content().AlignCenter().AlignMiddle().Image(imagePath).FitArea();
            });
        }).GeneratePdf(outputPath);
    }

    public static void ConvertTableToPdf(
        List<List<string>> rows,
        string outputPath,
        string? title = null,
        bool landscape = true)
    {
        if (rows == null || rows.Count == 0) return;

        int totalCols = rows.Max(r => r.Count);
        if (totalCols == 0) return;

        foreach (var row in rows)
            while (row.Count < totalCols) row.Add("");

        QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Content().Column(column =>
                {
                    if (!string.IsNullOrEmpty(title))
                    {
                        column.Item().Text(title).FontSize(14).Bold();
                        column.Item().PaddingBottom(8);
                    }

                    var layout = CalculateTableLayout(rows, landscape);
                    int fontSize = layout.FontSize;

                    for (int groupStart = 0; groupStart < totalCols; groupStart += layout.ColumnsPerPage)
                    {
                        int groupEnd = Math.Min(groupStart + layout.ColumnsPerPage, totalCols);

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                for (int ci = groupStart; ci < groupEnd; ci++)
                                    c.RelativeColumn();
                            });

                            table.Header(header =>
                            {
                                for (int ci = groupStart; ci < groupEnd; ci++)
                                {
                                    header.Cell().Background(Colors.Grey.Lighten2).Padding(4)
                                        .AlignCenter().Text(rows[0][ci]).FontSize(fontSize).Bold();
                                }
                            });

                            for (int r = 1; r < rows.Count; r++)
                            {
                                for (int ci = groupStart; ci < groupEnd; ci++)
                                {
                                    string cellText = ci < rows[r].Count ? rows[r][ci] : "";
                                    bool isNumeric = double.TryParse(cellText, out _);
                                    var cell = table.Cell().Padding(3);
                                    if (isNumeric)
                                        cell.AlignRight().Text(cellText).FontSize(fontSize);
                                    else
                                        cell.AlignLeft().Text(cellText).FontSize(fontSize);
                                }
                            }
                        });

                        if (groupEnd < totalCols)
                        {
                            column.Item().PaddingTop(6).Text("(continúa...)")
                                .FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
                            column.Item().PageBreak();
                        }
                    }
                });
            });
        }).GeneratePdf(outputPath);
    }

    private static (int FontSize, int ColumnsPerPage) CalculateTableLayout(
        List<List<string>> rows, bool landscape)
    {
        double usableWidth = (landscape ? 807 : 535) - 60;
        int totalCols = rows.Max(r => r.Count);

        double[] colWidths = new double[totalCols];
        int sample = Math.Min(rows.Count, 50);
        for (int c = 0; c < totalCols; c++)
        {
            double max = 0;
            for (int r = 0; r < sample; r++)
                if (c < rows[r].Count) max = Math.Max(max, rows[r][c].Length);
            colWidths[c] = max;
        }

        double totalChars = colWidths.Sum();

        for (int fs = 10; fs >= 6; fs--)
        {
            double charW = fs * 0.6;
            double pad = fs * 1.2;
            double need = (totalChars * charW) + (totalCols * pad * 2);
            if (need <= usableWidth) return (fs, totalCols);
        }

        for (int fs = 10; fs >= 6; fs--)
        {
            double charW = fs * 0.6;
            double pad = fs * 1.2;
            int cols = 0;
            double cur = 0;
            for (int c = 0; c < totalCols; c++)
            {
                double cw = (colWidths[c] * charW) + (pad * 2);
                if (cur + cw > usableWidth && cols > 0) break;
                cur += cw;
                cols++;
            }
            if (cols > 0) return (fs, cols);
        }

        return (6, Math.Max(1, totalCols / 10));
    }

    public static string SanitiseFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    public static void RenderHtmlToPdf(string html, string outputPath)
    {
        var lines = html.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        int nonEmpty = lines.Count(l => !string.IsNullOrEmpty(l.Trim()));
        if (nonEmpty == 0) return;

        QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(50);
                page.DefaultTextStyle(x => x.FontFamily("Consolas").FontSize(9));
                page.Content().Column(column =>
                {
                    foreach (var rawLine in lines)
                    {
                        var trimmed = rawLine.Trim();
                        if (string.IsNullOrEmpty(trimmed))
                        {
                            column.Item().PaddingVertical(2);
                            continue;
                        }
                        int leadingSpaces = rawLine.Length - rawLine.TrimStart().Length;
                        var indent = new string(' ', leadingSpaces);

                        column.Item().PaddingTop(0.5f).Text(t =>
                        {
                            if (leadingSpaces > 0)
                                t.Span(indent).FontColor(Colors.Grey.Lighten2);
                            ColorizeHtml(t, trimmed);
                        });
                    }
                });
            });
        }).GeneratePdf(outputPath);
    }

    public static void RenderHtmlDocumentToPdf(string html, string outputPath)
    {
        string text = ConvertHtmlToPlainText(html);
        ConvertTextToPdf(text, outputPath);
    }

    private static string ConvertHtmlToPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        html = Regex.Replace(html, @"<script[^>]*>.*?</script>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        html = Regex.Replace(html, @"<style[^>]*>.*?</style>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        html = Regex.Replace(html, @"<(br|hr)[^>]*/?>", "\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<(p|div|h[1-6]|li|tr)[^>]*>", "\n\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"</(p|div|h[1-6]|li|tr)>", "\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<[^>]+>", "");
        html = System.Net.WebUtility.HtmlDecode(html);
        html = Regex.Replace(html, @"[ \t]+", " ");
        html = Regex.Replace(html, @"\n[ \t]+", "\n");
        html = Regex.Replace(html, @"\n{3,}", "\n\n");
        return html.Trim();
    }

    private static void ColorizeHtml(TextDescriptor t, string line)
    {
        // Comment
        var commentMatch = Regex.Match(line, @"^(<!--.*?-->)");
        if (commentMatch.Success)
        {
            t.Span(commentMatch.Groups[1].Value).FontColor(Colors.Grey.Medium).Italic();
            line = line[commentMatch.Length..].TrimStart();
            if (!string.IsNullOrEmpty(line)) ColorizeHtml(t, line);
            return;
        }

        // Opening tag with attributes: <tagname attr="val">
        var tagMatch = Regex.Match(line, @"^(</?[\w-]+)");
        if (tagMatch.Success)
        {
            t.Span(tagMatch.Groups[1].Value).FontColor("#569CD6").Bold();
            line = line[tagMatch.Length..];
            // Parse attributes until >
            while (line.Length > 0 && line[0] != '>')
            {
                var wsMatch = Regex.Match(line, @"^(\s+)");
                if (wsMatch.Success)
                {
                    t.Span(wsMatch.Groups[1].Value);
                    line = line[wsMatch.Length..];
                    continue;
                }
                if (line[0] == '>')
                {
                    t.Span(">").FontColor("#569CD6");
                    return;
                }
                if (line.StartsWith("/>"))
                {
                    t.Span("/>").FontColor("#569CD6");
                    return;
                }
                // Attribute name
                var attrName = Regex.Match(line, @"^([\w:-]+)(=?)");
                if (attrName.Success)
                {
                    t.Span(attrName.Groups[1].Value).FontColor("#9CDCFE");
                    line = line[attrName.Length..];
                    if (attrName.Groups[2].Value == "=")
                    {
                        t.Span("=").FontColor(Colors.White);
                        // Attribute value
                        var valMatch = Regex.Match(line, @"^(""[^""]*""|'[^']*')");
                        if (valMatch.Success)
                        {
                            t.Span(valMatch.Groups[1].Value).FontColor("#CE9178");
                            line = line[valMatch.Length..];
                        }
                    }
                }
                else
                {
                    t.Span(line[0].ToString());
                    line = line[1..];
                }
            }
            if (line.Length > 0 && line[0] == '>')
            {
                t.Span(">").FontColor("#569CD6");
                line = line[1..];
                if (!string.IsNullOrEmpty(line)) ColorizeHtml(t, line);
            }
            return;
        }

        // Closing tag only: </tagname>
        var closeOnly = Regex.Match(line, @"^(</[\w-]+>)");
        if (closeOnly.Success)
        {
            t.Span(closeOnly.Groups[1].Value).FontColor("#569CD6").Bold();
            line = line[closeOnly.Length..];
            if (!string.IsNullOrEmpty(line)) ColorizeHtml(t, line);
            return;
        }

        // Text content between tags
        var textMatch = Regex.Match(line, @"^([^<]+)");
        if (textMatch.Success)
        {
            t.Span(textMatch.Groups[1].Value);
            line = line[textMatch.Length..];
            if (!string.IsNullOrEmpty(line)) ColorizeHtml(t, line);
            return;
        }

        // Remaining < that didn't match as tag
        if (line.Length > 0)
        {
            t.Span(line[0].ToString());
            if (line.Length > 1) ColorizeHtml(t, line[1..]);
        }
    }

    private static void CleanupTempImages(List<string> paths)
    {
        foreach (var p in paths)
        {
            try { if (File.Exists(p)) File.Delete(p); } catch { }
        }
    }

    public static void RenderDocxToPdf(string docxPath, string outputPath)
    {
        if (!File.Exists(docxPath))
            throw new FileNotFoundException($"DOCX not found: {docxPath}");

        using var doc = WordprocessingDocument.Open(docxPath, false);
        var mainPart = doc.MainDocumentPart ?? throw new InvalidOperationException("DOCX has no MainDocumentPart");
        var body = mainPart.Document?.Body ?? throw new InvalidOperationException("DOCX has no body");

        var styleResolver = new DocxStyleResolver(mainPart);
        var tempImages = new List<string>();

        try
        {
            QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(50);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Calibri"));
                    page.Content().Column(column =>
                    {
                        foreach (var element in body.Elements())
                        {
                            if (element is Paragraph p)
                                RenderDocxParagraph(column, p, mainPart, styleResolver, tempImages);
                            else if (element is Table t)
                                RenderDocxTable(column, t, styleResolver, mainPart);
                        }
                    });
                });
            }).GeneratePdf(outputPath);
        }
        finally
        {
            CleanupTempImages(tempImages);
        }
    }

    private static void RenderDocxParagraph(
        ColumnDescriptor column,
        Paragraph para,
        MainDocumentPart mainPart,
        DocxStyleResolver styles,
        List<string> tempImages)
    {
        var pPr = para.ParagraphProperties;
        var styleId = pPr?.ParagraphStyleId?.Val?.Value ?? "";
        var resolved = styles.ResolveForParagraph(styleId);

        bool isList = pPr?.NumberingProperties != null;
        int numId = pPr?.NumberingProperties?.NumberingId?.Val?.Value ?? 0;
        bool isNumbered = isList && styles.IsNumberedList(numId);

        bool hasPageBreak = para.Descendants<Break>().Any(b => b.Type != null && b.Type.Value == BreakValues.Page);
        if (hasPageBreak)
            column.Item().PageBreak();

        var text = CollectParagraphText(para);
        if (string.IsNullOrEmpty(text) && !para.Descendants<A.Blip>().Any())
            return;

        if (isList)
        {
            string marker = isNumbered ? "1. " : "• ";
            column.Item().PaddingTop(2).Row(row =>
            {
                row.AutoItem().Text(marker);
                row.RelativeItem().Text(t => RenderRunsInText(t, para, mainPart, styles, resolved));
            });
            RenderDocxParagraphImages(column, para, mainPart, tempImages);
            return;
        }

        column.Item().PaddingTop((float)resolved.SpaceBefore).Text(t => RenderRunsInText(t, para, mainPart, styles, resolved));
        RenderDocxParagraphImages(column, para, mainPart, tempImages);
    }

    private static string CollectParagraphText(Paragraph para)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var run in para.Elements<Run>())
            sb.Append(run.InnerText);
        return sb.ToString();
    }

    private static void RenderDocxParagraphImages(
        ColumnDescriptor column,
        Paragraph para,
        MainDocumentPart mainPart,
        List<string> tempImages)
    {
        foreach (var blip in para.Descendants<A.Blip>())
        {
            if (blip?.Embed?.Value == null) continue;
            try
            {
                var rel = mainPart.GetPartById(blip.Embed.Value!) as ImagePart;
                if (rel == null) continue;
                var tempPath = ExtractImageToTemp(rel);
                if (tempPath == null) continue;
                tempImages.Add(tempPath);
                column.Item().PaddingTop(4).Image(tempPath).FitArea();
            }
            catch { continue; }
        }
    }

    private static void RenderRunsInText(
        TextDescriptor t,
        Paragraph para,
        MainDocumentPart mainPart,
        DocxStyleResolver styles,
        DocxStyleResolver.ResolvedStyle paragraphStyle)
    {
        t.DefaultTextStyle(s => s
            .FontFamily(paragraphStyle.FontFamily)
            .FontSize((float)paragraphStyle.FontSize));

        if (paragraphStyle.Alignment == "center") t.AlignCenter();
        else if (paragraphStyle.Alignment == "right") t.AlignRight();

        foreach (var child in para.ChildElements)
        {
            if (child is Run run)
                RenderDocxRun(t, run, styles, paragraphStyle);
            else if (child is Hyperlink hyperlink)
            {
                var linkRuns = hyperlink.Elements<Run>().ToList();
                var linkText = string.Concat(linkRuns.Select(r => r.InnerText));
                var url = ResolveHyperlinkUrl(hyperlink, mainPart);
                if (!string.IsNullOrEmpty(url))
                    t.Hyperlink(url, linkText).FontColor(Colors.Blue.Medium).Underline();
                else
                    t.Span(linkText);
            }
        }
    }

    private static void RenderDocxRun(
        TextDescriptor t,
        Run run,
        DocxStyleResolver styles,
        DocxStyleResolver.ResolvedStyle paragraphStyle)
    {
        var text = run.InnerText;
        if (string.IsNullOrEmpty(text)) return;

        var rPr = run.RunProperties;
        var resolved = styles.ResolveForRun(rPr, paragraphStyle);

        var span = t.Span(text);
        if (resolved.Bold) span.Bold();
        if (resolved.Italic) span.Italic();
        if (resolved.Underline) span.Underline();
        if (resolved.Strike) span.Strikethrough();
        if (!string.IsNullOrEmpty(resolved.Color)) span.FontColor(resolved.Color);
        if (resolved.FontSize != paragraphStyle.FontSize) span.FontSize((float)resolved.FontSize);
        if (!string.IsNullOrEmpty(resolved.FontFamily) && resolved.FontFamily != paragraphStyle.FontFamily)
            span.FontFamily(resolved.FontFamily);
        if (resolved.Highlight) span.BackgroundColor(resolved.HighlightColor);
    }

    private static string? ResolveHyperlinkUrl(Hyperlink hyperlink, MainDocumentPart mainPart)
    {
        var relId = hyperlink.Id?.Value;
        if (string.IsNullOrEmpty(relId)) return null;
        var rel = mainPart.HyperlinkRelationships.FirstOrDefault(r => r.Id == relId);
        return rel?.Uri?.AbsoluteUri;
    }



    private static void RenderDocxTable(
        ColumnDescriptor column,
        Table table,
        DocxStyleResolver styleResolver,
        MainDocumentPart mainPart)
    {
        var rows = table.Elements<TableRow>().ToList();
        if (rows.Count == 0) return;

        int totalCols = rows.Max(r => r.Elements<TableCell>().Count());
        if (totalCols == 0) return;

        column.Item().PaddingTop(6).Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                for (int i = 0; i < totalCols; i++) c.RelativeColumn();
            });

            bool isFirst = true;
            foreach (var row in rows)
            {
                foreach (var cell in row.Elements<TableCell>())
                {
                    var cellParas = cell.Elements<Paragraph>().ToList();
                    if (isFirst)
                    {
                        t.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(4)
                            .Background(Colors.Grey.Lighten3)
                            .Column(col =>
                            {
                                foreach (var p in cellParas)
                                    RenderCellParagraph(col, p, styleResolver, mainPart, isHeader: true);
                            });
                    }
                    else
                    {
                        t.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(4)
                            .Column(col =>
                            {
                                foreach (var p in cellParas)
                                    RenderCellParagraph(col, p, styleResolver, mainPart, isHeader: false);
                            });
                    }
                }
                isFirst = false;
            }
        });
    }

    private static void RenderCellParagraph(ColumnDescriptor col, Paragraph para, DocxStyleResolver styles, MainDocumentPart mainPart, bool isHeader)
    {
        var pPr = para.ParagraphProperties;
        var styleId = pPr?.ParagraphStyleId?.Val?.Value ?? "";
        var resolved = styles.ResolveForParagraph(styleId);
        if (isHeader) resolved.Bold = true;

        col.Item().Text(t => RenderRunsInText(t, para, mainPart, styles, resolved));
    }

    private static string? ExtractImageToTemp(ImagePart part)
    {
        try
        {
            var ext = part.ContentType switch
            {
                "image/png" => "png",
                "image/jpeg" => "jpg",
                "image/gif" => "gif",
                "image/bmp" => "bmp",
                "image/tiff" => "tiff",
                _ => "img"
            };
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.{ext}");
            using var fs = File.Create(path);
            using var stream = part.GetStream();
            stream.CopyTo(fs);
            return path;
        }
        catch { return null; }
    }
}

internal class DocxStyleResolver
{
    private readonly MainDocumentPart _mainPart;
    private readonly Dictionary<string, Style> _styles = new(StringComparer.OrdinalIgnoreCase);
    private double _defaultFontSize = 11.0;
    private string _defaultFont = "Calibri";

    public DocxStyleResolver(MainDocumentPart mainPart)
    {
        _mainPart = mainPart;
        LoadStyles();
        LoadDefaults();
    }

    private void LoadStyles()
    {
        var stylesPart = _mainPart.StyleDefinitionsPart;
        if (stylesPart?.Styles == null) return;
        foreach (var s in stylesPart.Styles.Elements<Style>())
        {
            var id = s.StyleId?.Value;
            if (!string.IsNullOrEmpty(id))
                _styles[id!] = s;
        }
    }

    private void LoadDefaults()
    {
        var stylesPart = _mainPart.StyleDefinitionsPart;
        var docDefaults = stylesPart?.Styles?.DocDefaults;
        if (docDefaults == null) return;

        var rPr = docDefaults.Descendants<RunProperties>().FirstOrDefault();
        if (rPr != null)
        {
            var fonts = rPr.Descendants<DocumentFormat.OpenXml.Wordprocessing.RunFonts>().FirstOrDefault();
            if (fonts != null)
                _defaultFont = fonts.Ascii?.Value ?? fonts.ComplexScript?.Value ?? _defaultFont;
            var sz = rPr.Descendants<FontSize>().FirstOrDefault();
            if (sz?.Val?.Value != null)
                _defaultFontSize = int.Parse(sz.Val.Value) / 2.0;
        }
    }

    public class ResolvedStyle
    {
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public bool Underline { get; set; }
        public bool Strike { get; set; }
        public string? Color { get; set; }
        public double FontSize { get; set; } = 11.0;
        public string FontFamily { get; set; } = "Calibri";
        public string Alignment { get; set; } = "left";
        public bool IsHeading { get; set; }
        public int HeadingLevel { get; set; }
        public double SpaceBefore { get; set; } = 4;
        public bool Highlight { get; set; }
        public string HighlightColor { get; set; } = "#FFFF00";
    }

    public ResolvedStyle ResolveForParagraph(string styleId)
    {
        var resolved = new ResolvedStyle { FontFamily = _defaultFont, FontSize = _defaultFontSize, SpaceBefore = 4 };
        if (string.IsNullOrEmpty(styleId) || !_styles.TryGetValue(styleId, out var style))
            return resolved;

        if (styleId.StartsWith("Heading", StringComparison.OrdinalIgnoreCase))
        {
            resolved.IsHeading = true;
            var numStr = Regex.Replace(styleId, @"[^\d]", "");
            if (int.TryParse(numStr, out int lv)) resolved.HeadingLevel = Math.Clamp(lv, 1, 6);
            resolved.FontSize = resolved.HeadingLevel switch { 1 => 18, 2 => 15, 3 => 13, 4 => 12, 5 => 11, _ => 10 };
            resolved.SpaceBefore = resolved.HeadingLevel switch { 1 => 16, 2 => 12, 3 => 10, 4 => 8, _ => 6 };
            resolved.Bold = true;
        }

        var pPr = style.StyleParagraphProperties;
        if (pPr != null)
        {
            if (pPr.SpacingBetweenLines?.Before?.Value != null)
                resolved.SpaceBefore = int.Parse(pPr.SpacingBetweenLines.Before.Value) / 20.0;
            if (pPr.Justification?.Val?.Value != null)
            {
                var j = pPr.Justification.Val.Value;
                if (j == JustificationValues.Center) resolved.Alignment = "center";
                else if (j == JustificationValues.Right) resolved.Alignment = "right";
                else if (j == JustificationValues.Both) resolved.Alignment = "both";
            }
        }

        var rPr = style.StyleRunProperties;
        if (rPr != null) ApplyRunProps(rPr, resolved);
        return resolved;
    }

    public ResolvedStyle ResolveForRun(RunProperties? rPr, ResolvedStyle paraStyle)
    {
        var resolved = new ResolvedStyle
        {
            Bold = paraStyle.Bold, Italic = paraStyle.Italic, Underline = paraStyle.Underline,
            Strike = paraStyle.Strike, Color = paraStyle.Color, FontSize = paraStyle.FontSize,
            FontFamily = paraStyle.FontFamily, IsHeading = paraStyle.IsHeading,
            Alignment = paraStyle.Alignment, SpaceBefore = paraStyle.SpaceBefore
        };
        if (rPr != null) ApplyRunProps(rPr, resolved);
        return resolved;
    }

    private void ApplyRunProps(OpenXmlElement rPr, ResolvedStyle resolved)
    {
        if (rPr.Elements<Bold>().Any()) resolved.Bold = true;
        if (rPr.Elements<Italic>().Any()) resolved.Italic = true;
        if (rPr.Elements<Underline>().Any(u => u.Val?.Value != UnderlineValues.None)) resolved.Underline = true;
        if (rPr.Elements<Strike>().Any()) resolved.Strike = true;

        var colorEl = rPr.Elements<Color>().FirstOrDefault();
        if (colorEl?.Val?.Value != null && colorEl.Val.Value != "auto")
            resolved.Color = "#" + colorEl.Val.Value;

        var sz = rPr.Elements<FontSize>().FirstOrDefault();
        if (sz?.Val?.Value != null)
            resolved.FontSize = int.Parse(sz.Val.Value) / 2.0;

        var fonts = rPr.Elements<DocumentFormat.OpenXml.Wordprocessing.RunFonts>().FirstOrDefault();
        if (fonts != null)
        {
            var f = fonts.Ascii?.Value ?? fonts.ComplexScript?.Value;
            if (!string.IsNullOrEmpty(f)) resolved.FontFamily = f;
        }

        var highlight = rPr.Elements<Highlight>().FirstOrDefault();
        if (highlight != null && highlight.Val?.Value != HighlightColorValues.None)
        {
            resolved.Highlight = true;
            var colorStr = highlight.Val.Value.ToString();
            resolved.HighlightColor = colorStr switch
            {
                "yellow" => "#FFFF00", "green" => "#00FF00", "cyan" => "#00FFFF",
                "magenta" => "#FF00FF", "red" => "#FF0000", "blue" => "#0000FF",
                "black" => "#000000", "white" => "#FFFFFF",
                _ => "#FFFF00"
            };
        }
    }

    public bool IsNumberedList(int numId)
    {
        var numberingPart = _mainPart.NumberingDefinitionsPart;
        if (numberingPart?.Numbering == null) return false;
        var num = numberingPart.Numbering.Elements<NumberingInstance>().FirstOrDefault(n => n.NumberID?.Value == numId);
        if (num == null) return false;
        var abstractNumId = num.AbstractNumId?.Val?.Value;
        if (abstractNumId == null) return false;
        var absNum = numberingPart.Numbering.Elements<AbstractNum>().FirstOrDefault(a => a.AbstractNumberId?.Value == abstractNumId);
        var lvl0 = absNum?.Elements<Level>().FirstOrDefault(l => l.LevelIndex?.Value == 0);
        var fmt = lvl0?.NumberingFormat?.Val?.Value;
        return fmt == NumberFormatValues.Decimal || fmt == NumberFormatValues.LowerRoman || fmt == NumberFormatValues.UpperRoman;
    }
}
