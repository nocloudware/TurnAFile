using TurnAFile.Windows.Resources;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TurnAFile.Core.Helpers;
using TurnAFile.Core.Models;
using TurnAFile.Core.Services;
using TurnAFile.Windows.Controls;
using TurnAFile.Windows.Helpers;
using TurnAFile.Windows.Services;
using Microsoft.Win32;

namespace TurnAFile.Windows;

public enum FileItemStatus
{
    Pending,
    Success,
    Failed
}

public class FileItem : INotifyPropertyChanged
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public ImageSource? IconImage { get; set; }

    private FileItemStatus _status = FileItemStatus.Pending;
    public FileItemStatus Status
    {
        get => _status;
        set
        {
            if (_status == value) return;
            _status = value;
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(StatusVisible));
            OnPropertyChanged(nameof(StatusIcon));
            OnPropertyChanged(nameof(StatusColor));
        }
    }

    public Visibility StatusVisible => Status != FileItemStatus.Pending ? Visibility.Visible : Visibility.Collapsed;
    public string StatusIcon => Status == FileItemStatus.Success ? "✓" : "✗";
    public System.Windows.Media.Brush StatusColor => Status == FileItemStatus.Success
        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80))
        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 67, 54));

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string p) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}

public class LanguageInfo
{
    public string Name { get; set; } = string.Empty;
    public string Flag { get; set; } = string.Empty;
    public string CultureCode { get; set; } = string.Empty;

    public LanguageInfo(string name, string flag, string cultureCode)
    {
        Name = name;
        Flag = flag;
        CultureCode = cultureCode;
    }

