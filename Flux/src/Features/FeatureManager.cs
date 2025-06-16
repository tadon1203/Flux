using System.Collections.Generic;
using System.Linq;
using Flux.Graphics;

namespace Flux.Features;

/// <summary>
///     Manages all features, including their initialization and lifecycle.
/// </summary>
public static class FeatureManager
{
    private static readonly List<Feature> Features = new();

    public static void Initialize()
    {
        Logger.Info("Initializing features...");
    }

    private static void RegisterFeature(Feature feature)
    {
        if (Features.Any(f => f.Name == feature.Name))
        {
            Logger.Warning($"Feature with name '{feature.Name}' is already registered.");
            return;
        }

        Features.Add(feature);
    }

    public static T GetFeature<T>() where T : Feature
    {
        return Features.OfType<T>().FirstOrDefault();
    }

    /// <summary>
    ///     Gets all registered features for a given category.
    /// </summary>
    /// <param name="category">The feature category.</param>
    /// <returns>An enumerable of features in the specified category.</returns>
    public static IEnumerable<Feature> GetFeaturesByCategory(FeatureCategory category)
    {
        return Features.Where(f => f.Category == category);
    }

    public static void Update()
    {
        foreach (Feature feature in Features)
        {
            // Check for toggle key presses regardless of the feature's current state.
            feature.CheckToggleKey();

            // Only call OnUpdate if the feature is enabled.
            if (feature.IsEnabled)
                feature.OnUpdate();
        }
    }

    public static void Render(IRenderContext context)
    {
        foreach (Feature feature in Features)
        {
            if (feature.IsEnabled)
                feature.OnRender(context);
        }
    }
}