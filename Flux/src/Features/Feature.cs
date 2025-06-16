using Flux.Graphics;
using UnityEngine;

namespace Flux.Features;

/// <summary>
///     Represents the base class for all features.
/// </summary>
public abstract class Feature
{
    private bool _isEnabled;

    public string Name { get; }
    public FeatureCategory Category { get; }
    public KeyCode ToggleKey { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the feature is enabled.
    ///     Enabling or disabling a feature will call the OnEnable/OnDisable methods.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
                return;
            _isEnabled = value;
            if (_isEnabled)
                OnEnable();
            else
                OnDisable();
        }
    }

    protected Feature(string name, FeatureCategory category)
    {
        Name = name;
        Category = category;
    }
    
    /// <summary>
    /// Enables the feature.
    /// </summary>
    public void Enable() => IsEnabled = true;

    /// <summary>
    /// Disables the feature.
    /// </summary>
    public void Disable() => IsEnabled = false;
    
    /// <summary>
    /// Toggles the enabled state of the feature.
    /// </summary>
    public void Toggle() => IsEnabled = !IsEnabled;
    
    /// <summary>
    /// Checks if the toggle key was pressed and updates the feature's state.
    /// This is called automatically by the FeatureManager.
    /// </summary>
    public void CheckToggleKey()
    {
        if (ToggleKey != KeyCode.None && Input.GetKeyDown(ToggleKey))
        {
            Toggle();
        }
    }

    /// <summary>
    ///     Called when the feature is enabled.
    /// </summary>
    public virtual void OnEnable() { }

    /// <summary>
    ///     Called when the feature is disabled.
    /// </summary>
    public virtual void OnDisable() { }

    /// <summary>
    ///     Called every frame. Used for game logic updates.
    /// </summary>
    public virtual void OnUpdate() { }

    /// <summary>
    ///     Called during the rendering phase. Use the context to issue drawing commands.
    ///     Example:
    ///     <code>context.DrawText("Hello Bold", new Vector2(10, 10), Colors.White, 16f, FontFamily.Inter, FontWeight.Bold);</code>
    /// </summary>
    /// <param name="context">The render context for issuing drawing commands.</param>
    public virtual void OnRender(IRenderContext context) { }
}