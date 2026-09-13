namespace XISFLib;

public sealed record XisfStretchOptions
{
    public static XisfStretchOptions Auto { get; } = new();
    public static XisfStretchOptions DisplayFunction { get; } = new() { UseDisplayFunction = true };

    public double TargetBackground { get; init; } = 0.25;
    public double ShadowsClipping { get; init; } = -2.8;
    public bool LinkChannels { get; init; } = true;
    public bool UseDisplayFunction { get; init; }
}