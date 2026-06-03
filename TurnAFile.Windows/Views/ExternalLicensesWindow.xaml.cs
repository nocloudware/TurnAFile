using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls;
using TurnAFile.Windows.Resources;

namespace TurnAFile.Windows.Views
{
    /// <summary>
    /// Interaction logic for ExternalLicensesWindow.xaml
    /// </summary>
    public partial class ExternalLicensesWindow : Wpf.Ui.Controls.FluentWindow
    {
        private readonly string _toolsPath;
        private readonly string _libsPath;
        private readonly Dictionary<string, string> _componentPaths = new();
        private string? _selectedComponentPath;

        public ExternalLicensesWindow()
        {
            InitializeComponent();

            // Determine the base path (where the executable is located)
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _toolsPath = Path.Combine(baseDir, "tools");
            _libsPath = Path.Combine(baseDir, "Libs");

            // Load components
            LoadComponents();
        }

    private void LoadComponents()
    {
        var components = new List<string>();
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        // Always include THIRD_PARTY_NOTICES if present
        var noticesPath = Path.Combine(baseDir, "THIRD_PARTY_NOTICES.txt");
        if (!File.Exists(noticesPath))
        {
            var rootDir = Path.GetFullPath(Path.Combine(baseDir, "..\\..\\..\\..\\"));
            noticesPath = Path.Combine(rootDir, "THIRD_PARTY_NOTICES.txt");
        }
        if (File.Exists(noticesPath))
        {
            components.Add("THIRD-PARTY NOTICES (All)");
            _componentPaths["THIRD-PARTY NOTICES (All)"] = "@FILE:" + noticesPath;
        }

        // Scan tools folder for license files
        if (Directory.Exists(_toolsPath))
        {
            foreach (var toolFolder in Directory.GetDirectories(_toolsPath))
            {
                var folderName = Path.GetFileName(toolFolder);
                var licenseFile = FindLicenseFile(toolFolder);
                if (licenseFile != null)
                {
                    components.Add($"Tool: {folderName}");
                    _componentPaths[$"Tool: {folderName}"] = "@FILE:" + licenseFile;
                }
            }
        }

        // Scan Libs folder for license files
        if (Directory.Exists(_libsPath))
        {
            foreach (var libFolder in Directory.GetDirectories(_libsPath))
            {
                var folderName = Path.GetFileName(libFolder);
                var licenseFile = FindLicenseFile(libFolder);
                if (licenseFile != null)
                {
                    components.Add($"Lib: {folderName}");
                    _componentPaths[$"Lib: {folderName}"] = "@FILE:" + licenseFile;
                }
            }
        }

        // Always add all known NuGet packages
        var knownComponents = new (string Name, string Content)[]
        {
            ("ClosedXML (MIT)", "@LIC:MIT License\nCopyright (c) 2013 ClosedXML\n\nhttps://github.com/ClosedXML/ClosedXML"),
            ("DocumentFormat.OpenXml (MIT)", "@LIC:MIT License\nCopyright (c) Microsoft Corporation\n\nhttps://github.com/OfficeDev/Open-XML-SDK"),
            ("QuestPDF (MIT Community)", "@LIC:MIT License (Community)\nCopyright (c) QuestPDF contributors\nFree for individuals, non-profits, businesses under $1M revenue.\n\nhttps://www.questpdf.com/"),
            ("PdfPig (Apache 2.0)", "@LIC:Apache License 2.0\nCopyright (c) PdfPig contributors\n\nhttps://github.com/UglyToad/PdfPig"),
            ("Tesseract (Apache 2.0)", "@LIC:Apache License 2.0\nCopyright (c) Tesseract contributors\n\nhttps://github.com/charlesw/tesseract"),
            ("WPF-UI (MIT)", "@LIC:MIT License\nCopyright (c) WPF-UI contributors\n\nhttps://wpfui.lepo.co/"),
            ("SixLabors.Fonts (Apache 2.0)", "@LIC:Apache License 2.0\nCopyright (c) Six Labors\n\nhttps://sixlabors.com/"),
            ("SkiaSharp (MIT)", "@LIC:MIT License\nCopyright (c) Xamarin Inc / Microsoft Corporation\n\nhttps://github.com/mono/SkiaSharp"),
            ("Microsoft.Win32.SystemEvents (MIT)", "@LIC:MIT License\nCopyright (c) .NET Foundation and Contributors\n\nhttps://dotnet.microsoft.com/"),
            ("System.Management (MIT)", "@LIC:MIT License\nCopyright (c) .NET Foundation and Contributors\n\nhttps://dotnet.microsoft.com/"),
            ("FFmpeg (LGPL/GPL)", "@LIC:GNU Lesser General Public License (LGPL) / GNU General Public License (GPL)\nCopyright (c) the FFmpeg developers\n\nhttps://ffmpeg.org/"),
        };

        foreach (var (name, content) in knownComponents)
        {
            if (!components.Contains(name))
            {
                components.Add(name);
                _componentPaths[name] = content;
            }
        }

        // Populate combo box
        ComponentSelector.ItemsSource = components;
        if (components.Count > 0)
            ComponentSelector.SelectedIndex = 0;
        else
            LicenseTextBlock.Text = "No license entries found.";
    }

