namespace XISFLib;

public static partial class XisfKnownProperties
{

    /// <summary>Predefined properties in the Processing group.</summary>
    public static class Processing
    {
        /// <summary>Processing:Description (String).</summary>
        public static XisfPropertyKey<string> Description { get; } = new("Processing:Description", "String");
        /// <summary>Processing:History (String).</summary>
        public static XisfPropertyKey<string> History { get; } = new("Processing:History", "String");
    }
}
