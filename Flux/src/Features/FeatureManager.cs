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
    
    public static void Update()
    {
        foreach (Feature feature in Features)
        {
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