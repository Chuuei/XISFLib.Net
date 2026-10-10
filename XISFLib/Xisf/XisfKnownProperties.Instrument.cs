namespace XISFLib;

public static partial class XisfKnownProperties
{

    /// <summary>Predefined properties in the Instrument group.</summary>
    public static class Instrument
    {
        /// <summary>Instrument:ExposureTime (Float32; units: seconds).</summary>
        public static XisfPropertyKey<float> ExposureTime { get; } = new("Instrument:ExposureTime", "Float32", unit: "seconds");

        /// <summary>Predefined properties in the Camera group.</summary>
        public static class Camera
        {
            /// <summary>Instrument:Camera:Gain (Float32; units: electrons/data number).</summary>
            public static XisfPropertyKey<float> Gain { get; } = new("Instrument:Camera:Gain", "Float32", unit: "electrons/data number");
            /// <summary>Instrument:Camera:ISOSpeed (Int32).</summary>
            public static XisfPropertyKey<int> ISOSpeed { get; } = new("Instrument:Camera:ISOSpeed", "Int32");
            /// <summary>Instrument:Camera:Name (String).</summary>
            public static XisfPropertyKey<string> Name { get; } = new("Instrument:Camera:Name", "String");
            /// <summary>Instrument:Camera:ReadoutNoise (Float32; units: electrons).</summary>
            public static XisfPropertyKey<float> ReadoutNoise { get; } = new("Instrument:Camera:ReadoutNoise", "Float32", unit: "electrons");
            /// <summary>Instrument:Camera:Rotation (Float32; units: degrees).</summary>
            public static XisfPropertyKey<float> Rotation { get; } = new("Instrument:Camera:Rotation", "Float32", unit: "degrees");
            /// <summary>Instrument:Camera:XBinning (Int32).</summary>
            public static XisfPropertyKey<int> XBinning { get; } = new("Instrument:Camera:XBinning", "Int32");
            /// <summary>Instrument:Camera:YBinning (Int32).</summary>
            public static XisfPropertyKey<int> YBinning { get; } = new("Instrument:Camera:YBinning", "Int32");
        }

        /// <summary>Predefined properties in the Filter group.</summary>
        public static class Filter
        {
            /// <summary>Instrument:Filter:Name (String).</summary>
            public static XisfPropertyKey<string> Name { get; } = new("Instrument:Filter:Name", "String");
        }

        /// <summary>Predefined properties in the Focuser group.</summary>
        public static class Focuser
        {
            /// <summary>Instrument:Focuser:Position (Float32; units: millimeters).</summary>
            public static XisfPropertyKey<float> Position { get; } = new("Instrument:Focuser:Position", "Float32", unit: "millimeters");
        }

        /// <summary>Predefined properties in the Sensor group.</summary>
        public static class Sensor
        {
            /// <summary>Instrument:Sensor:TargetTemperature (Float32; units: degrees Celsius).</summary>
            public static XisfPropertyKey<float> TargetTemperature { get; } = new("Instrument:Sensor:TargetTemperature", "Float32", unit: "degrees Celsius");
            /// <summary>Instrument:Sensor:Temperature (Float32; units: degrees Celsius).</summary>
            public static XisfPropertyKey<float> Temperature { get; } = new("Instrument:Sensor:Temperature", "Float32", unit: "degrees Celsius");
            /// <summary>Instrument:Sensor:XPixelSize (Float32; units: microns).</summary>
            public static XisfPropertyKey<float> XPixelSize { get; } = new("Instrument:Sensor:XPixelSize", "Float32", unit: "microns");
            /// <summary>Instrument:Sensor:YPixelSize (Float32; units: microns).</summary>
            public static XisfPropertyKey<float> YPixelSize { get; } = new("Instrument:Sensor:YPixelSize", "Float32", unit: "microns");
        }

        /// <summary>Predefined properties in the Telescope group.</summary>
        public static class Telescope
        {
            /// <summary>Instrument:Telescope:Aperture (Float32; units: meters).</summary>
            public static XisfPropertyKey<float> Aperture { get; } = new("Instrument:Telescope:Aperture", "Float32", unit: "meters");
            /// <summary>Instrument:Telescope:CollectingArea (Float32; units: square meters).</summary>
            public static XisfPropertyKey<float> CollectingArea { get; } = new("Instrument:Telescope:CollectingArea", "Float32", unit: "square meters");
            /// <summary>Instrument:Telescope:FocalLength (Float32; units: meters).</summary>
            public static XisfPropertyKey<float> FocalLength { get; } = new("Instrument:Telescope:FocalLength", "Float32", unit: "meters");
            /// <summary>Instrument:Telescope:Name (String).</summary>
            public static XisfPropertyKey<string> Name { get; } = new("Instrument:Telescope:Name", "String");
        }
    }
}
