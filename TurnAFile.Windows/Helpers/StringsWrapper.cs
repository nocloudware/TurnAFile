using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Markup;

namespace TurnAFile.Windows.Resources;

/// <summary>
/// Singleton wrapper for the auto-generated Strings resource class.
/// Provides INotifyPropertyChanged for XAML binding support.
/// </summary>
public sealed class StringsWrapper : INotifyPropertyChanged
{
    private static StringsWrapper? _instance;
    public static StringsWrapper Instance => _instance ??= new StringsWrapper();

    private StringsWrapper() { }

    // Expose all properties from the auto-generated Strings class
    public string AppTitle => Strings.AppTitle;
    public string AppSubtitle => Strings.AppSubtitle;
    public string ThemeToggleTooltip => Strings.ThemeToggleTooltip;

    public string SelectedFilesTitle => Strings.SelectedFilesTitle;
    public string DragFilesMessage => Strings.DragFilesMessage;
    public string SelectFilesButton => Strings.SelectFilesButton;
    public string OutputFolderDefault => Strings.OutputFolderDefault;
    public string SelectOutputFolderTitle => Strings.SelectOutputFolderTitle;
    public string ChangeButton => Strings.ChangeButton;

    public string FormatHeader => Strings.FormatHeader;
    public string QualityHigh => Strings.QualityHigh;
    public string QualityMedium => Strings.QualityMedium;
    public string QualityLow => Strings.QualityLow;

    public string VideoLabel => Strings.VideoLabel;
    public string AudioLabel => Strings.AudioLabel;
    public string ImageLabel => Strings.ImageLabel;
    public string DocumentLabel => Strings.DocumentLabel;
    public string DataLabel => Strings.DataLabel;

    public string AboutButton => Strings.AboutButton;
    public string ConvertButton => Strings.ConvertButton;
    public string BuyButton => Strings.BuyButton;
    public string DonateButton => Strings.DonateButton;

    public string ContextMenuConvertTo => Strings.ContextMenuConvertTo;
    public string ContextMenuQuality => Strings.ContextMenuQuality;
    public string ContextMenuQualityHigh => Strings.ContextMenuQualityHigh;
    public string ContextMenuQualityMedium => Strings.ContextMenuQualityMedium;
    public string ContextMenuQualityLow => Strings.ContextMenuQualityLow;
    public string ContextMenuExtractOCR => Strings.ContextMenuExtractOCR;
    public string ContextMenuSaveAsTXT => Strings.ContextMenuSaveAsTXT;
    public string ContextMenuSaveAsDOCX => Strings.ContextMenuSaveAsDOCX;
    public string ContextMenuSaveAsXLSX => Strings.ContextMenuSaveAsXLSX;
    public string ContextMenuRemove => Strings.ContextMenuRemove;
    public string ContextMenuClearAll => Strings.ContextMenuClearAll;

    public string ShellMenuAppName => Strings.ShellMenuAppName;
    public string ShellMenuAddEntry => Strings.ShellMenuAddEntry;
    public string ShellMenuConvertWith => Strings.ShellMenuConvertWith;
    public string ShellMenuQualityHigh => Strings.ShellMenuQualityHigh;
    public string ShellMenuQualityMedium => Strings.ShellMenuQualityMedium;
    public string ShellMenuQualityLow => Strings.ShellMenuQualityLow;
    public string ShellMenuExtractOCR => Strings.ShellMenuExtractOCR;
    public string ShellMenuSaveAsTXT => Strings.ShellMenuSaveAsTXT;
    public string ShellMenuSaveAsDOCX => Strings.ShellMenuSaveAsDOCX;
    public string ShellMenuSaveAsXLSX => Strings.ShellMenuSaveAsXLSX;
    public string ShellMenuConvertTo(string format) => string.Format(Strings.ShellMenuConvertTo, format);

    public string ConversionInProgress => Strings.ConversionInProgress;
    public string SelectFilesMessage => Strings.SelectFilesMessage;
    public string NoFilesSelected => Strings.NoFilesSelected;
    public string FFmpegNotFound => Strings.FFmpegNotFound;
    public string FileTypeNotSupported => Strings.FileTypeNotSupported;
    public string ErrorConvertingFile => Strings.ErrorConvertingFile;
    public string ConversionCancelled => Strings.ConversionCancelled;
    public string ConversionCompleted => Strings.ConversionCompleted;
    public string ConversionError => Strings.ConversionError;

    // ─────────────────────────────────────────────────────────────────────
    // ACERCA DE
    // ─────────────────────────────────────────────────────────────────────

    public string AboutTitle => Strings.AboutTitle;
    public string AboutVersion => Strings.AboutVersion;



    public string ConversionProgress_Title => Strings.ConversionProgress_Title;
    public string Conversion_progressLabel => Strings.Conversion_progressLabel;
    public string Conversion_cancelButton => Strings.Conversion_cancelButton;
    public string CancelButton => Strings.CancelButton;
    public string CloseButton => Strings.CloseButton;
    public string ExitButton => Strings.ExitButton;

    public string SettingsLanguage => Strings.SettingsLanguage;
    public string LanguageEnglish => Strings.LanguageEnglish;
    public string LanguageSpanish => Strings.LanguageSpanish;
    public string LanguageFrench => Strings.LanguageFrench;
    public string LanguageGerman => Strings.LanguageGerman;
    public string LanguageChinese => Strings.LanguageChinese;
    public string LanguageJapanese => Strings.LanguageJapanese;
    public string LanguagePortuguese => Strings.LanguagePortuguese;

