namespace XISFLib;

public static partial class XisfKnownProperties
{

    /// <summary>Predefined properties in the AstrometricSolution group.</summary>
    public static class AstrometricSolution
    {
        /// <summary>AstrometricSolution:Catalog (String).</summary>
        public static XisfPropertyKey<string> Catalog { get; } = new("AstrometricSolution:Catalog", "String");
        /// <summary>AstrometricSolution:CelestialPoleNativeCoordinates (F64Vector; units: degrees).</summary>
        public static XisfPropertyKey<double[]> CelestialPoleNativeCoordinates { get; } = new("AstrometricSolution:CelestialPoleNativeCoordinates", "F64Vector", unit: "degrees", length: 2);
        /// <summary>AstrometricSolution:CelestialReferenceSystem (String).</summary>
        public static XisfPropertyKey<string> CelestialReferenceSystem { get; } = new("AstrometricSolution:CelestialReferenceSystem", "String");
        /// <summary>AstrometricSolution:CreationTime (TimePoint).</summary>
        public static XisfPropertyKey<DateTimeOffset> CreationTime { get; } = new("AstrometricSolution:CreationTime", "TimePoint");
        /// <summary>AstrometricSolution:CreatorApplication (String).</summary>
        public static XisfPropertyKey<string> CreatorApplication { get; } = new("AstrometricSolution:CreatorApplication", "String");
        /// <summary>AstrometricSolution:CreatorModule (String).</summary>
        public static XisfPropertyKey<string> CreatorModule { get; } = new("AstrometricSolution:CreatorModule", "String");
        /// <summary>AstrometricSolution:CreatorOS (String).</summary>
        public static XisfPropertyKey<string> CreatorOS { get; } = new("AstrometricSolution:CreatorOS", "String");
        /// <summary>AstrometricSolution:LinearTransformationMatrix (F64Matrix; units: degrees/pixel).</summary>
        public static XisfPropertyKey<double[,]> LinearTransformationMatrix { get; } = new("AstrometricSolution:LinearTransformationMatrix", "F64Matrix", unit: "degrees/pixel", rows: 2, columns: 2);
        /// <summary>AstrometricSolution:ProjectionSystem (String).</summary>
        public static XisfPropertyKey<string> ProjectionSystem { get; } = new("AstrometricSolution:ProjectionSystem", "String");
        /// <summary>AstrometricSolution:ReferenceCelestialCoordinates (F64Vector; units: degrees).</summary>
        public static XisfPropertyKey<double[]> ReferenceCelestialCoordinates { get; } = new("AstrometricSolution:ReferenceCelestialCoordinates", "F64Vector", unit: "degrees", length: 2);
        /// <summary>AstrometricSolution:ReferenceImageCoordinates (F64Vector; units: pixels).</summary>
        public static XisfPropertyKey<double[]> ReferenceImageCoordinates { get; } = new("AstrometricSolution:ReferenceImageCoordinates", "F64Vector", unit: "pixels", length: 2);
        /// <summary>AstrometricSolution:ReferenceNativeCoordinates (F64Vector; units: degrees).</summary>
        public static XisfPropertyKey<double[]> ReferenceNativeCoordinates { get; } = new("AstrometricSolution:ReferenceNativeCoordinates", "F64Vector", unit: "degrees", length: 2);
        /// <summary>AstrometricSolution:Version (String).</summary>
        public static XisfPropertyKey<string> Version { get; } = new("AstrometricSolution:Version", "String");
        /// <summary>AstrometricSolution:Weights (F64Vector).</summary>
        public static XisfPropertyKey<double[]> Weights { get; } = new("AstrometricSolution:Weights", "F64Vector");

