using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TurnAFile.Windows.Helpers;

public static class IconGenerator
{
    /// <summary>
    /// Genera un icono circular de color para calidades
    /// </summary>
    public static FrameworkElement CreateQualityIcon(string quality)
    {
        Color color = quality.ToLower() switch
        {
            "alta" => Color.FromRgb(0, 120, 212),   // Azul
            "media" => Color.FromRgb(107, 142, 35),  // Oliva
            "baja" => Color.FromRgb(205, 92, 92),    // Terracota
            _ => Colors.Gray
        };

        return new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(color),
            Margin = new Thickness(0, 0, 8, 0),
            ToolTip = quality
        };
    }

    /// <summary>
    /// Crea el icono para el menu contextual usando emojis + colores
    /// </summary>
    public static FrameworkElement CreateFormatIcon(string format)
    {
        var (emoji, color) = GetFormatInfo(format);

        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var circle = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(color),
            Margin = new Thickness(0, 0, 4, 0)
        };

        var text = new TextBlock
        {
            Text = emoji,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };

        stack.Children.Add(circle);
        stack.Children.Add(text);

        return stack;
    }

    private static (string Emoji, Color Color) GetFormatInfo(string format)
    {
        return format.ToUpper() switch
        {
            // Video formats
            "MP4" => ("\U0001F3AC", Color.FromRgb(0, 120, 212)),
            "AVI" => ("\U0001F3AC", Color.FromRgb(156, 39, 176)),
            "MKV" => ("\U0001F3AC", Color.FromRgb(233, 30, 99)),
            "WEBM" => ("\U0001F3AC", Color.FromRgb(76, 175, 80)),
            "MOV" => ("\U0001F3A5", Color.FromRgb(255, 152, 0)),

            // Audio formats
            "MP3" => ("\U0001F3B5", Color.FromRgb(33, 150, 243)),
            "WAV" => ("\U0001F3B5", Color.FromRgb(96, 125, 139)),
            "M4A" => ("\U0001F3B5", Color.FromRgb(0, 150, 136)),
            "FLAC" => ("\U0001F3B5", Color.FromRgb(63, 81, 181)),
            "OGG" => ("\U0001F3B5", Color.FromRgb(255, 87, 34)),

            // Image formats
            "JPG" => ("\U0001F5BC\U0000FE0F", Color.FromRgb(255, 193, 7)),
            "JPEG" => ("\U0001F5BC\U0000FE0F", Color.FromRgb(255, 193, 7)),
            "PNG" => ("\U0001F5BC\U0000FE0F", Color.FromRgb(139, 195, 74)),
            "WEBP" => ("\U0001F5BC\U0000FE0F", Color.FromRgb(0, 188, 212)),
            "GIF" => ("\U0001F5BC\U0000FE0F", Color.FromRgb(103, 58, 183)),
            "BMP" => ("\U0001F5BC\U0000FE0F", Color.FromRgb(158, 158, 158)),

            // Document formats
            "DOCX" => ("\U0001F4DD", Color.FromRgb(41, 121, 255)),
            "PDF" => ("\U0001F4D5", Color.FromRgb(234, 67, 53)),
            "MD" => ("\U0001F4D3", Color.FromRgb(107, 142, 35)),
            "HTML" => ("\U0001F310", Color.FromRgb(227, 76, 38)),
            "EPUB" => ("\U0001F4D6", Color.FromRgb(156, 39, 176)),
            "TXT" => ("\U0001F4C3", Color.FromRgb(158, 158, 158)),

            // Data formats
            "XLSX" => ("\U0001F4CA", Color.FromRgb(33, 150, 243)),
            "CSV" => ("\U0001F4CB", Color.FromRgb(76, 175, 80)),
            "JSON" => ("\U0001F527", Color.FromRgb(255, 152, 0)),

            _ => ("\U0001F4C4", Colors.Gray)
        };
    }
}
