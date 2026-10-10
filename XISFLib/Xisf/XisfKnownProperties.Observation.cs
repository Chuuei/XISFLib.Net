namespace XISFLib;

public static partial class XisfKnownProperties
{

    /// <summary>Predefined properties in the Observation group.</summary>
    public static class Observation
    {
        /// <summary>Observation:BibliographicReferences (String).</summary>
        public static XisfPropertyKey<string> BibliographicReferences { get; } = new("Observation:BibliographicReferences", "String");
        /// <summary>Observation:CelestialReferenceSystem (String).</summary>
        public static XisfPropertyKey<string> CelestialReferenceSystem { get; } = new("Observation:CelestialReferenceSystem", "String");
        /// <summary>Observation:Description (String).</summary>
        public static XisfPropertyKey<string> Description { get; } = new("Observation:Description", "String");
        /// <summary>Observation:Equinox (Float64; units: years).</summary>
        public static XisfPropertyKey<double> Equinox { get; } = new("Observation:Equinox", "Float64", unit: "years");
        /// <summary>Observation:GeodeticReferenceSystem (String).</summary>
        public static XisfPropertyKey<string> GeodeticReferenceSystem { get; } = new("Observation:GeodeticReferenceSystem", "String");
        /// <summary>Observation:RelatedResources (String).</summary>
        public static XisfPropertyKey<string> RelatedResources { get; } = new("Observation:RelatedResources", "String");
        /// <summary>Observation:Title (String).</summary>
        public static XisfPropertyKey<string> Title { get; } = new("Observation:Title", "String");

        /// <summary>Predefined properties in the Center group.</summary>
        public static class Center
        {
            /// <summary>Observation:Center:Dec (Float64; units: degrees).</summary>
            public static XisfPropertyKey<double> Dec { get; } = new("Observation:Center:Dec", "Float64", unit: "degrees");
            /// <summary>Observation:Center:RA (Float64; units: degrees).</summary>
            public static XisfPropertyKey<double> RA { get; } = new("Observation:Center:RA", "Float64", unit: "degrees");
            /// <summary>Observation:Center:X (Float64; units: pixels).</summary>
            public static XisfPropertyKey<double> X { get; } = new("Observation:Center:X", "Float64", unit: "pixels");
            /// <summary>Observation:Center:Y (Float64; units: pixels).</summary>
            public static XisfPropertyKey<double> Y { get; } = new("Observation:Center:Y", "Float64", unit: "pixels");
        }

        /// <summary>Predefined properties in the Location group.</summary>
        public static class Location
        {
            /// <summary>Observation:Location:Elevation (Float64; units: meters).</summary>
            public static XisfPropertyKey<double> Elevation { get; } = new("Observation:Location:Elevation", "Float64", unit: "meters");
            /// <summary>Observation:Location:Latitude (Float64; units: degrees).</summary>
            public static XisfPropertyKey<double> Latitude { get; } = new("Observation:Location:Latitude", "Float64", unit: "degrees");
            /// <summary>Observation:Location:Longitude (Float64; units: degrees).</summary>
            public static XisfPropertyKey<double> Longitude { get; } = new("Observation:Location:Longitude", "Float64", unit: "degrees");
            /// <summary>Observation:Location:Name (String).</summary>
            public static XisfPropertyKey<string> Name { get; } = new("Observation:Location:Name", "String");
        }

        /// <summary>Predefined properties in the Meteorology group.</summary>
        public static class Meteorology
        {
            /// <summary>Observation:Meteorology:AmbientTemperature (Float32; units: degrees Celsius).</summary>
            public static XisfPropertyKey<float> AmbientTemperature { get; } = new("Observation:Meteorology:AmbientTemperature", "Float32", unit: "degrees Celsius");
            /// <summary>Observation:Meteorology:AtmosphericPressure (Float32; units: hectopascals).</summary>
            public static XisfPropertyKey<float> AtmosphericPressure { get; } = new("Observation:Meteorology:AtmosphericPressure", "Float32", unit: "hectopascals");
            /// <summary>Observation:Meteorology:RelativeHumidity (Float32; units: percent).</summary>
            public static XisfPropertyKey<float> RelativeHumidity { get; } = new("Observation:Meteorology:RelativeHumidity", "Float32", unit: "percent");
            /// <summary>Observation:Meteorology:WindDirection (Float32; units: degrees (north azimuth)).</summary>
            public static XisfPropertyKey<float> WindDirection { get; } = new("Observation:Meteorology:WindDirection", "Float32", unit: "degrees (north azimuth)");
            /// <summary>Observation:Meteorology:WindGust (Float32; units: kilometers/hour).</summary>
            public static XisfPropertyKey<float> WindGust { get; } = new("Observation:Meteorology:WindGust", "Float32", unit: "kilometers/hour");
            /// <summary>Observation:Meteorology:WindSpeed (Float32; units: kilometers/hour).</summary>
            public static XisfPropertyKey<float> WindSpeed { get; } = new("Observation:Meteorology:WindSpeed", "Float32", unit: "kilometers/hour");
        }

        /// <summary>Predefined properties in the Object group.</summary>
        public static class Object
        {
            /// <summary>Observation:Object:Dec (Float64; units: degrees).</summary>
            public static XisfPropertyKey<double> Dec { get; } = new("Observation:Object:Dec", "Float64", unit: "degrees");
            /// <summary>Observation:Object:Name (String).</summary>
            public static XisfPropertyKey<string> Name { get; } = new("Observation:Object:Name", "String");
            /// <summary>Observation:Object:RA (Float64; units: degrees).</summary>
            public static XisfPropertyKey<double> RA { get; } = new("Observation:Object:RA", "Float64", unit: "degrees");
        }

        /// <summary>Predefined properties in the Time group.</summary>
        public static class Time
        {
            /// <summary>Observation:Time:End (TimePoint).</summary>
            public static XisfPropertyKey<DateTimeOffset> End { get; } = new("Observation:Time:End", "TimePoint");
            /// <summary>Observation:Time:Start (TimePoint).</summary>
            public static XisfPropertyKey<DateTimeOffset> Start { get; } = new("Observation:Time:Start", "TimePoint");
        }
    }
}
