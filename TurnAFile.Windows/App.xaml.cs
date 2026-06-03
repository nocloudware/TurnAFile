using TurnAFile.Windows.Resources;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TurnAFile.Core.Helpers;
using TurnAFile.Core.Services;
using TurnAFile.Windows.Services;
using TurnAFile.Windows.Views;

namespace TurnAFile.Windows;

public partial class App : System.Windows.Application
{
    private ResourceDictionary? _currentThemeResources;
    private ResourceDictionary? _sharedResources;
    private EventHandler? _themeChangeHandler;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        InitializeLanguage();
        LoadSharedResources();
        ApplyTheme(ThemeDetector.GetCurrentTheme());
        _themeChangeHandler = (s, _) =>
            Dispatcher.Invoke(() => ApplyTheme(ThemeDetector.GetCurrentTheme()));
        ThemeDetector.ListenToThemeChanges(_themeChangeHandler);

        var args = ParseArgs(e.Args);

        if (args.IsAddMode)
        {
            if (!SingleInstanceManager.TryBecomeFirstInstance())
            {
                SingleInstanceManager.SendFileToRunningInstance(args.InputPath);
                Shutdown(0);
                return;
            }

            EnsureShellRegistered();
            StartPipeServer();
            StartAddMode(args);
            return;
        }

        if (args.IsConvertMode)
        {
            EnsureShellRegistered();
            _ = StartHeadlessConversionAsync(args);
            return;
        }

        if (!SingleInstanceManager.TryBecomeFirstInstance())
        {
            Shutdown(0);
            return;
        }

        EnsureShellRegistered();
        StartPipeServer();
        StartNormalMode();

