namespace XISFLib;

/// <summary>How a third FITS axis is interpreted during image conversion.</summary>
public enum FitsAxisInterpretation
{
    /// <summary>Retains every axis as a scientific dimension; this is the default.</summary>
    PreserveDimensions,
    /// <summary>Explicitly interprets a third axis of length three or four as RGB or RGBA.</summary>
    RgbOrRgba
}

/// <summary>Whether a conversion may omit content or change its representation.</summary>
public enum ConversionLossPolicy
{
    /// <summary>Rejects conversions with reported losses; this is the default.</summary>
    Reject,
    /// <summary>Allows reported losses and transformations.</summary>
    Allow
}

/// <summary>The effect of a conversion on a source feature.</summary>
public enum ConversionEffect
{
    /// <summary>The feature is retained.</summary>
    Preserved,
    /// <summary>The feature is represented differently.</summary>
    Transformed,
    /// <summary>Some source information is omitted.</summary>
    Omitted
}

/// <summary>A feature-level conversion finding.</summary>
/// <param name="Code">Stable finding code.</param>
/// <param name="Effect">The effect on the source content.</param>
/// <param name="Message">The explanation, including the affected image or HDU.</param>
public sealed record ConversionFinding(string Code, ConversionEffect Effect, string Message);

/// <summary>A converted value with explicit preservation and loss findings.</summary>
/// <typeparam name="T">The converted content type.</typeparam>
public sealed class ConversionResult<T>
{
    internal ConversionResult(T value, IEnumerable<ConversionFinding> findings)
    { Value = value; Findings = Array.AsReadOnly(findings.ToArray()); }
    /// <summary>The converted content.</summary>
    public T Value { get; }
    /// <summary>Preserved, transformed and omitted feature descriptions.</summary>
    public IReadOnlyList<ConversionFinding> Findings { get; }
    /// <summary>Whether any finding describes omitted information.</summary>
    public bool HasLosses => Findings.Any(f => f.Effect == ConversionEffect.Omitted);
    internal void Enforce(ConversionLossPolicy policy)
    {
        ApiContracts.Defined(policy, nameof(policy));
        if (policy == ConversionLossPolicy.Reject && HasLosses) throw new ConversionLossException(Findings);
    }
}

/// <summary>Reports conversion losses that the caller has not explicitly allowed.</summary>
public sealed class ConversionLossException : InvalidOperationException
{
    internal ConversionLossException(IReadOnlyList<ConversionFinding> findings)
        : base("Conversion would omit content. Inspect Findings and explicitly choose ConversionLossPolicy.Allow when acceptable.") { Findings = findings; }
    /// <summary>The conversion findings that caused rejection.</summary>
    public IReadOnlyList<ConversionFinding> Findings { get; }
}