        /// <summary>Predefined properties in the ControlPoints group.</summary>
        public static class ControlPoints
        {
            /// <summary>AstrometricSolution:ControlPoints:Celestial (F64Matrix; units: degrees).</summary>
            public static XisfPropertyKey<double[,]> Celestial { get; } = new("AstrometricSolution:ControlPoints:Celestial", "F64Matrix", unit: "degrees", columns: 2);
            /// <summary>AstrometricSolution:ControlPoints:Image (F64Matrix; units: pixels).</summary>
            public static XisfPropertyKey<double[,]> Image { get; } = new("AstrometricSolution:ControlPoints:Image", "F64Matrix", unit: "pixels", columns: 2);
            /// <summary>AstrometricSolution:ControlPoints:Rejected (I32Vector).</summary>
            public static XisfPropertyKey<int[]> Rejected { get; } = new("AstrometricSolution:ControlPoints:Rejected", "I32Vector");
        }

        /// <summary>Predefined properties in the DistortionModel group.</summary>
        public static class DistortionModel
        {

            /// <summary>Predefined properties in the ImageToProjection group.</summary>
            public static class ImageToProjection
            {
                /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:BasisFunction (String).</summary>
                public static XisfPropertyKey<string> BasisFunction { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:BasisFunction", "String");
                /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Order (Int32).</summary>
                public static XisfPropertyKey<int> Order { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Order", "Int32");
                /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Polynomial (Boolean).</summary>
                public static XisfPropertyKey<bool> Polynomial { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Polynomial", "Boolean");
                /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Terms (String).</summary>
                public static XisfPropertyKey<string> Terms { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Terms", "String");

                /// <summary>Predefined properties in the Fallback group.</summary>
                public static class Fallback
                {
                    /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Fallback:Threshold (Float64).</summary>
                    public static XisfPropertyKey<double> Threshold { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Fallback:Threshold", "Float64");

                    /// <summary>Predefined properties in the X group.</summary>
                    public static class X
                    {
                        /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Fallback:X:Coefficients (F64Vector).</summary>
                        public static XisfPropertyKey<double[]> Coefficients { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Fallback:X:Coefficients", "F64Vector");
                        /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Fallback:X:Nodes (F64Matrix).</summary>
                        public static XisfPropertyKey<double[,]> Nodes { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Fallback:X:Nodes", "F64Matrix", columns: 2);
                        /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Fallback:X:Normalization (F64Vector).</summary>
                        public static XisfPropertyKey<double[]> Normalization { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Fallback:X:Normalization", "F64Vector", length: 3);
                        /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Fallback:X:ShapeParameter (Float64).</summary>
                        public static XisfPropertyKey<double> ShapeParameter { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Fallback:X:ShapeParameter", "Float64");
                    }

                    /// <summary>Predefined properties in the Y group.</summary>
                    public static class Y
                    {
                        /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Fallback:Y:Coefficients (F64Vector).</summary>
                        public static XisfPropertyKey<double[]> Coefficients { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Fallback:Y:Coefficients", "F64Vector");
                        /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Fallback:Y:Nodes (F64Matrix).</summary>
                        public static XisfPropertyKey<double[,]> Nodes { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Fallback:Y:Nodes", "F64Matrix", columns: 2);
                        /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Fallback:Y:Normalization (F64Vector).</summary>
                        public static XisfPropertyKey<double[]> Normalization { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Fallback:Y:Normalization", "F64Vector", length: 3);
                        /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Fallback:Y:ShapeParameter (Float64).</summary>
                        public static XisfPropertyKey<double> ShapeParameter { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Fallback:Y:ShapeParameter", "Float64");
                    }
                }

                /// <summary>Predefined properties in the Global group.</summary>
                public static class Global
                {

                    /// <summary>Predefined properties in the X group.</summary>
                    public static class X
                    {
                        /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Global:X:Coefficients (F64Vector).</summary>
                        public static XisfPropertyKey<double[]> Coefficients { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Global:X:Coefficients", "F64Vector");
                        /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Global:X:Nodes (F64Matrix).</summary>
                        public static XisfPropertyKey<double[,]> Nodes { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Global:X:Nodes", "F64Matrix", columns: 2);
                        /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Global:X:Normalization (F64Vector).</summary>
                        public static XisfPropertyKey<double[]> Normalization { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Global:X:Normalization", "F64Vector", length: 3);
                        /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Global:X:ShapeParameter (Float64).</summary>
                        public static XisfPropertyKey<double> ShapeParameter { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Global:X:ShapeParameter", "Float64");
                    }

                    /// <summary>Predefined properties in the Y group.</summary>
                    public static class Y
                    {
                        /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Global:Y:Coefficients (F64Vector).</summary>
                        public static XisfPropertyKey<double[]> Coefficients { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Global:Y:Coefficients", "F64Vector");
                        /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Global:Y:Nodes (F64Matrix).</summary>
                        public static XisfPropertyKey<double[,]> Nodes { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Global:Y:Nodes", "F64Matrix", columns: 2);
                        /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Global:Y:Normalization (F64Vector).</summary>
                        public static XisfPropertyKey<double[]> Normalization { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Global:Y:Normalization", "F64Vector", length: 3);
                        /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Global:Y:ShapeParameter (Float64).</summary>
                        public static XisfPropertyKey<double> ShapeParameter { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Global:Y:ShapeParameter", "Float64");
                    }
                }

                /// <summary>Predefined properties in the Local group.</summary>
                public static class Local
                {
                    /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Local:Center (F64Matrix).</summary>
                    public static XisfPropertyKey<double[,]> Center { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Local:Center", "F64Matrix", columns: 2);
                    /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Local:Radius (F64Vector).</summary>
                    public static XisfPropertyKey<double[]> Radius { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Local:Radius", "F64Vector");

                    /// <summary>Predefined properties in the X group.</summary>
                    public static class X
                    {
                        /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Local:X:Coefficients (F64Vector).</summary>
                        public static XisfPropertyKey<double[]> Coefficients { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Local:X:Coefficients", "F64Vector");
                        /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Local:X:NodeOffsets (I32Vector).</summary>
                        public static XisfPropertyKey<int[]> NodeOffsets { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Local:X:NodeOffsets", "I32Vector");
                        /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Local:X:Nodes (F64Matrix).</summary>
                        public static XisfPropertyKey<double[,]> Nodes { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Local:X:Nodes", "F64Matrix", columns: 2);
                        /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Local:X:Normalization (F64Matrix).</summary>
                        public static XisfPropertyKey<double[,]> Normalization { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Local:X:Normalization", "F64Matrix", columns: 3);
                        /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Local:X:ShapeParameter (F64Vector).</summary>
                        public static XisfPropertyKey<double[]> ShapeParameter { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Local:X:ShapeParameter", "F64Vector");
                    }

                    /// <summary>Predefined properties in the Y group.</summary>
                    public static class Y
                    {
                        /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Local:Y:Coefficients (F64Vector).</summary>
                        public static XisfPropertyKey<double[]> Coefficients { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Local:Y:Coefficients", "F64Vector");
                        /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Local:Y:NodeOffsets (I32Vector).</summary>
                        public static XisfPropertyKey<int[]> NodeOffsets { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Local:Y:NodeOffsets", "I32Vector");
                        /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Local:Y:Nodes (F64Matrix).</summary>
                        public static XisfPropertyKey<double[,]> Nodes { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Local:Y:Nodes", "F64Matrix", columns: 2);
                        /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Local:Y:Normalization (F64Matrix).</summary>
                        public static XisfPropertyKey<double[,]> Normalization { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Local:Y:Normalization", "F64Matrix", columns: 3);
                        /// <summary>AstrometricSolution:DistortionModel:ImageToProjection:Local:Y:ShapeParameter (F64Vector).</summary>
                        public static XisfPropertyKey<double[]> ShapeParameter { get; } = new("AstrometricSolution:DistortionModel:ImageToProjection:Local:Y:ShapeParameter", "F64Vector");
                    }
                }
            }

            /// <summary>Predefined properties in the ProjectionToImage group.</summary>
            public static class ProjectionToImage
            {
                /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:BasisFunction (String).</summary>
                public static XisfPropertyKey<string> BasisFunction { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:BasisFunction", "String");
                /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Order (Int32).</summary>
                public static XisfPropertyKey<int> Order { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Order", "Int32");
                /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Polynomial (Boolean).</summary>
                public static XisfPropertyKey<bool> Polynomial { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Polynomial", "Boolean");
                /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Terms (String).</summary>
                public static XisfPropertyKey<string> Terms { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Terms", "String");

                /// <summary>Predefined properties in the Fallback group.</summary>
                public static class Fallback
                {
                    /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Fallback:Threshold (Float64).</summary>
                    public static XisfPropertyKey<double> Threshold { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Fallback:Threshold", "Float64");

                    /// <summary>Predefined properties in the X group.</summary>
                    public static class X
                    {
                        /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Fallback:X:Coefficients (F64Vector).</summary>
                        public static XisfPropertyKey<double[]> Coefficients { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Fallback:X:Coefficients", "F64Vector");
                        /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Fallback:X:Nodes (F64Matrix).</summary>
                        public static XisfPropertyKey<double[,]> Nodes { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Fallback:X:Nodes", "F64Matrix", columns: 2);
                        /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Fallback:X:Normalization (F64Vector).</summary>
                        public static XisfPropertyKey<double[]> Normalization { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Fallback:X:Normalization", "F64Vector", length: 3);
                        /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Fallback:X:ShapeParameter (Float64).</summary>
                        public static XisfPropertyKey<double> ShapeParameter { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Fallback:X:ShapeParameter", "Float64");
                    }

                    /// <summary>Predefined properties in the Y group.</summary>
                    public static class Y
                    {
                        /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Fallback:Y:Coefficients (F64Vector).</summary>
                        public static XisfPropertyKey<double[]> Coefficients { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Fallback:Y:Coefficients", "F64Vector");
                        /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Fallback:Y:Nodes (F64Matrix).</summary>
                        public static XisfPropertyKey<double[,]> Nodes { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Fallback:Y:Nodes", "F64Matrix", columns: 2);
                        /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Fallback:Y:Normalization (F64Vector).</summary>
                        public static XisfPropertyKey<double[]> Normalization { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Fallback:Y:Normalization", "F64Vector", length: 3);
                        /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Fallback:Y:ShapeParameter (Float64).</summary>
                        public static XisfPropertyKey<double> ShapeParameter { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Fallback:Y:ShapeParameter", "Float64");
                    }
                }

                /// <summary>Predefined properties in the Global group.</summary>
                public static class Global
                {

                    /// <summary>Predefined properties in the X group.</summary>
                    public static class X
                    {
                        /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Global:X:Coefficients (F64Vector).</summary>
                        public static XisfPropertyKey<double[]> Coefficients { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Global:X:Coefficients", "F64Vector");
                        /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Global:X:Nodes (F64Matrix).</summary>
                        public static XisfPropertyKey<double[,]> Nodes { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Global:X:Nodes", "F64Matrix", columns: 2);
                        /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Global:X:Normalization (F64Vector).</summary>
                        public static XisfPropertyKey<double[]> Normalization { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Global:X:Normalization", "F64Vector", length: 3);
                        /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Global:X:ShapeParameter (Float64).</summary>
                        public static XisfPropertyKey<double> ShapeParameter { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Global:X:ShapeParameter", "Float64");
                    }

                    /// <summary>Predefined properties in the Y group.</summary>
                    public static class Y
                    {
                        /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Global:Y:Coefficients (F64Vector).</summary>
                        public static XisfPropertyKey<double[]> Coefficients { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Global:Y:Coefficients", "F64Vector");
                        /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Global:Y:Nodes (F64Matrix).</summary>
                        public static XisfPropertyKey<double[,]> Nodes { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Global:Y:Nodes", "F64Matrix", columns: 2);
                        /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Global:Y:Normalization (F64Vector).</summary>
                        public static XisfPropertyKey<double[]> Normalization { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Global:Y:Normalization", "F64Vector", length: 3);
                        /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Global:Y:ShapeParameter (Float64).</summary>
                        public static XisfPropertyKey<double> ShapeParameter { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Global:Y:ShapeParameter", "Float64");
                    }
                }

                /// <summary>Predefined properties in the Local group.</summary>
                public static class Local
                {
                    /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Local:Center (F64Matrix).</summary>
                    public static XisfPropertyKey<double[,]> Center { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Local:Center", "F64Matrix", columns: 2);
                    /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Local:Radius (F64Vector).</summary>
                    public static XisfPropertyKey<double[]> Radius { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Local:Radius", "F64Vector");

                    /// <summary>Predefined properties in the X group.</summary>
                    public static class X
                    {
                        /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Local:X:Coefficients (F64Vector).</summary>
                        public static XisfPropertyKey<double[]> Coefficients { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Local:X:Coefficients", "F64Vector");
                        /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Local:X:NodeOffsets (I32Vector).</summary>
                        public static XisfPropertyKey<int[]> NodeOffsets { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Local:X:NodeOffsets", "I32Vector");
                        /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Local:X:Nodes (F64Matrix).</summary>
                        public static XisfPropertyKey<double[,]> Nodes { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Local:X:Nodes", "F64Matrix", columns: 2);
                        /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Local:X:Normalization (F64Matrix).</summary>
                        public static XisfPropertyKey<double[,]> Normalization { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Local:X:Normalization", "F64Matrix", columns: 3);
                        /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Local:X:ShapeParameter (F64Vector).</summary>
                        public static XisfPropertyKey<double[]> ShapeParameter { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Local:X:ShapeParameter", "F64Vector");
                    }

                    /// <summary>Predefined properties in the Y group.</summary>
                    public static class Y
                    {
                        /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Local:Y:Coefficients (F64Vector).</summary>
                        public static XisfPropertyKey<double[]> Coefficients { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Local:Y:Coefficients", "F64Vector");
                        /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Local:Y:NodeOffsets (I32Vector).</summary>
                        public static XisfPropertyKey<int[]> NodeOffsets { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Local:Y:NodeOffsets", "I32Vector");
                        /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Local:Y:Nodes (F64Matrix).</summary>
                        public static XisfPropertyKey<double[,]> Nodes { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Local:Y:Nodes", "F64Matrix", columns: 2);
                        /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Local:Y:Normalization (F64Matrix).</summary>
                        public static XisfPropertyKey<double[,]> Normalization { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Local:Y:Normalization", "F64Matrix", columns: 3);
                        /// <summary>AstrometricSolution:DistortionModel:ProjectionToImage:Local:Y:ShapeParameter (F64Vector).</summary>
                        public static XisfPropertyKey<double[]> ShapeParameter { get; } = new("AstrometricSolution:DistortionModel:ProjectionToImage:Local:Y:ShapeParameter", "F64Vector");
                    }
                }
            }
        }

        /// <summary>Predefined properties in the ProjectiveTransformation group.</summary>
        public static class ProjectiveTransformation
        {
            /// <summary>AstrometricSolution:ProjectiveTransformation:ImageToProjection (F64Matrix).</summary>
            public static XisfPropertyKey<double[,]> ImageToProjection { get; } = new("AstrometricSolution:ProjectiveTransformation:ImageToProjection", "F64Matrix", rows: 3, columns: 3);
            /// <summary>AstrometricSolution:ProjectiveTransformation:ProjectionToImage (F64Matrix).</summary>
            public static XisfPropertyKey<double[,]> ProjectionToImage { get; } = new("AstrometricSolution:ProjectiveTransformation:ProjectionToImage", "F64Matrix", rows: 3, columns: 3);
        }
    }
}
