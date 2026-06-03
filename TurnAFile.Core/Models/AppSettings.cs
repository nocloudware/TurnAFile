using System;
using TurnAFile.Core.Services;

namespace TurnAFile.Core.Models;

public class AppSettings
{
    // Performance
    public int MaxCpuCores { get; set; } = 0;
    public bool RunInBackground { get; set; } = true;
    public string ProcessPriority { get; set; } = "Normal";

    // Comportamiento
    public string DefaultOutputFolder { get; set; } = "SameAsOriginal";
    public bool EnableNotifications { get; set; } = true;
    public bool ShowNotificationsToast { get; set; } = true;

    // Formatos activos
    public string ActiveVideoFormat { get; set; } = "MP4";
    public string ActiveAudioFormat { get; set; } = "MP3";
    public string ActiveImageFormat { get; set; } = "JPG";
    public string ActiveDocumentFormat { get; set; } = "DOCX";
    public string ActiveDataFormat { get; set; } = "XLSX";

    // Idioma activo (código ISO 639-1: en, es, fr, de, zh, ja, pt)
    public string ActiveLanguage { get; set; } = string.Empty;

    // Calidades por defecto
    public VideoQuality DefaultVideoQuality { get; set; } = VideoQuality.Medium;
    public AudioQuality DefaultAudioQuality { get; set; } = AudioQuality.Medium;
    public ImageScaling DefaultImageScaling { get; set; } = ImageScaling.Original;

    // Updates
    public DateTime? LastUpdateCheckDate { get; set; }
    public string IgnoredUpdateVersion { get; set; } = string.Empty;
    public string LastVersionRun { get; set; } = string.Empty;
}