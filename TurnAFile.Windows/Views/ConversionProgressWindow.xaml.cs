using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using TurnAFile.Windows.Helpers;
using TurnAFile.Windows.Resources;

namespace TurnAFile.Windows.Views;

public partial class ConversionProgressWindow : Wpf.Ui.Controls.FluentWindow
{
    private CancellationTokenSource? _cancellationTokenSource;
    private int _totalFiles;
    private int _currentFileIndex;
    private DateTime _currentFileStartTime;
    private TimeSpan? _currentFileDuration;
    private double _lastPercentage = -1;
    private int _isCancellingFlag; // 0 = false, 1 = true (Interlocked)
    private int _isCompletedFlag;  // 0 = false, 1 = true (Interlocked)
    private System.Windows.Threading.DispatcherTimer? _autoCloseTimer;

    private bool IsCancelling => Interlocked.CompareExchange(ref _isCancellingFlag, 0, 0) == 1;
    private bool IsCompleted => Interlocked.CompareExchange(ref _isCompletedFlag, 0, 0) == 1;

    public ConversionProgressWindow()
    {
        InitializeComponent();
        Closed += OnWindowClosed;
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _autoCloseTimer?.Stop();
        _autoCloseTimer = null;
    }

    public void StartConversion(int totalFiles, CancellationTokenSource cts)
    {
        _totalFiles = totalFiles;
        _cancellationTokenSource = cts;
        _currentFileIndex = 0;
        Interlocked.Exchange(ref _isCancellingFlag, 0);
        Interlocked.Exchange(ref _isCompletedFlag, 0);
        _lastPercentage = -1;
        StatusText.Text = StringsWrapper.Instance.ProcessingFile(1, _totalFiles);

        CancelButton.IsEnabled = true;
        CancelButton.Content = StringsWrapper.Instance.Conversion_cancelButton;
        CancelButton.Foreground = (Brush)FindResource("TextPrimaryBrush");
    }

    public void InitializeFileInfo(string inputPath, string targetFormat)
    {
        Dispatcher.Invoke(() =>
        {
            string fileName = System.IO.Path.GetFileName(inputPath);
            string sourceFormat = System.IO.Path.GetExtension(inputPath).TrimStart('.').ToUpper();
            string nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(inputPath);
            string targetFileName = $"{nameWithoutExt}.{targetFormat.ToLower()}";

            SourceFileName.Text = $"{fileName} ({sourceFormat})";
            SourceIcon.Source = Helpers.FileIconHelper.GetIconForExtension(inputPath, size: 16);
            TargetFileName.Text = $"{targetFileName} ({targetFormat.ToUpper()})";
            TargetIcon.Source = Helpers.FileIconHelper.GetIconForExtension("." + targetFormat.ToLower(), size: 16);
        });
    }

