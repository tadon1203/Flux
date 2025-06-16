using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Vortice.DirectWrite;

namespace Flux.Graphics.Font;

/// <summary>
///     Manages loading and lifecycle of custom fonts.
/// </summary>
public static class FontManager
{
    private static readonly Dictionary<FontFamily, string> FontFamilyNames = new()
    {
        [FontFamily.Inter] = "Inter"
    };

    private static readonly List<string> FontResources =
    [
        "Flux.resources.Inter-Regular.ttf",
        "Flux.resources.Inter-Bold.ttf"
    ];

    private static readonly List<string> TempFontFiles = [];
    private static bool _isInitialized;

    /// <summary>
    ///     Initializes the FontManager, loads custom fonts from embedded resources, and creates a font collection.
    /// </summary>
    /// <param name="factory">The DirectWrite factory.</param>
    /// <returns>An <see cref="IDWriteFontCollection" /> containing the loaded custom fonts.</returns>
    public static IDWriteFontCollection Initialize(IDWriteFactory factory)
    {
        if (_isInitialized)
        {
            Logger.Warning("FontManager is already initialized.");
            return null;
        }

        Logger.Info("Initializing FontManager and loading custom fonts...");

        using var factory5 = factory.QueryInterface<IDWriteFactory5>();
        using IDWriteFontSetBuilder1 fontSetBuilder = factory5.CreateFontSetBuilder();

        foreach (string resourceName in FontResources)
        {
            try
            {
                string tempFilePath = ExtractResourceToFile(resourceName);
                if (string.IsNullOrEmpty(tempFilePath))
                    continue;

                using IDWriteFontFile fontFile = factory5.CreateFontFileReference(tempFilePath);
                fontSetBuilder.AddFontFile(fontFile);

                TempFontFiles.Add(tempFilePath);
                Logger.Info($"Successfully loaded font from resource '{resourceName}'.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load font from resource '{resourceName}': {ex.Message}");
            }
        }

        using IDWriteFontSet fontSet = fontSetBuilder.CreateFontSet();
        IDWriteFontCollection1 fontCollection = factory5.CreateFontCollectionFromFontSet(fontSet);

        _isInitialized = true;
        return fontCollection;
    }

    public static string GetFontFamilyName(FontFamily font)
    {
        return FontFamilyNames.GetValueOrDefault(font, "Arial"); // Fallback to Arial
    }

    public static void Dispose()
    {
        if (!_isInitialized)
            return;

        Logger.Info("Disposing FontManager and cleaning up temporary font files...");
        foreach (string file in TempFontFiles)
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to delete temporary font file '{file}': {ex.Message}");
            }
        }

        TempFontFiles.Clear();
        _isInitialized = false;
    }

    private static string ExtractResourceToFile(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using Stream stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            Logger.Error($"Embedded resource not found: {resourceName}. Make sure the build action is 'Embedded Resource'.");
            return null;
        }

        string tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.ttf");
        using var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write);
        stream.CopyTo(fileStream);

        return tempFilePath;
    }
}