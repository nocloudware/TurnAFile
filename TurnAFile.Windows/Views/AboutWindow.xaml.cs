using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using TurnAFile.Core.Services;
using TurnAFile.Windows.Resources;

namespace TurnAFile.Windows.Views;

public partial class AboutWindow : Wpf.Ui.Controls.FluentWindow
{
    public AboutWindow()
    {
        try
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error initializing About window: {ex.Message}\n\n{ex.StackTrace}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Close();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateVersionDisplay();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error loading About window: {ex.Message}\n\n{ex.StackTrace}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void UpdateVersionDisplay()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"{StringsWrapper.Instance.AboutVersion} {version?.Major ?? 1}.{version?.Minor ?? 0}.{version?.Build ?? 0}";
    }

    private void OnHyperlinkRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(StringsWrapper.Instance.CouldNotOpenLink(ex.Message), StringsWrapper.Instance.ErrorTitle,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        CheckUpdatesButton.Content = "⏳ Verificando...";

        try
        {
            var updateService = new UpdateService();
            var updateInfo = await updateService.CheckForUpdatesAsync();

            CheckUpdatesButton.IsEnabled = true;
            CheckUpdatesButton.Content = "🔍 Buscar actualizaciones";

            if (updateInfo == null)
            {
                MessageBox.Show(
                    "No se pudo verificar la versión más reciente.\n\n" +
                    "El repositorio del proyecto no tiene releases publicados aún.\n\n" +
                    "Puedes revisar manualmente en:\n" +
                    "https://github.com/nocloudware/TurnAFile/releases",
                    "Sin actualizaciones disponibles",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (updateInfo.IsNewerVersion)
            {
                var currentVersion = System.Reflection.Assembly.GetExecutingAssembly()
                    .GetName().Version?.ToString() ?? "0.0.0";

                var dialog = new UpdateAvailableDialog(updateInfo, currentVersion);
                dialog.Owner = this;
                dialog.ShowDialog();
            }
            else
            {
                MessageBox.Show(
                    "¡Estás usando la última versión de TurnAFile!",
                    "Actualizado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            CheckUpdatesButton.IsEnabled = true;
            CheckUpdatesButton.Content = "🔍 Buscar actualizaciones";

            MessageBox.Show(
                $"Error al verificar actualizaciones: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnViewExternalLicensesClick(object sender, RoutedEventArgs e)
    {
        var licensesWindow = new ExternalLicensesWindow();
        licensesWindow.Owner = this;
        licensesWindow.ShowDialog();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
            Close();
    }
}   