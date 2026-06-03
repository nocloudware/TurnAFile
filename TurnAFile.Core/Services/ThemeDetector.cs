using System;
using Microsoft.Win32;

namespace TurnAFile.Core.Services;

public enum AppTheme
{
    Light,
    Dark,
    Unknown
}

public static class ThemeDetector
{
    public static AppTheme GetCurrentTheme()
    {
        try
        {
#pragma warning disable CA1416
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int value)
            {
                return value == 1 ? AppTheme.Light : AppTheme.Dark;
            }
#pragma warning restore CA1416
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al leer tema: {ex.Message}");
        }

        return AppTheme.Light;
    }

    private static EventHandler? _currentHandler;

    public static void ListenToThemeChanges(EventHandler handler)
    {
        _currentHandler = handler;
#pragma warning disable CA1416
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
#pragma warning restore CA1416
    }

    public static void StopListening(EventHandler handler)
    {
        if (_currentHandler == handler)
            _currentHandler = null;
#pragma warning disable CA1416
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
#pragma warning restore CA1416
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
        {
            _currentHandler?.Invoke(sender, e);
        }
    }

    public static bool IsDarkTheme()
    {
        return GetCurrentTheme() == AppTheme.Dark;
    }

    public static bool IsLightTheme()
    {
        return GetCurrentTheme() == AppTheme.Light;
    }
}