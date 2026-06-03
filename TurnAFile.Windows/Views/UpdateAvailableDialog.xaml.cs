using System;
using System.Diagnostics;
using System.Windows;
using TurnAFile.Core.Services;

namespace TurnAFile.Windows.Views;

public partial class UpdateAvailableDialog : Window
{
    private readonly UpdateInfo _updateInfo;
    private readonly UpdateService _updateService;
    private readonly string _currentVersion;

    public UpdateAvailableDialog(UpdateInfo updateInfo, string currentVersion)
    {
        InitializeComponent();
        _updateInfo = updateInfo;
        _updateService = new UpdateService();
        _currentVersion = currentVersion;

        LoadUpdateInfo();
    }

    private void LoadUpdateInfo()
    {
        VersionInfoText.Text = $"Nueva versión: v{_updateInfo.Version}";
        CurrentVersionText.Text = $"v{_currentVersion} (actual)";
        ReleaseNotesText.Text = !string.IsNullOrEmpty(_updateInfo.ReleaseNotes)
            ? _updateInfo.ReleaseNotes
            : "Sin notas de versión disponibles.";

        Title = $"TurnAFile v{_updateInfo.Version} disponible";
    }

    private void OnDownloadClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!string.IsNullOrEmpty(_updateInfo.DownloadUrl))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _updateInfo.DownloadUrl,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error al abrir el enlace de descarga: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        Close();
    }

    private void OnRemindLaterClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnIgnoreClick(object sender, RoutedEventArgs e)
    {
        _updateService.IgnoreVersion(_updateInfo.Version);
        Close();
    }
}