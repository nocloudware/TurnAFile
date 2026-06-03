using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TurnAFile.Windows.Helpers;

/// <summary>
/// Extracts real Windows system file-type icons.
/// Uses two strategies:
///   - ExtractAssociatedIcon: for files that exist on disk (file list)
///   - SHGetFileInfo: for extensions even without real files (context menu formats)
/// </summary>
public static class FileIconHelper
{
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [Flags]
    private enum SHGFI : uint
    {
        Icon = 0x000000100,
        SmallIcon = 0x000000001,
        UseFileAttributes = 0x000000010,
    }

    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

    private static readonly ConcurrentDictionary<string, BitmapSource?> _iconCache = new();
    private static readonly ConcurrentDictionary<string, BitmapSource?> _fileIconCache = new();

    /// <summary>
    /// Gets the icon for a file that exists on disk using ExtractAssociatedIcon.
    /// </summary>
    public static BitmapSource? GetIconForFile(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            return null;

        if (_fileIconCache.TryGetValue(fullPath, out var cached))
            return cached;

        BitmapSource? result = null;

        try
        {
            var icon = Icon.ExtractAssociatedIcon(fullPath);
            if (icon != null)
            {
                result = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions()
                );
                result.Freeze();
                icon.Dispose();
            }
        }
        catch { }

        _fileIconCache[fullPath] = result;
        return result;
    }

    /// <summary>
    /// Gets the Windows system icon for a file extension.
    /// Works even when no file of that type exists on disk.
    /// </summary>
    public static BitmapSource? GetIconForExtension(string extension, int size = 16)
    {
        if (string.IsNullOrEmpty(extension))
            return null;

        if (!extension.StartsWith('.'))
            extension = "." + extension;

        var ext = extension.ToLowerInvariant();
        var cacheKey = $"{ext}_{size}";

        if (_iconCache.TryGetValue(cacheKey, out var cached))
            return cached;

        BitmapSource? result = null;

        try
        {
            var fakePath = $"file{ext}";
            SHFILEINFO shfi = new SHFILEINFO();
            uint flags = (uint)SHGFI.Icon | (uint)SHGFI.SmallIcon | (uint)SHGFI.UseFileAttributes;

            IntPtr hImg = SHGetFileInfo(
                fakePath,
                FILE_ATTRIBUTE_NORMAL,
                ref shfi,
                (uint)Marshal.SizeOf(typeof(SHFILEINFO)),
                flags
            );

            if (hImg != IntPtr.Zero && shfi.hIcon != IntPtr.Zero)
            {
                result = Imaging.CreateBitmapSourceFromHIcon(
                    shfi.hIcon,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions()
                );
                result.Freeze();
                DestroyIcon(shfi.hIcon);
            }
        }
        catch { }

        _iconCache[cacheKey] = result;
        return result;
    }

    public static void ClearCache()
    {
        _iconCache.Clear();
        _fileIconCache.Clear();
    }
}