    private string? FindLicenseFile(string folderPath)
    {
        var licenseFileNames = new[]
        {
            "LICENSE.txt", "LICENSE.md", "LICENSE",
            "COPYING.txt", "COPYING.md", "COPYING",
            "THIRD_PARTY_NOTICES.txt", "THIRD_PARTY_NOTICES.md",
            "NOTICE.txt", "NOTICE.md", "NOTICE",
            "UNLICENSE", "UNLICENSE.txt",
            "LICENCE.txt", "LICENCE.md", "LICENCE"
        };

        // Check root of folder
        foreach (var name in licenseFileNames)
        {
            var fullPath = Path.Combine(folderPath, name);
            if (File.Exists(fullPath))
                return fullPath;
        }

        // Check first-level subfolders (for extracted packages)
        try
        {
            foreach (var subDir in Directory.GetDirectories(folderPath))
            {
                foreach (var name in licenseFileNames)
                {
                    var fullPath = Path.Combine(subDir, name);
                    if (File.Exists(fullPath))
                        return fullPath;
                }
            }
        }
        catch (UnauthorizedAccessException) { }

        return null;
    }

        private void OnComponentSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComponentSelector.SelectedItem is string componentName &&
                _componentPaths.TryGetValue(componentName, out var path))
            {
                if (path.StartsWith("@FILE:"))
                {
                    var filePath = path[6..];
                    try
                    {
                        var licenseText = File.ReadAllText(filePath);
                        LicenseTextBlock.Text = licenseText;
                        _selectedComponentPath = Path.GetDirectoryName(filePath);
                    }
                    catch (Exception ex)
                    {
                        LicenseTextBlock.Text = $"Error loading license file: {ex.Message}";
                        _selectedComponentPath = null;
                    }
                }
                else if (path.StartsWith("http://") || path.StartsWith("https://"))
                {
                    LicenseTextBlock.Text = $"Full license text available online:\n{path}\n\n" +
                        "Use 'Open Folder' button to open the link in your browser.\n\n" +
                        "License summary:\n{componentName}";
                    _selectedComponentPath = null;
                }
                else if (path.StartsWith("@LIC:"))
                {
                    LicenseTextBlock.Text = path[5..];
                    _selectedComponentPath = null;
                }
                else
                {
                    try
                    {
                        var licenseText = File.ReadAllText(path);
                        LicenseTextBlock.Text = licenseText;
                        _selectedComponentPath = Path.GetDirectoryName(path);
                    }
                    catch (Exception ex)
                    {
                        LicenseTextBlock.Text = $"Error loading: {ex.Message}";
                        _selectedComponentPath = null;
                    }
                }
            }
        }

        private void OnOpenFolderClick(object sender, RoutedEventArgs e)
        {
            if (ComponentSelector.SelectedItem is string componentName &&
                _componentPaths.TryGetValue(componentName, out var path))
            {
                // File-based: open folder
                if (path.StartsWith("@FILE:") && _selectedComponentPath != null && Directory.Exists(_selectedComponentPath))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo { FileName = _selectedComponentPath, UseShellExecute = true, Verb = "open" });
                        return;
                    }
                    catch { }
                }
                // URL-based: open browser
                var url = path switch
                {
                    string p when p.StartsWith("@FILE:") => null,
                    string p when p.StartsWith("@LIC:") => null,
                    string p when p.StartsWith("http://") || p.StartsWith("https://") => p,
                    _ => null
                };
                if (url != null)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                        return;
                    }
                    catch { }
                }
            }

            // Fallback message
            System.Windows.MessageBox.Show(
                StringsWrapper.Instance.NoComponentSelected ?? "Please select a component with a file or URL.",
                "Information",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }
    }
}