    // Conversion Progress
    public string ProcessingFile(int current, int total) => string.Format(Strings.ProcessingFile, current, total);
    public string TimeRemainingCalculating => Strings.TimeRemainingCalculating;
    public string TimeRemaining(string time) => string.Format(Strings.TimeRemaining, time);
    public string ConversionCompletedStatus => Strings.ConversionCompletedStatus;
    public string ConversionErrorStatus(string error) => string.Format(Strings.ConversionErrorStatus, error);
    public string CancellingStatus => Strings.CancellingStatus;
    public string ButtonCompleted => Strings.ButtonCompleted;
    public string ButtonError => Strings.ButtonError;

    // Context Menu
    public string ConvertToFormat(string format) => string.Format(Strings.ConvertToFormat, format);
    public string QualityAlta => Strings.QualityAlta;
    public string QualityMedia => Strings.QualityMedia;
    public string QualityBaja => Strings.QualityBaja;

    // Dialogs
    public string SelectOutputFolderDialog => Strings.SelectOutputFolderDialog;

    // Error Messages
    public string ErrorOpeningBrowser(string message) => string.Format(Strings.ErrorOpeningBrowser, message);
    public string ErrorTitle => Strings.ErrorTitle;
    public string ErrorReadingFile(string message) => string.Format(Strings.ErrorReadingFile, message);
    public string InvalidEmail => Strings.InvalidEmail;
    public string FileNotFound(string path) => string.Format(Strings.FileNotFound, path);
    public string FFmpegNotFoundMessage => Strings.FFmpegNotFoundMessage;
    public string CouldNotOpenLink(string message) => string.Format(Strings.CouldNotOpenLink, message);
    public string CouldNotConvertFile => Strings.CouldNotConvertFile;
    public string ConversionCancelledMsg => Strings.ConversionCancelledMsg;

    // New conversion error messages
    public string OCRConversionFailed => Strings.OCRConversionFailed;
    public string PandocNotFoundMessage(string path) => string.Format(Strings.PandocNotFoundMessage, path);
    public string DocumentConversionFailed => Strings.DocumentConversionFailed;
    public string UnsupportedDataConversion(string from, string to) => string.Format(Strings.UnsupportedDataConversion, from, to);
    public string DataConversionFailed => Strings.DataConversionFailed;
    public string CouldNotGeneratePDF => Strings.CouldNotGeneratePDF;
    public string ErrorGeneratingPDF => Strings.ErrorGeneratingPDF;
    public string PDFGeneratedError => Strings.PDFGeneratedError;
    public string ConversionCancelledStatus => Strings.ConversionCancelledStatus;
    public string GeneralError => Strings.GeneralError;

    // About Window Credits
    public string AboutCredits => Strings.AboutCredits;
    public string AboutDevelopedBy => Strings.AboutDevelopedBy;
    public string AboutFormatIcons => Strings.AboutFormatIcons;
    public string AboutIconsDash => Strings.AboutIconsDash;
    public string AboutFFmpegDesc => Strings.AboutFFmpegDesc;
    public string AboutClosedXML => Strings.AboutClosedXML;
    public string AboutClosedXMLDesc => Strings.AboutClosedXMLDesc;
    public string AboutQuestPDF => Strings.AboutQuestPDF;
    public string AboutQuestPDFDesc => Strings.AboutQuestPDFDesc;
    public string AboutOpenXML => Strings.AboutOpenXML;
    public string AboutOpenXMLDesc => Strings.AboutOpenXMLDesc;
    public string AboutSpecialThanks => Strings.AboutSpecialThanks;
    public string AboutSpecialThanksMessage => Strings.AboutSpecialThanksMessage;
    public string AboutTechnologiesUsed => Strings.AboutTechnologiesUsed;
    public string AboutTechList => Strings.AboutTechList;
    public string AboutLicense => Strings.AboutLicense;
    public string AboutLicenseInfo => Strings.AboutLicenseInfo;
    public string AboutViewTerms => Strings.AboutViewTerms;
    
    // External Licenses Window
    public string ExternalLicensesTitle => Strings.ExternalLicensesTitle;
    public string ExternalLicensesHeader => Strings.ExternalLicensesHeader;
    public string ExternalLicensesDescription => Strings.ExternalLicensesDescription;
    public string SelectComponent => Strings.SelectComponent;
    public string OpenFolderButton => Strings.OpenFolderButton;
    public string NoExternalLicensesFound => Strings.NoExternalLicensesFound;
    public string NoComponentSelected => Strings.NoComponentSelected;
    public string ViewExternalLicensesButton => Strings.ViewExternalLicensesButton;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Refreshes all bindings in this wrapper.
    /// </summary>
    public static void Refresh()
    {
        Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs(null));
    }

    /// <summary>
    /// Changes the UI culture and refreshes all bindings.
    /// </summary>
    public static void SetCulture(CultureInfo culture)
    {
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Strings.Culture = culture;
        Refresh();

        // Update all open windows
        foreach (Window window in Application.Current.Windows)
        {
            window.Dispatcher.Invoke(() =>
            {
                window.Language = XmlLanguage.GetLanguage(culture.IetfLanguageTag);
            });
        }
    }

    public static CultureInfo CurrentCulture => CultureInfo.CurrentUICulture;

    public static Dictionary<string, CultureInfo> SupportedLanguages => new()
    {
        { "English", new CultureInfo("en") },
        { "Español", new CultureInfo("es") },
        { "Français", new CultureInfo("fr") },
        { "Deutsch", new CultureInfo("de") },
        { "中文", new CultureInfo("zh") },
        { "日本語", new CultureInfo("ja") },
        { "Português", new CultureInfo("pt") }
    };
}