    public override string ToString() => Name;
}

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private ObservableCollection<FileItem> _files = new();
    private bool _isDarkTheme = ThemeDetector.GetCurrentTheme() == AppTheme.Dark;
    private bool _isConverting = false;
    private CancellationTokenSource? _currentCts;
    private Views.ConversionProgressWindow? _progressWindow;
    private string? _customOutputFolder = null;
    private ConfigurationService _configService = null!;

    // Nuevos convertidores
    private DocumentConverter? _documentConverter;
    private DataConverter? _dataConverter;
    private OcrConverter? _ocrConverter;

    public List<string> VideoFormats { get; } = new() { "MP4", "AVI", "MKV", "WEBM", "MOV", "MP3", "M4A" };
    public List<string> AudioFormats { get; } = new() { "MP3", "WAV", "M4A", "FLAC", "OGG" };
    public List<string> ImageFormats { get; } = new() { "JPG", "PNG", "WEBP", "GIF", "BMP", "TIFF", "PDF", "TXT (OCR)" };

    // Nuevos formatos para documentos, datos y OCR
    public List<string> DocumentFormats { get; } = new() { "DOCX", "PDF", "MD", "HTML", "EPUB", "TXT", "RTF", "ODT" };
    public List<string> DataFileFormats { get; } = new() { "XLSX", "CSV", "JSON", "PDF" };

    private string _selectedVideoFormat = "MP4";
    private string _selectedAudioFormat = "MP3";
    private string _selectedImageFormat = "JPG";
    private string _selectedDocumentFormat = "DOCX";
    private string _selectedDataFormat = "XLSX";

    public string SelectedVideoFormat
    {
        get => _selectedVideoFormat;
        set { if (_selectedVideoFormat != value) { _selectedVideoFormat = value; OnPropertyChanged(); SaveSettings(); } }
    }

    public string SelectedAudioFormat
    {
        get => _selectedAudioFormat;
        set { if (_selectedAudioFormat != value) { _selectedAudioFormat = value; OnPropertyChanged(); SaveSettings(); } }
    }

    public string SelectedImageFormat
    {
        get => _selectedImageFormat;
        set { if (_selectedImageFormat != value) { _selectedImageFormat = value; OnPropertyChanged(); SaveSettings(); } }
    }

    public string SelectedDocumentFormat
    {
        get => _selectedDocumentFormat;
        set { if (_selectedDocumentFormat != value) { _selectedDocumentFormat = value; OnPropertyChanged(); SaveSettings(); } }
    }

    public string SelectedDataFormat
    {
        get => _selectedDataFormat;
        set { if (_selectedDataFormat != value) { _selectedDataFormat = value; OnPropertyChanged(); SaveSettings(); } }
    }

    private ToggleState _selectedVideoQuality = ToggleState.Medium;
    private ToggleState _selectedAudioQuality = ToggleState.Medium;
    private ToggleState _selectedImageQuality = ToggleState.Medium;

    private ImageSource? _videoIcon;
    private ImageSource? _audioIcon;
    private ImageSource? _imageIcon;
    private ImageSource? _fileIcon;
    private ImageSource? _defaultFileIcon;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    // Enumeración para categorías de archivos
    private enum FileCategory
    {
        Video, Audio, Image, Document, Data, Unknown
    }

    public MainWindow()
    {
        InitializeComponent();
        _configService = new ConfigurationService();
        LoadSettings();
        UpdateThemeIcon();

        // Inicializar convertidores
        _documentConverter = new DocumentConverter();
        _dataConverter = new DataConverter();
        _ocrConverter = new OcrConverter();

        try
        {
            _videoIcon = new BitmapImage(new Uri("pack://application:,,,/Assets/Emojis/video.png"));
            _audioIcon = new BitmapImage(new Uri("pack://application:,,,/Assets/Emojis/audio.png"));
            _imageIcon = new BitmapImage(new Uri("pack://application:,,,/Assets/Emojis/image.png"));
            _fileIcon = new BitmapImage(new Uri("pack://application:,,,/Assets/Emojis/file.png"));
            _defaultFileIcon = new BitmapImage(new Uri("pack://application:,,,/Assets/Emojis/file.png"));
        }
        catch (Exception ex) { Debug.WriteLine($"Error cargando imágenes: {ex.Message}"); }

        DataContext = this;
        FileListBox.ItemsSource = _files;

        InitializeLanguageSelector();

        if (ShellMenuInstaller.IsInstalled())
        {
            _ = Task.Run(() =>
            {
                try { ShellMenuInstaller.Reinstall(); }
                catch (Exception ex) { Debug.WriteLine($"Error reinstalling shell menu: {ex.Message}"); }
            });
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // INICIALIZACIÓN DE IDIOMA
    // ─────────────────────────────────────────────────────────────────────

    private void InitializeLanguageSelector()
    {
        var languages = new List<LanguageInfo>
        {
            new LanguageInfo("English", "/Assets/Emojis/flag-uk.png", "en"),
            new LanguageInfo("Español", "/Assets/Emojis/flag-es.png", "es"),
            new LanguageInfo("Français", "/Assets/Emojis/flag-fr.png", "fr"),
            new LanguageInfo("Deutsch", "/Assets/Emojis/flag-de.png", "de"),
            new LanguageInfo("中文", "/Assets/Emojis/flag-cn.png", "zh"),
            new LanguageInfo("日本語", "/Assets/Emojis/flag-jp.png", "ja"),
            new LanguageInfo("Português", "/Assets/Emojis/flag-br.png", "pt")
        };

        LanguageSelector.ItemsSource = languages;

        var currentCulture = StringsWrapper.CurrentCulture.TwoLetterISOLanguageName;
        var currentIndex = languages.FindIndex(l => l.CultureCode == currentCulture);
        LanguageSelector.SelectedIndex = currentIndex >= 0 ? currentIndex : 0;
    }

    // ─────────────────────────────────────────────────────────────────────
    // MANEJO DE ARCHIVOS
    // ─────────────────────────────────────────────────────────────────────

    public void AppendFiles(string[] paths)
    {
        foreach (var path in paths)
        {
            if (_files.Any(f => f.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase)))
                continue;

            var fileName = System.IO.Path.GetFileName(path);
            _files.Add(new FileItem
            {
                Name = fileName,
                FullPath = path,
                IconImage = GetFileIconImage(fileName)
            });
        }
        UpdateDropText();

        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Activate();
    }

    private void OnSelectOutputFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = StringsWrapper.Instance.SelectOutputFolderDialog,
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            _customOutputFolder = dialog.FolderName;
            OutputFolderText.Text = _customOutputFolder;
            OutputFolderText.Foreground = (Brush)FindResource("TextPrimaryBrush");
            OutputFolderText.ToolTip = _customOutputFolder;
        }
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0)
                AddFiles(files);
        }
    }

    private void OnSelectFiles(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Archivos soportados|*.mp4;*.mov;*.avi;*.mkv;*.mp3;*.wav;*.jpg;*.png;*.gif;*.docx;*.pdf;*.xlsx;*.csv;*.json|Todos los archivos|*.*"
        };
        if (dialog.ShowDialog() == true)
            AddFiles(dialog.FileNames);
    }

    private void AddFiles(string[] paths)
    {
        _files.Clear();
        foreach (var path in paths)
        {
            var fileName = Path.GetFileName(path);
            _files.Add(new FileItem
            {
                Name = fileName,
                FullPath = path,
                IconImage = GetFileIconImage(fileName)
            });
        }
        UpdateDropText();
    }

    private void OnFileListContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var element = e.OriginalSource as FrameworkElement;
        var fileItem = FindFileItemFromElement(element)
                       ?? FileListBox.SelectedItem as FileItem;

        if (fileItem == null)
        {
            e.Handled = true;
            return;
        }

        FileListBox.SelectedItem = fileItem;
        FileListBox.ContextMenu = BuildContextMenu(fileItem);
    }

    private FileItem? FindFileItemFromElement(FrameworkElement? element)
    {
        while (element != null)
        {
            if (element.DataContext is FileItem fi)
                return fi;
            element = VisualTreeHelper.GetParent(element) as FrameworkElement;
        }
        return null;
    }

    private ContextMenu BuildContextMenu(FileItem file)
    {
        var menu = new ContextMenu();

        menu.Items.Add(new MenuItem
        {
            Header = StringsWrapper.Instance.ShellMenuConvertWith,
            IsEnabled = false,
            FontWeight = FontWeights.SemiBold
        });

        menu.Items.Add(new Separator());

        List<string> formats;
        var category = GetFileCategory(file.Name);

        formats = category switch
        {
            FileCategory.Video => VideoFormats,
            FileCategory.Audio => AudioFormats,
            FileCategory.Image => ImageFormats,
            FileCategory.Document => DocumentFormats,
            FileCategory.Data => DataFileFormats,
            _ => new List<string>()
        };

        foreach (var format in formats)
        {
            bool isSameFormat = string.Equals(
                Path.GetExtension(file.Name).TrimStart('.'),
                format,
                StringComparison.OrdinalIgnoreCase);

            var formatHeader = new StackPanel { Orientation = Orientation.Horizontal };
            var formatIcon = Helpers.FileIconHelper.GetIconForExtension("." + format.ToLower(), size: 16);
            if (formatIcon != null)
            {
                formatHeader.Children.Add(new Image
                {
                    Source = formatIcon,
                    Width = 16,
                    Height = 16,
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    Margin = new Thickness(0, 0, 6, 0),
                    UseLayoutRounding = true
                });
            }
            formatHeader.Children.Add(new TextBlock
            {
                Text = StringsWrapper.Instance.ConvertToFormat(format),
                VerticalAlignment = VerticalAlignment.Center
            });

            var formatItem = new MenuItem
            {
                Header = formatHeader,
                IsEnabled = !isSameFormat,
                Opacity = isSameFormat ? 0.45 : 1.0
            };

            foreach (var (qualityLabel, qualityValue) in QualityOptions())
            {
                var qualityHeader = new StackPanel { Orientation = Orientation.Horizontal };
                qualityHeader.Children.Add(IconGenerator.CreateQualityIcon(qualityLabel));
                qualityHeader.Children.Add(new TextBlock
                {
                    Text = qualityLabel,
                    VerticalAlignment = VerticalAlignment.Center
                });

                var qualityItem = new MenuItem
                {
                    Header = qualityHeader
                };

                var capturedFile = file;
                var capturedFormat = format;
                var capturedQuality = qualityValue;

                qualityItem.Click += async (_, _) =>
                    await ConvertSingleFileAsync(capturedFile, capturedFormat, capturedQuality);

                formatItem.Items.Add(qualityItem);
            }

            menu.Items.Add(formatItem);
        }

        menu.Items.Add(new Separator());

        var removeItem = new MenuItem { Header = StringsWrapper.Instance.ContextMenuRemove };
        removeItem.Click += (_, _) => RemoveFile(file);
        menu.Items.Add(removeItem);

        var clearItem = new MenuItem { Header = StringsWrapper.Instance.ContextMenuClearAll };
        clearItem.Click += (_, _) => ClearFiles();
        menu.Items.Add(clearItem);

        return menu;
    }

    private static IEnumerable<(string Label, ToggleState Quality)> QualityOptions()
    {
        yield return (StringsWrapper.Instance.QualityAlta, ToggleState.High);
        yield return (StringsWrapper.Instance.QualityMedia, ToggleState.Medium);
        yield return (StringsWrapper.Instance.QualityBaja, ToggleState.Low);
    }

    private async Task ConvertSingleFileAsync(FileItem file, string targetFormat, ToggleState quality)
    {
        if (_isConverting)
        {
            System.Windows.MessageBox.Show(StringsWrapper.Instance.ConversionInProgress,
                            StringsWrapper.Instance.AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _isConverting = true;
        SetControlsEnabled(false);

        _currentCts = new CancellationTokenSource();
        string outputPath = ConversionHelpers.GetOutputPath(file.FullPath, targetFormat, _customOutputFolder);
        string qualityLabel = QualityLabel(quality);

        _progressWindow = new Views.ConversionProgressWindow();
        _progressWindow.Owner = this;
        _progressWindow.StartConversion(1, _currentCts);
        _progressWindow.InitializeFileInfo(file.FullPath, Path.GetExtension(outputPath).TrimStart('.').ToUpper());
        _progressWindow.Closed += OnProgressWindowClosed;
        _progressWindow.Show();

        try
        {
            bool success = false;
            var category = GetFileCategory(file.Name);
            string sourceFormat = GetSourceFormatForFile(file.Name);

            switch (category)
            {
                case FileCategory.Video:
                case FileCategory.Audio:
                            string ffmpegPath = ConversionHelpers.GetFFmpegPath();
                    if (!File.Exists(ffmpegPath))
                    {
                        _progressWindow.SetError("No se encontró FFmpeg");
                        return;
                    }
                    var svc = new ConversionService();
                    svc.ProgressChanged += (_, args) =>
                    {
                        _progressWindow?.Dispatcher.Invoke(() =>
                        {
                            _progressWindow.UpdateProgress(
                                file.FullPath, sourceFormat, targetFormat, qualityLabel,
                                args.Percentage, 1, 1, args.Duration, args.CurrentTime);
                        });
                    };
                    success = await svc.ConvertAsync(file.FullPath, outputPath, targetFormat,
                        ConversionHelpers.MapVideoQuality(quality), ConversionHelpers.MapAudioQuality(quality), ConversionHelpers.MapImageScaling(quality),
                        _currentCts.Token, ffmpegPath);
                    break;

                case FileCategory.Image:
                    if (_selectedImageFormat == "TXT (OCR)")
                    {
                        if (_ocrConverter != null)
                        {
                            var result = await _ocrConverter.ConvertImageToTextAsync(file.FullPath, outputPath, "txt", _currentCts.Token);
                            success = result.Success;
                            if (!success) _progressWindow.SetError(result.ErrorMessage ?? StringsWrapper.Instance.OCRConversionFailed);
                            else _progressWindow.SetCompleted();
                        }
                    }
                    else if (targetFormat.ToLower() == "pdf")
                    {
                        _progressWindow?.Dispatcher.Invoke(() =>
                        {
                            _progressWindow.UpdateProgress(
                                file.FullPath, sourceFormat, targetFormat, "Normal",
                                0, 1, 1, null, null);
                        });

                        try
                        {
                            DocumentPdfGenerator.ConvertImageToPdf(file.FullPath, outputPath);
                            success = File.Exists(outputPath);
                            if (success) _progressWindow!.SetCompleted();
                            else _progressWindow!.SetError(StringsWrapper.Instance.CouldNotGeneratePDF);
                        }
                        catch
                        {
                            _progressWindow!.SetError(StringsWrapper.Instance.ErrorGeneratingPDF);
                        }
                    }
                    else
                    {
                                string ffmpegPath2 = ConversionHelpers.GetFFmpegPath();
                        if (!File.Exists(ffmpegPath2))
                        {
                            _progressWindow.SetError(StringsWrapper.Instance.FFmpegNotFoundMessage);
                            return;
                        }
                        var svc2 = new ConversionService();
                        svc2.ProgressChanged += (_, args) =>
                        {
                            _progressWindow?.Dispatcher.Invoke(() =>
                            {
                                _progressWindow.UpdateProgress(
                                    file.FullPath, sourceFormat, targetFormat, qualityLabel,
                                    args.Percentage, 1, 1, args.Duration, args.CurrentTime);
                            });
                        };
                        success = await svc2.ConvertAsync(file.FullPath, outputPath, targetFormat,
                            ConversionHelpers.MapVideoQuality(quality), ConversionHelpers.MapAudioQuality(quality), ConversionHelpers.MapImageScaling(quality),
                            _currentCts.Token, ffmpegPath2);
                    }
                    break;

                case FileCategory.Document:
                    if (_documentConverter != null)
                    {
                        _progressWindow?.Dispatcher.Invoke(() =>
                        {
                            _progressWindow!.UpdateProgress(
                                file.FullPath, sourceFormat, targetFormat, "Normal",
                                0, 1, 1, null, null);
                        });

                        var result = await _documentConverter.ConvertAsync(file.FullPath, outputPath, targetFormat, _currentCts.Token);
                        success = result.Success;
                        if (!success) _progressWindow!.SetError(result.ErrorMessage ?? StringsWrapper.Instance.DocumentConversionFailed);
                        else _progressWindow!.SetCompleted();
                    }
                    break;

                case FileCategory.Data:
                    if (_dataConverter != null)
                    {
                        _progressWindow?.Dispatcher.Invoke(() =>
                        {
                            _progressWindow.UpdateProgress(
                                file.FullPath, sourceFormat, targetFormat, "Normal",
                                0, 1, 1, null, null);
                        });

                        ConversionResult? result = null;
                        string ext = Path.GetExtension(file.FullPath).ToLower();
                        string target = targetFormat.ToUpper();

                        if ((ext == ".xlsx" || ext == ".xls") && target == "CSV")
                            result = await _dataConverter.ConvertExcelToCsvAsync(file.FullPath, outputPath, _currentCts.Token);
                        else if ((ext == ".xlsx" || ext == ".xls") && target == "JSON")
                            result = await _dataConverter.ConvertExcelToJsonAsync(file.FullPath, outputPath, _currentCts.Token);
                        else if ((ext == ".xlsx" || ext == ".xls") && target == "PDF")
                            result = await _dataConverter.ConvertExcelToPdfAsync(file.FullPath, outputPath, _currentCts.Token);
                        else if (ext == ".csv" && target == "XLSX")
                            result = await _dataConverter.ConvertCsvToExcelAsync(file.FullPath, outputPath, _currentCts.Token);
                        else if (ext == ".csv" && target == "JSON")
                            result = await _dataConverter.ConvertCsvToJsonAsync(file.FullPath, outputPath, _currentCts.Token);
                        else if (ext == ".csv" && target == "PDF")
                            result = await _dataConverter.ConvertCsvToPdfAsync(file.FullPath, outputPath, _currentCts.Token);
                        else if (ext == ".json" && target == "CSV")
                            result = await _dataConverter.ConvertJsonToCsvAsync(file.FullPath, outputPath, _currentCts.Token);
                        else if (ext == ".json" && target == "XLSX")
                            result = await _dataConverter.ConvertJsonToExcelAsync(file.FullPath, outputPath, _currentCts.Token);
                        else if (ext == ".json" && target == "PDF")
                            result = await _dataConverter.ConvertJsonToPdfAsync(file.FullPath, outputPath, _currentCts.Token);
                        else
                            throw new NotSupportedException(StringsWrapper.Instance.UnsupportedDataConversion(ext, target));

                        success = result?.Success ?? false;
                        if (!success) _progressWindow!.SetError(result?.ErrorMessage ?? StringsWrapper.Instance.DataConversionFailed);
                        else _progressWindow!.SetCompleted();
                    }
                    break;

                default:
                    _progressWindow!.SetError(string.Format(StringsWrapper.Instance.ErrorConvertingFile, file.Name));
                    break;
            }

            if (success && !_currentCts.Token.IsCancellationRequested)
            {
                _progressWindow?.Dispatcher.Invoke(() => _progressWindow!.SetCompleted());
            }
        }
        catch (Exception ex)
        {
            _progressWindow?.Dispatcher.Invoke(() => _progressWindow.SetError(ex.Message));
        }
    }

    private static string QualityLabel(ToggleState s) => s switch
    {
        ToggleState.High => "Alta",
        ToggleState.Low => "Baja",
        _ => "Media"
    };

    private void RemoveFile(FileItem file)
    {
        _files.Remove(file);
        UpdateDropText();
    }

    private void ClearFiles()
    {
        _files.Clear();
        UpdateDropText();
    }

    private void UpdateDropText()
    {
        FileCountText.Text = _files.Count > 0 ? $"({_files.Count})" : "";
        EmptyListText.Visibility = _files.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    // ─────────────────────────────────────────────────────────────────────
    // CONFIGURACIÓN Y SETTINGS
    // ─────────────────────────────────────────────────────────────────────

    private void LoadSettings()
    {
        var settings = _configService.LoadSettings();

        _selectedVideoFormat = !string.IsNullOrEmpty(settings.ActiveVideoFormat) ? settings.ActiveVideoFormat : "MP4";
        _selectedAudioFormat = !string.IsNullOrEmpty(settings.ActiveAudioFormat) ? settings.ActiveAudioFormat : "MP3";
        _selectedImageFormat = !string.IsNullOrEmpty(settings.ActiveImageFormat) ? settings.ActiveImageFormat : "JPG";
        _selectedDocumentFormat = !string.IsNullOrEmpty(settings.ActiveDocumentFormat) ? settings.ActiveDocumentFormat : "DOCX";
        _selectedDataFormat = !string.IsNullOrEmpty(settings.ActiveDataFormat) ? settings.ActiveDataFormat : "XLSX";

        if (!string.IsNullOrEmpty(settings.ActiveLanguage))
        {
            var culture = new System.Globalization.CultureInfo(settings.ActiveLanguage);
            StringsWrapper.SetCulture(culture);
        }

        OnPropertyChanged(nameof(SelectedVideoFormat));
        OnPropertyChanged(nameof(SelectedAudioFormat));
        OnPropertyChanged(nameof(SelectedImageFormat));
        OnPropertyChanged(nameof(SelectedDocumentFormat));
        OnPropertyChanged(nameof(SelectedDataFormat));

        _selectedVideoQuality = settings.DefaultVideoQuality switch
        {
            VideoQuality.High => ToggleState.High,
            VideoQuality.Low => ToggleState.Low,
            _ => ToggleState.Medium
        };

        _selectedAudioQuality = settings.DefaultAudioQuality switch
        {
            AudioQuality.High => ToggleState.High,
            AudioQuality.Low => ToggleState.Low,
            _ => ToggleState.Medium
        };

        _selectedImageQuality = settings.DefaultImageScaling switch
        {
            ImageScaling.Original => ToggleState.High,
            ImageScaling.FiftyPercent => ToggleState.Low,
            _ => ToggleState.Medium
        };

        if (VideoQualityToggle != null) VideoQualityToggle.State = _selectedVideoQuality;
        if (AudioQualityToggle != null) AudioQualityToggle.State = _selectedAudioQuality;
        if (ImageQualityToggle != null) ImageQualityToggle.State = _selectedImageQuality;
    }

    private void SaveSettings()
    {
        var settings = _configService.LoadSettings();

        settings.ActiveVideoFormat = _selectedVideoFormat;
        settings.ActiveAudioFormat = _selectedAudioFormat;
        settings.ActiveImageFormat = _selectedImageFormat;
        settings.ActiveDocumentFormat = _selectedDocumentFormat;
        settings.ActiveDataFormat = _selectedDataFormat;
        settings.ActiveLanguage = StringsWrapper.CurrentCulture.TwoLetterISOLanguageName;

        settings.DefaultVideoQuality = _selectedVideoQuality switch
        {
            ToggleState.High => VideoQuality.High,
            ToggleState.Low => VideoQuality.Low,
            _ => VideoQuality.Medium
        };

        settings.DefaultAudioQuality = _selectedAudioQuality switch
        {
            ToggleState.High => AudioQuality.High,
            ToggleState.Low => AudioQuality.Low,
            _ => AudioQuality.Medium
        };

        settings.DefaultImageScaling = _selectedImageQuality switch
        {
            ToggleState.High => ImageScaling.Original,
            ToggleState.Low => ImageScaling.FiftyPercent,
            _ => ImageScaling.SeventyFivePercent
        };

        _configService.SaveSettings(settings);
    }

    // ─────────────────────────────────────────────────────────────────────
    // TEMA Y UI
    // ─────────────────────────────────────────────────────────────────────

    public void UpdateThemeState(AppTheme theme)
    {
        _isDarkTheme = theme == AppTheme.Dark;
        Background = (System.Windows.Media.Brush)FindResource("WindowBackgroundBrush");
        UpdateThemeIcon();
    }

    private void UpdateThemeIcon()
    {
        if (ThemeIcon == null) return;
        ThemeIcon.Symbol = _isDarkTheme
            ? Wpf.Ui.Controls.SymbolRegular.WeatherSunny20
            : Wpf.Ui.Controls.SymbolRegular.WeatherMoon20;
    }

    private void OnThemeToggleClick(object sender, RoutedEventArgs e)
    {
        var newTheme = _isDarkTheme ? AppTheme.Light : AppTheme.Dark;
        _isDarkTheme = !_isDarkTheme;
        if (Application.Current is App app)
            app.ApplyTheme(newTheme);
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageSelector.SelectedItem is LanguageInfo selectedLanguage)
        {
            try
            {
                var culture = new System.Globalization.CultureInfo(selectedLanguage.CultureCode);
                StringsWrapper.SetCulture(culture);
                SaveSettings();

                if (ShellMenuInstaller.IsInstalled())
                {
                    _ = Task.Run(() =>
                    {
                        try { ShellMenuInstaller.Reinstall(); }
                        catch (Exception ex) { Debug.WriteLine($"Error reinstalling shell menu: {ex.Message}"); }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error changing language: {ex.Message}");
            }
        }
    }

    private void OnVideoFormatChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cmbVideoFormat.SelectedItem != null)
            SelectedVideoFormat = cmbVideoFormat.SelectedItem.ToString() ?? "MP4";
    }

    private void OnAudioFormatChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cmbAudioFormat.SelectedItem != null)
            SelectedAudioFormat = cmbAudioFormat.SelectedItem.ToString() ?? "MP3";
    }

    private void OnImageFormatChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cmbImageFormat.SelectedItem != null)
            SelectedImageFormat = cmbImageFormat.SelectedItem.ToString() ?? "JPG";
    }

    private void OnVideoQualityChanged(object sender, RoutedEventArgs e)
    {
        if (VideoQualityToggle != null) { _selectedVideoQuality = VideoQualityToggle.State; SaveSettings(); }
    }

    private void OnAudioQualityChanged(object sender, RoutedEventArgs e)
    {
        if (AudioQualityToggle != null) { _selectedAudioQuality = AudioQualityToggle.State; SaveSettings(); }
    }

    private void OnImageQualityChanged(object sender, RoutedEventArgs e)
    {
        if (ImageQualityToggle != null) { _selectedImageQuality = ImageQualityToggle.State; SaveSettings(); }
    }

    private void OnDocumentFormatChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cmbDocumentFormat.SelectedItem != null)
            SelectedDocumentFormat = cmbDocumentFormat.SelectedItem.ToString() ?? "DOCX";
    }

    private void OnDataFormatChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cmbDataFormat.SelectedItem != null)
            SelectedDataFormat = cmbDataFormat.SelectedItem.ToString() ?? "XLSX";
    }

    // ─────────────────────────────────────────────────────────────────────
    // ICONOS Y CATEGORIZACIÓN
    // ─────────────────────────────────────────────────────────────────────

    private ImageSource? GetFileIconImage(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return _defaultFileIcon;

        var ext = Path.GetExtension(fileName);
        var shellIcon = Helpers.FileIconHelper.GetIconForExtension(ext, size: 16);
        if (shellIcon != null)
            return shellIcon;

        if (IsVideoFile(fileName)) return _videoIcon;
        if (IsAudioFile(fileName)) return _audioIcon;
        if (IsImageFile(fileName)) return _imageIcon;
        return _fileIcon;
    }

    private static bool IsVideoFile(string fileName)
    {
        if (!Path.HasExtension(fileName)) return false;
        var ext = Path.GetExtension(fileName).ToLower();
        return ext is ".mp4" or ".mov" or ".avi" or ".mkv" or ".wmv" or ".webm" or ".flv";
    }

    private static bool IsAudioFile(string fileName)
    {
        if (!Path.HasExtension(fileName)) return false;
        var ext = Path.GetExtension(fileName).ToLower();
        return ext is ".mp3" or ".wav" or ".m4a" or ".flac" or ".ogg" or ".aac" or ".wma";
    }

    private static bool IsImageFile(string fileName)
    {
        if (!Path.HasExtension(fileName)) return false;
        var ext = Path.GetExtension(fileName).ToLower();
        return ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".tiff";
    }

    private FileCategory GetFileCategory(string fileName)
    {
        if (IsVideoFile(fileName)) return FileCategory.Video;
        if (IsAudioFile(fileName)) return FileCategory.Audio;

        string ext = Path.GetExtension(fileName).ToLower();

        if (ext is ".docx" or ".pdf" or ".md" or ".markdown" or ".html" or ".htm" or ".epub" or ".txt" or ".rtf" or ".odt")
            return FileCategory.Document;

        if (ext is ".xlsx" or ".xls" or ".csv" or ".json")
            return FileCategory.Data;

        if (ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".tiff")
            return FileCategory.Image;

        return FileCategory.Unknown;
    }

    private string GetTargetFormatForFile(string fileName)
    {
        var category = GetFileCategory(fileName);

        switch (category)
        {
            case FileCategory.Video: return _selectedVideoFormat;
            case FileCategory.Audio: return _selectedAudioFormat;
            case FileCategory.Image:
                return _selectedImageFormat == "TXT (OCR)" ? "TXT" : _selectedImageFormat;
            case FileCategory.Document: return _selectedDocumentFormat;
            case FileCategory.Data: return _selectedDataFormat;
            default: return "MP4";
        }
    }

    private string GetSourceFormatForFile(string fileName)
    {
        if (!Path.HasExtension(fileName)) return "?";
        return Path.GetExtension(fileName).TrimStart('.').ToUpper();
    }

    private string GetQualityDisplayName(string fileName)
    {
        var category = GetFileCategory(fileName);

        switch (category)
        {
            case FileCategory.Video: return QualityLabel(_selectedVideoQuality);
            case FileCategory.Audio: return QualityLabel(_selectedAudioQuality);
            case FileCategory.Image:
                return QualityLabel(_selectedImageQuality);
            case FileCategory.Document:
            case FileCategory.Data:
                return "Normal";
            default: return "Normal";
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // CONVERSIÓN POR LOTES
    // ─────────────────────────────────────────────────────────────────────

    private async void OnConvertClick(object sender, RoutedEventArgs e)
    {
        if (_isConverting)
        {
            System.Windows.MessageBox.Show(StringsWrapper.Instance.ConversionInProgress, StringsWrapper.Instance.ConversionError,
                            MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_files.Count == 0)
        {
            System.Windows.MessageBox.Show(StringsWrapper.Instance.SelectFilesMessage, StringsWrapper.Instance.NoFilesSelected,
                            MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var filesToProcess = _files.ToList();

        _isConverting = true;
        SetControlsEnabled(false);

        _currentCts = new CancellationTokenSource();
        _progressWindow = new Views.ConversionProgressWindow();
        _progressWindow.Owner = this;
        _progressWindow.StartConversion(filesToProcess.Count, _currentCts);
        _progressWindow.InitializeFileInfo(filesToProcess[0].FullPath,
            Path.GetExtension(ConversionHelpers.GetOutputPath(filesToProcess[0].FullPath, GetTargetFormatForFile(filesToProcess[0].Name))).TrimStart('.').ToUpper());
        _progressWindow.Closed += OnProgressWindowClosed;
        _progressWindow.Show();

        await ProcessConversionAsync(filesToProcess,
            ConversionHelpers.MapVideoQuality(_selectedVideoQuality), ConversionHelpers.MapAudioQuality(_selectedAudioQuality), ConversionHelpers.MapImageScaling(_selectedImageQuality), _currentCts.Token);
    }

    private async Task ProcessConversionAsync(
        List<FileItem> files,
        VideoQuality videoQuality, AudioQuality audioQuality, ImageScaling imageScaling,
        CancellationToken ct)
    {
        var logger = new ConversionLogger();
        int processed = 0;
        bool hasError = false;
        int successCount = 0;
        int failCount = 0;
        var batchStopwatch = System.Diagnostics.Stopwatch.StartNew();

        logger.LogBatchStart(files.Count);

        try
        {
            for (int i = 0; i < files.Count; i++)
            {
                if (ct.IsCancellationRequested) break;

                var file = files[i];
                processed = i + 1;
                string targetFormat = GetTargetFormatForFile(file.Name);
                string outputPath = ConversionHelpers.GetOutputPath(file.FullPath, targetFormat, _customOutputFolder);
                string qualityDisplay = GetQualityDisplayName(file.Name);
                string sourceFormat = GetSourceFormatForFile(file.Name);

                var fileStopwatch = System.Diagnostics.Stopwatch.StartNew();
                bool ok = false;
                string? lastErrorMessage = null;

                try
                {
                    var category = GetFileCategory(file.Name);

                    switch (category)
                    {
                        case FileCategory.Video:
                        case FileCategory.Audio:
                    string ffmpegPath = ConversionHelpers.GetFFmpegPath();
                            if (!File.Exists(ffmpegPath))
                            {
                                throw new FileNotFoundException(StringsWrapper.Instance.FFmpegNotFoundMessage);
                            }
                            var svc = new ConversionService();
                            svc.ProgressChanged += (_, args) =>
                            {
                                _progressWindow?.Dispatcher.Invoke(() =>
                                {
                                    _progressWindow.UpdateProgress(
                                        file.FullPath, sourceFormat, targetFormat, qualityDisplay,
                                        args.Percentage, processed, files.Count, args.Duration, args.CurrentTime);
                                });
                            };
                            ok = await svc.ConvertAsync(file.FullPath, outputPath, targetFormat,
                                videoQuality, audioQuality, imageScaling, ct, ffmpegPath);
                            break;

                        case FileCategory.Image:
                            if (_selectedImageFormat == "TXT (OCR)")
                            {
                                if (_ocrConverter != null)
                                {
                                    var result = await _ocrConverter.ConvertImageToTextAsync(file.FullPath, outputPath, "txt", ct);
                                    ok = result.Success;
                                    if (!ok)
                                    {
                                        lastErrorMessage = result.ErrorMessage ?? StringsWrapper.Instance.OCRConversionFailed;
                                        logger.LogError(lastErrorMessage);
                                    }
                                }
                            }
                            else if (targetFormat.ToLower() == "pdf")
                            {
                                try
                                {
                                    DocumentPdfGenerator.ConvertImageToPdf(file.FullPath, outputPath);
                                    ok = File.Exists(outputPath);
                                    if (!ok)
                                    {
                                        lastErrorMessage = StringsWrapper.Instance.CouldNotGeneratePDF;
                                        logger.LogError(lastErrorMessage);
                                    }
                                }
                                catch (Exception pdfEx)
                                {
                                    lastErrorMessage = pdfEx.Message;
                                    logger.LogError($"Image to PDF error: {pdfEx.Message}");
                                }
                            }
                            else
                            {
                        string ffmpegPath2 = ConversionHelpers.GetFFmpegPath();
                                if (!File.Exists(ffmpegPath2))
                                {
                                    throw new FileNotFoundException(StringsWrapper.Instance.FFmpegNotFoundMessage);
                                }
                                var svc2 = new ConversionService();
                                svc2.ProgressChanged += (_, args) =>
                                {
                                    _progressWindow?.Dispatcher.Invoke(() =>
                                    {
                                        _progressWindow.UpdateProgress(
                                            file.FullPath, sourceFormat, targetFormat, qualityDisplay,
                                            args.Percentage, processed, files.Count, args.Duration, args.CurrentTime);
                                    });
                                };
                                ok = await svc2.ConvertAsync(file.FullPath, outputPath, targetFormat,
                                    videoQuality, audioQuality, imageScaling, ct, ffmpegPath2);
                            }
                            break;

                        case FileCategory.Document:
                            if (_documentConverter != null)
                            {
                                _progressWindow?.Dispatcher.Invoke(() =>
                                {
                                    _progressWindow.UpdateProgress(
                                        file.FullPath, sourceFormat, targetFormat, "Normal",
                                        0, processed, files.Count, null, null);
                                });

                                System.Diagnostics.Debug.WriteLine($"Document conversion: {file.FullPath} -> {outputPath}, format: {targetFormat}");
                                var result = await _documentConverter.ConvertAsync(file.FullPath, outputPath, targetFormat, ct);
                                System.Diagnostics.Debug.WriteLine($"Document result: Success={result.Success}, Error={result.ErrorMessage}");
                                ok = result.Success;
                                if (!ok)
                                {
                                    lastErrorMessage = result.ErrorMessage ?? StringsWrapper.Instance.DocumentConversionFailed;
                                    logger.LogError(lastErrorMessage);
                                }
                            }
                            break;

                        case FileCategory.Data:
                            if (_dataConverter != null)
                            {
                                _progressWindow?.Dispatcher.Invoke(() =>
                                {
                                    _progressWindow.UpdateProgress(
                                        file.FullPath, sourceFormat, targetFormat, "Normal",
                                        0, processed, files.Count, null, null);
                                });

                                ConversionResult? result = null;
                                string ext = Path.GetExtension(file.FullPath).ToLower();
                                string target = targetFormat.ToUpper();

                                System.Diagnostics.Debug.WriteLine($"Data conversion: {file.FullPath} -> {outputPath}, ext: {ext}, target: {target}");

                                if ((ext == ".xlsx" || ext == ".xls") && target == "CSV")
                                    result = await _dataConverter.ConvertExcelToCsvAsync(file.FullPath, outputPath, ct);
                                else if ((ext == ".xlsx" || ext == ".xls") && target == "JSON")
                                    result = await _dataConverter.ConvertExcelToJsonAsync(file.FullPath, outputPath, ct);
                                else if ((ext == ".xlsx" || ext == ".xls") && target == "PDF")
                                    result = await _dataConverter.ConvertExcelToPdfAsync(file.FullPath, outputPath, ct);
                                else if (ext == ".csv" && target == "XLSX")
                                    result = await _dataConverter.ConvertCsvToExcelAsync(file.FullPath, outputPath, ct);
                                else if (ext == ".csv" && target == "JSON")
                                    result = await _dataConverter.ConvertCsvToJsonAsync(file.FullPath, outputPath, ct);
                                else if (ext == ".csv" && target == "PDF")
                                    result = await _dataConverter.ConvertCsvToPdfAsync(file.FullPath, outputPath, ct);
                                else if (ext == ".json" && target == "CSV")
                                    result = await _dataConverter.ConvertJsonToCsvAsync(file.FullPath, outputPath, ct);
                                else if (ext == ".json" && target == "XLSX")
                                    result = await _dataConverter.ConvertJsonToExcelAsync(file.FullPath, outputPath, ct);
                                else if (ext == ".json" && target == "PDF")
                                    result = await _dataConverter.ConvertJsonToPdfAsync(file.FullPath, outputPath, ct);
                                else
                                    throw new NotSupportedException(StringsWrapper.Instance.UnsupportedDataConversion(ext, target));

                                ok = result?.Success ?? false;
                        if (!ok)
                        {
                            lastErrorMessage = result?.ErrorMessage ?? StringsWrapper.Instance.DataConversionFailed;
                            logger.LogError(lastErrorMessage);
                        }
                    }
                    break;

                default:
                    throw new NotSupportedException(string.Format(StringsWrapper.Instance.FileTypeNotSupported, file.Name));
            }

                    fileStopwatch.Stop();
                    var outputSize = File.Exists(outputPath) ? new FileInfo(outputPath).Length : 0;
                    var inputSize = new FileInfo(file.FullPath).Length;

                    if (ok && !ct.IsCancellationRequested)
                    {
                        logger.LogConversionComplete(processed, files.Count, true, fileStopwatch.Elapsed, outputSize, inputSize);
                        successCount++;
                        file.Status = FileItemStatus.Success;
                    }
                    else if (!ct.IsCancellationRequested)
                    {
                        logger.LogConversionComplete(processed, files.Count, false, fileStopwatch.Elapsed, outputSize, inputSize);
                        hasError = true;
                        failCount++;
                        string errorMsg = lastErrorMessage ?? string.Format(StringsWrapper.Instance.ErrorConvertingFile, file.Name);
                        file.Status = FileItemStatus.Failed;
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    fileStopwatch.Stop();
                    logger.LogError($"Excepción al convertir {file.Name}", ex);
                    hasError = true;
                    failCount++;
                    file.Status = FileItemStatus.Failed;
                    continue;
                }
            }

            batchStopwatch.Stop();

            if (!hasError && !ct.IsCancellationRequested)
            {
                logger.LogBatchComplete(successCount, files.Count, batchStopwatch.Elapsed);
                _progressWindow?.Dispatcher.Invoke(() => _progressWindow?.SetCompleted());
            }
            else if (ct.IsCancellationRequested)
            {
                logger.LogWarning($"Conversión cancelada después de {processed}/{files.Count} archivos");
                _progressWindow?.Dispatcher.Invoke(() => _progressWindow?.SetError(StringsWrapper.Instance.ConversionCancelledStatus));
            }
        }
        catch (Exception ex)
        {
            batchStopwatch.Stop();
            logger.LogError(StringsWrapper.Instance.GeneralError, ex);
            _progressWindow?.Dispatcher.Invoke(() => _progressWindow?.SetError(ex.Message));
        }
    }

    private void OnProgressWindowClosed(object? sender, EventArgs e)
    {
        _isConverting = false;
        SetControlsEnabled(true);
        _currentCts?.Dispose();
        _currentCts = null;
        if (_progressWindow != null)
        {
            _progressWindow.Closed -= OnProgressWindowClosed;
            _progressWindow = null;
        }
    }

    private void SetControlsEnabled(bool enabled)
    {
        Dispatcher.Invoke(() =>
        {
            cmbVideoFormat.IsEnabled = enabled;
            cmbAudioFormat.IsEnabled = enabled;
            cmbImageFormat.IsEnabled = enabled;
            VideoQualityToggle.IsEnabled = enabled;
            AudioQualityToggle.IsEnabled = enabled;
            ImageQualityToggle.IsEnabled = enabled;
            SelectFilesButton.IsEnabled = enabled;
            SelectOutputFolderButton.IsEnabled = enabled;
            ConvertButton.IsEnabled = enabled;
            ExitButton.IsEnabled = enabled;
            AboutButton.IsEnabled = enabled;
        });
    }

    // ─────────────────────────────────────────────────────────────────────
    // ABOUT
    // ─────────────────────────────────────────────────────────────────────

    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var w = new Views.AboutWindow { Owner = this };
        w.ShowDialog();
    }

    private void OnDonateClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.nocloudware.com/donate.html",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void OnHeaderMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }
}