    public void UpdateProgress(string fileName, string sourceFormat, string targetFormat, string quality,
                                double percentage, int fileIndex, int totalFiles, TimeSpan? duration = null, TimeSpan? currentTime = null)
    {
        Dispatcher.Invoke(() =>
        {
            if (IsCancelling || IsCompleted) return;

            int safeFileIndex = Math.Max(1, Math.Min(fileIndex, totalFiles));

            if (safeFileIndex != _currentFileIndex)
            {
                _currentFileIndex = safeFileIndex;
                _currentFileStartTime = DateTime.Now;
                _currentFileDuration = duration;
                _lastPercentage = -1;
            }

            string displaySource = string.IsNullOrEmpty(sourceFormat) ? "?" : sourceFormat.ToUpper();
            string displayTarget = string.IsNullOrEmpty(targetFormat) ? "?" : targetFormat.ToUpper();
            
            // Extract just the filename from the full path if needed
            string justFileName = Path.GetFileName(fileName);
            
            // Update source info with shell icon
            SourceFileName.Text = $"{justFileName} ({displaySource})";
            SourceIcon.Source = FileIconHelper.GetIconForExtension(fileName, size: 16);
            
            // Update target info - generate target filename with shell icon
            string nameWithoutExt = Path.GetFileNameWithoutExtension(justFileName);
            string targetFileName = $"{nameWithoutExt}.{displayTarget.ToLower()}";
            TargetFileName.Text = $"{targetFileName} ({displayTarget})";
            TargetIcon.Source = FileIconHelper.GetIconForExtension("." + displayTarget.ToLower(), size: 16);

            if (percentage < _lastPercentage && _lastPercentage > 0)
            {
                percentage = _lastPercentage;
            }
            _lastPercentage = percentage;

            ProgressBar.Value = percentage;
            PercentageText.Text = $"{percentage:F0}%";
            StatusText.Text = StringsWrapper.Instance.ProcessingFile(safeFileIndex, totalFiles);

            // Calcular tiempo restante
            if (duration.HasValue && duration.Value.TotalSeconds > 0 && percentage > 0 && percentage < 100)
            {
                double remainingSeconds = (100 - percentage) / percentage * (DateTime.Now - _currentFileStartTime).TotalSeconds;
                if (remainingSeconds > 0 && remainingSeconds < 3600)
                {
                    var remaining = TimeSpan.FromSeconds(remainingSeconds);
                    TimeRemainingText.Text = StringsWrapper.Instance.TimeRemaining($"{remaining:mm\\:ss}");
                }
                else
                {
                    TimeRemainingText.Text = StringsWrapper.Instance.TimeRemainingCalculating;
                }
            }
            else if (currentTime.HasValue && duration.HasValue && duration.Value.TotalSeconds > 0)
            {
                double remainingSeconds = duration.Value.TotalSeconds - currentTime.Value.TotalSeconds;
                if (remainingSeconds > 0 && remainingSeconds < 3600)
                {
                    var remaining = TimeSpan.FromSeconds(remainingSeconds);
                    TimeRemainingText.Text = StringsWrapper.Instance.TimeRemaining($"{remaining:mm\\:ss}");
                }
                else
                {
                    TimeRemainingText.Text = StringsWrapper.Instance.TimeRemainingCalculating;
                }
            }
            else
            {
                TimeRemainingText.Text = StringsWrapper.Instance.TimeRemainingCalculating;
            }
        });
    }

    public void SetCompleted()
    {
        Dispatcher.Invoke(() =>
        {
            if (IsCancelling) return;

            Interlocked.Exchange(ref _isCompletedFlag, 1);
            ProgressBar.Value = 100;
            PercentageText.Text = "100%";
            StatusText.Text = StringsWrapper.Instance.ConversionCompletedStatus;
            TimeRemainingText.Text = "";
            CancelButton.IsEnabled = false;
            CancelButton.Content = StringsWrapper.Instance.ButtonCompleted;

            _autoCloseTimer = new System.Windows.Threading.DispatcherTimer();
            _autoCloseTimer.Interval = TimeSpan.FromSeconds(2);
            _autoCloseTimer.Tick += (s, e) =>
            {
                _autoCloseTimer?.Stop();
                Close();
            };
            _autoCloseTimer.Start();
        });
    }

    public void SetError(string errorMessage)
    {
        Dispatcher.Invoke(() =>
        {
            if (IsCancelling) return;

            Interlocked.Exchange(ref _isCompletedFlag, 1);
            StatusText.Text = StringsWrapper.Instance.ConversionErrorStatus(errorMessage);
            StatusText.Foreground = Brushes.Red;
            TimeRemainingText.Text = "";
            CancelButton.IsEnabled = true;
            CancelButton.Content = StringsWrapper.Instance.CloseButton;

            _autoCloseTimer = new System.Windows.Threading.DispatcherTimer();
            _autoCloseTimer.Interval = TimeSpan.FromSeconds(2);
            _autoCloseTimer.Tick += (s, e) =>
            {
                _autoCloseTimer?.Stop();
                Close();
            };
            _autoCloseTimer.Start();
        });
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        if (IsCompleted)
        {
            Close();
            return;
        }

        Interlocked.Exchange(ref _isCancellingFlag, 1);
        StatusText.Text = StringsWrapper.Instance.CancellingStatus;
        StatusText.Foreground = (Brush)FindResource("TextSecondaryBrush");
        CancelButton.IsEnabled = false;

        _cancellationTokenSource?.Cancel();

        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (!IsCancelling && !IsCompleted)
        {
            _cancellationTokenSource?.Cancel();
        }
        base.OnClosed(e);
    }
}