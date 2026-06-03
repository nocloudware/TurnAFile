using System;

namespace TurnAFile.Core.Services;

public class ConversionResult
{
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
    public long InputSize { get; set; }
    public long OutputSize { get; set; }
    public string? OutputPath { get; set; }
}