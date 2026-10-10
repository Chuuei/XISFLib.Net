namespace XISFLib;

public static partial class XisfKnownProperties
{

    /// <summary>Predefined properties in the Organization group.</summary>
    public static class Organization
    {
        /// <summary>Organization:EmailAddress (String).</summary>
        public static XisfPropertyKey<string> EmailAddress { get; } = new("Organization:EmailAddress", "String");
        /// <summary>Organization:Name (String).</summary>
        public static XisfPropertyKey<string> Name { get; } = new("Organization:Name", "String");
        /// <summary>Organization:PostalAddress (String).</summary>
        public static XisfPropertyKey<string> PostalAddress { get; } = new("Organization:PostalAddress", "String");
        /// <summary>Organization:Website (String).</summary>
        public static XisfPropertyKey<string> Website { get; } = new("Organization:Website", "String");
    }
}
