namespace XISFLib;

/// <summary>Controls display stretching in Windows image adapters.</summary>
public sealed record XisfStretchOptions
{
    /// <summary>Default options for automatic background stretching.</summary>
    public static XisfStretchOptions Auto { get; } = new();
    /// <summary>Options that request the stored display function when it is available.</summary>
    public static XisfStretchOptions DisplayFunction { get; } = new() { UseDisplayFunction = true };

    /// <summary>The desired normalized background level for automatic stretching; default 0.25.</summary>
    public double TargetBackground { get; init; } = 0.25;
    /// <summary>The shadow clipping offset in robust standard-deviation units; default -2.8.</summary>
    public double ShadowsClipping { get; init; } = -2.8;
    /// <summary>Whether channels share an automatically computed stretch.</summary>
    public bool LinkChannels { get; init; } = true;
    /// <summary>Whether the image display function is used when available.</summary>
    public bool UseDisplayFunction { get; init; }
}
