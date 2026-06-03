using System.Text.Json;
using TurnAFile.Core.Models;

namespace TurnAFile.Core.Services;

public class ConfigurationService
{
    private readonly string _configPath;
    private AppSettings? _settings;

    public ConfigurationService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appFolder = Path.Combine(appData, "TurnAFile");

        if (!Directory.Exists(appFolder))
            Directory.CreateDirectory(appFolder);

        _configPath = Path.Combine(appFolder, "settings.json");
    }

    public AppSettings LoadSettings()
    {
        if (_settings != null)
            return _settings;

        try
        {
            if (File.Exists(_configPath))
            {
                string json = File.ReadAllText(_configPath);
                _settings = JsonSerializer.Deserialize<AppSettings>(json);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error cargando configuración: {ex.Message}");
        }

        _settings ??= new AppSettings();

        // Asegurar valores por defecto
        if (string.IsNullOrEmpty(_settings.ActiveVideoFormat))
            _settings.ActiveVideoFormat = "MP4";
        if (string.IsNullOrEmpty(_settings.ActiveAudioFormat))
            _settings.ActiveAudioFormat = "MP3";
        if (string.IsNullOrEmpty(_settings.ActiveImageFormat))
            _settings.ActiveImageFormat = "JPG";
        if (string.IsNullOrEmpty(_settings.ActiveDocumentFormat))
            _settings.ActiveDocumentFormat = "DOCX";
        if (string.IsNullOrEmpty(_settings.ActiveDataFormat))
            _settings.ActiveDataFormat = "XLSX";

        return _settings;
    }

    public void SaveSettings(AppSettings settings)
    {
        try
        {
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
            _settings = settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error guardando configuración: {ex.Message}");
        }
    }

    public void SaveQualitySettings(VideoQuality videoQuality, AudioQuality audioQuality, ImageScaling imageScaling)
    {
        var settings = LoadSettings();
        settings.DefaultVideoQuality = videoQuality;
        settings.DefaultAudioQuality = audioQuality;
        settings.DefaultImageScaling = imageScaling;
        SaveSettings(settings);
    }

    public (VideoQuality video, AudioQuality audio, ImageScaling image) LoadQualitySettings()
    {
        var settings = LoadSettings();
        return (settings.DefaultVideoQuality, settings.DefaultAudioQuality, settings.DefaultImageScaling);
    }
}