        Dispatcher.BeginInvoke(new Action(async () => await CheckForUpdatesAsync()),
            System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_themeChangeHandler != null)
            ThemeDetector.StopListening(_themeChangeHandler);
        SingleInstanceManager.Release();
        base.OnExit(e);
    }

    // ─────────────────────────────────────────────────────────────────────
    // VERIFICACIÓN DE ACTUALIZACIONES
    // ─────────────────────────────────────────────────────────────────────

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var updateService = new UpdateService();

            if (!updateService.ShouldCheckToday())
                return;

            var updateInfo = await updateService.CheckForUpdatesAsync();
            updateService.MarkCheckedToday();

            if (updateInfo != null && updateInfo.IsNewerVersion)
            {
                if (updateService.IsVersionIgnored(updateInfo.Version))
                    return;

                var currentVersion = System.Reflection.Assembly.GetExecutingAssembly()
                    .GetName().Version?.ToString() ?? "0.0.0";

                Dispatcher.Invoke(() =>
                {
                    var dialog = new UpdateAvailableDialog(updateInfo, currentVersion);
                    dialog.Owner = MainWindow;
                    dialog.ShowDialog();
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error en verificación automática de actualizaciones: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // REINSTALACIÓN DEL MENÚ CONTEXTUAL
    // ─────────────────────────────────────────────────────────────────────

    private void EnsureShellRegistered()
    {
        try
        {
            if (!ShellMenuInstaller.IsInstalled())
            {
                ShellMenuInstaller.Install();
                Debug.WriteLine("Menú contextual instalado correctamente");
            }
            else
            {
                Debug.WriteLine("Menú contextual ya instalado");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"No se pudo registrar menú Explorer: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // LANGUAGE
    // ─────────────────────────────────────────────────────────────────────

    private void InitializeLanguage()
    {
        var currentCulture = System.Globalization.CultureInfo.CurrentUICulture;
        Debug.WriteLine($"App language initialized to: {currentCulture.Name}");
    }

    public void ChangeLanguage(System.Globalization.CultureInfo culture)
    {
        StringsWrapper.SetCulture(culture);
    }

    // ─────────────────────────────────────────────────────────────────────
    // PIPE SERVER
    // ─────────────────────────────────────────────────────────────────────

    private void StartPipeServer()
    {
        SingleInstanceManager.StartPipeServer(filePaths =>
        {
            Dispatcher.Invoke(() =>
            {
                if (MainWindow is MainWindow mw)
                {
                    mw.AppendFiles(filePaths);
                    if (mw.WindowState == WindowState.Minimized)
                        mw.WindowState = WindowState.Normal;
                    mw.Activate();
                }
            });
        });
    }

    // ─────────────────────────────────────────────────────────────────────
    // MODOS DE ARRANQUE
    // ─────────────────────────────────────────────────────────────────────

    private void StartNormalMode()
    {
        var w = new MainWindow();
        MainWindow = w;
        w.Show();
    }

    private void StartAddMode(ConversionArgs args)
    {
        var w = new MainWindow();
        MainWindow = w;
        w.Show();

        if (File.Exists(args.InputPath))
            w.AppendFiles(new[] { args.InputPath });
    }

    // ─────────────────────────────────────────────────────────────────────
    // MODO HEADLESS
    // ─────────────────────────────────────────────────────────────────────

    private async Task StartHeadlessConversionAsync(ConversionArgs args)
    {
        if (!File.Exists(args.InputPath))
            {
                MessageBox.Show(StringsWrapper.Instance.FileNotFound(args.InputPath),
                    StringsWrapper.Instance.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

        string ffmpegPath = ConversionHelpers.GetFFmpegPath();

        if (!File.Exists(ffmpegPath))
        {
            MessageBox.Show(StringsWrapper.Instance.FFmpegNotFoundMessage,
                StringsWrapper.Instance.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        string outputPath = ConversionHelpers.GetOutputPath(args.InputPath, args.Format);
        var cts = new CancellationTokenSource();
        var progressWindow = new ConversionProgressWindow();
        bool isClosing = false;

        progressWindow.Closed += (_, _) =>
        {
            if (isClosing) return;
            isClosing = true;

            if (!cts.IsCancellationRequested)
            {
                cts.Cancel();
                Task.Delay(500).ContinueWith(_ => Dispatcher.Invoke(Shutdown));
            }
            else
            {
                Dispatcher.Invoke(Shutdown);
            }
        };

        progressWindow.StartConversion(1, cts);
        progressWindow.InitializeFileInfo(args.InputPath, Path.GetExtension(outputPath).TrimStart('.').ToUpper());

        MainWindow = progressWindow;
        progressWindow.Show();

        try
        {
            bool ok = false;

            if (args.MediaType == "ocr")
            {
                var ocrConverter = new OcrConverter();
                var ocrResult = await ocrConverter.ConvertImageToTextAsync(
                    args.InputPath, outputPath, args.Format, cts.Token);
                ok = ocrResult.Success;

                if (!isClosing && !ocrResult.Success)
                {
                    progressWindow.Dispatcher.Invoke(() =>
                        progressWindow.SetError(ocrResult.ErrorMessage ?? StringsWrapper.Instance.OCRConversionFailed));
                }
            }
            else if (args.MediaType == "document")
            {
                var docConverter = new DocumentConverter();
                var docResult = await docConverter.ConvertAsync(
                    args.InputPath, outputPath, args.Format, cts.Token);
                ok = docResult.Success;

                if (!isClosing && !docResult.Success)
                {
                    progressWindow.Dispatcher.Invoke(() =>
                        progressWindow.SetError(docResult.ErrorMessage ?? StringsWrapper.Instance.DocumentConversionFailed));
                }
            }
            else if (args.MediaType == "data")
            {
                var dataConverter = new DataConverter();
                string inputExt = Path.GetExtension(args.InputPath).ToLowerInvariant();
                string outputExt = $".{args.Format.ToLowerInvariant()}";

                Task<ConversionResult> convertTask;

                if (DataConverter.IsExcelFile(inputExt) && outputExt == ".csv")
                    convertTask = dataConverter.ConvertExcelToCsvAsync(args.InputPath, outputPath, cts.Token);
                else if (DataConverter.IsCsvFile(inputExt) && DataConverter.IsExcelFile(outputExt))
                    convertTask = dataConverter.ConvertCsvToExcelAsync(args.InputPath, outputPath, cts.Token);
                else if (DataConverter.IsJsonFile(inputExt) && outputExt == ".csv")
                    convertTask = dataConverter.ConvertJsonToCsvAsync(args.InputPath, outputPath, cts.Token);
                else if (DataConverter.IsExcelFile(inputExt) && outputExt == ".pdf")
                    convertTask = dataConverter.ConvertExcelToPdfAsync(args.InputPath, outputPath, cts.Token);
                else if (DataConverter.IsCsvFile(inputExt) && outputExt == ".pdf")
                    convertTask = dataConverter.ConvertCsvToPdfAsync(args.InputPath, outputPath, cts.Token);
                else if (DataConverter.IsJsonFile(inputExt) && outputExt == ".pdf")
                    convertTask = dataConverter.ConvertJsonToPdfAsync(args.InputPath, outputPath, cts.Token);
                else
                {
                    progressWindow.Dispatcher.Invoke(() =>
                        progressWindow.SetError(StringsWrapper.Instance.UnsupportedDataConversion(inputExt, outputExt)));
                    isClosing = true;
                    Shutdown(1);
                    return;
                }

                var dataResult = await convertTask;
                ok = dataResult.Success;

                if (!isClosing && !dataResult.Success)
                {
                    progressWindow.Dispatcher.Invoke(() =>
                        progressWindow.SetError(dataResult.ErrorMessage ?? StringsWrapper.Instance.DataConversionFailed));
                }
            }
            else if (args.MediaType == "image" && args.Format.ToLower() == "pdf")
            {
                try
                {
                    DocumentPdfGenerator.ConvertImageToPdf(args.InputPath, outputPath);
                    ok = File.Exists(outputPath);
                    if (!ok && !isClosing)
                    {
                        progressWindow.Dispatcher.Invoke(() =>
                            progressWindow.SetError(StringsWrapper.Instance.CouldNotGeneratePDF));
                    }
                }
                catch
                {
                    if (!isClosing)
                        progressWindow.Dispatcher.Invoke(() => progressWindow.SetError(StringsWrapper.Instance.PDFGeneratedError));
                }
            }
            else if (args.MediaType == "image")
            {
                var svc = new ConversionService();
                svc.ProgressChanged += (_, ev) =>
                    progressWindow.Dispatcher.Invoke(() =>
                        progressWindow.UpdateProgress(
                            ev.FileName,
                            Path.GetExtension(args.InputPath).TrimStart('.').ToUpper(),
                            args.Format.ToUpper(),
                            QualityLabel(args.Quality),
                            ev.Percentage, 1, 1, ev.Duration));

                ok = await svc.ConvertAsync(
                    args.InputPath, outputPath, args.Format,
                    ConversionHelpers.MapVideoQuality(args.Quality),
                    ConversionHelpers.MapAudioQuality(args.Quality),
                    ConversionHelpers.MapImageScaling(args.Quality),
                    cts.Token, ffmpegPath);
            }

            if (!isClosing)
            {
                progressWindow.Dispatcher.Invoke(() =>
                {
                    if (ok && !cts.Token.IsCancellationRequested)
                        progressWindow.SetCompleted();
                    else if (!cts.Token.IsCancellationRequested && args.MediaType != "ocr")
                        progressWindow.SetError(StringsWrapper.Instance.CouldNotConvertFile);
                });
            }
        }
        catch (OperationCanceledException)
        {
            if (!isClosing)
                progressWindow.Dispatcher.Invoke(() => progressWindow.SetError(StringsWrapper.Instance.ConversionCancelledMsg));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"StartHeadlessConversion error: {ex.Message}");
            if (!isClosing)
                progressWindow.Dispatcher.Invoke(() => progressWindow.SetError(ex.Message));
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────

    private static string QualityLabel(string q) => q switch
    {
        "high" => "Alta",
        "low" => "Baja",
        _ => "Media"
    };

    // ─────────────────────────────────────────────────────────────────────
    // PARSING DE ARGUMENTOS
    // ─────────────────────────────────────────────────────────────────────

    private static ConversionArgs ParseArgs(string[] args)
    {
        var result = new ConversionArgs();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "--add":
                    if (i + 1 < args.Length)
                        result.InputPath = args[i + 1].Trim('"');
                    result.IsAddMode = true;
                    break;
                case "--convert":
                    if (i + 1 < args.Length)
                        result.InputPath = args[i + 1].Trim('"');
                    result.IsConvertMode = true;
                    break;
                case "--format":
                    if (i + 1 < args.Length)
                        result.Format = args[i + 1].ToLower();
                    break;
                case "--quality":
                    if (i + 1 < args.Length)
                        result.Quality = args[i + 1].ToLower();
                    break;
                case "--type":
                    if (i + 1 < args.Length)
                        result.MediaType = args[i + 1].ToLower();
                    break;
            }
        }
        return result;
    }

    private class ConversionArgs
    {
        public bool IsAddMode { get; set; }
        public bool IsConvertMode { get; set; }
        public string InputPath { get; set; } = string.Empty;
        public string Format { get; set; } = "mp4";
        public string Quality { get; set; } = "medium";
        public string MediaType { get; set; } = "video";
    }

    // ─────────────────────────────────────────────────────────────────────
    // TEMA
    // ─────────────────────────────────────────────────────────────────────

    private void LoadSharedResources()
    {
        _sharedResources = new ResourceDictionary();
        _sharedResources.MergedDictionaries.Add(new ResourceDictionary
        { Source = new Uri("/Styles/Brushes.xaml", UriKind.Relative) });
        _sharedResources.MergedDictionaries.Add(new ResourceDictionary
        { Source = new Uri("/Styles/ButtonStyles.xaml", UriKind.Relative) });
        _sharedResources.MergedDictionaries.Add(new ResourceDictionary
        { Source = new Uri("/Styles/CardStyles.xaml", UriKind.Relative) });
        Resources.MergedDictionaries.Add(_sharedResources);
    }

    public void ApplyTheme(AppTheme theme)
    {
        if (_currentThemeResources != null)
            Resources.MergedDictionaries.Remove(_currentThemeResources);

        var themeName = theme == AppTheme.Dark ? "Brushes.Dark.xaml" : "Brushes.Light.xaml";
        _currentThemeResources = new ResourceDictionary
        { Source = new Uri($"/Styles/{themeName}", UriKind.Relative) };
        Resources.MergedDictionaries.Add(_currentThemeResources);

        foreach (Window window in Windows)
        {
            var s = window.Style; window.Style = null; window.Style = s;
            if (window is MainWindow mw)
            {
                mw.UpdateThemeState(theme);
                mw.Background = (System.Windows.Media.Brush)FindResource("WindowBackgroundBrush");
            }
        }
    }
}           