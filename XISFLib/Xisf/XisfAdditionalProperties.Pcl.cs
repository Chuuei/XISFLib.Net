namespace XISFLib;

public static partial class XisfAdditionalProperties
{

    /// <summary>Typed metadata in the Pcl group.</summary>
    public static class Pcl
    {
        /// <summary>PCL:TotalExposureTime (F64Vector); application metadata or a library storage hint, not a reserved XISF property.</summary>
        public static XisfPropertyKey<double[]> TotalExposureTime { get; } = new("PCL:TotalExposureTime", "F64Vector", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");

        /// <summary>Typed metadata in the AstrometricSolution group.</summary>
        public static class AstrometricSolution
        {
            /// <summary>PCL:AstrometricSolution:Catalog (String); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<string> Catalog { get; } = new("PCL:AstrometricSolution:Catalog", "String", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
            /// <summary>PCL:AstrometricSolution:CelestialPoleNativeCoordinates (F64Vector); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<double[]> CelestialPoleNativeCoordinates { get; } = new("PCL:AstrometricSolution:CelestialPoleNativeCoordinates", "F64Vector", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
            /// <summary>PCL:AstrometricSolution:CreationTime (TimePoint); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<DateTimeOffset> CreationTime { get; } = new("PCL:AstrometricSolution:CreationTime", "TimePoint", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
            /// <summary>PCL:AstrometricSolution:CreatorApplication (String); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<string> CreatorApplication { get; } = new("PCL:AstrometricSolution:CreatorApplication", "String", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
            /// <summary>PCL:AstrometricSolution:CreatorModule (String); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<string> CreatorModule { get; } = new("PCL:AstrometricSolution:CreatorModule", "String", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
            /// <summary>PCL:AstrometricSolution:CreatorOS (String); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<string> CreatorOS { get; } = new("PCL:AstrometricSolution:CreatorOS", "String", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
            /// <summary>PCL:AstrometricSolution:LinearTransformationMatrix (F64Matrix); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<double[,]> LinearTransformationMatrix { get; } = new("PCL:AstrometricSolution:LinearTransformationMatrix", "F64Matrix", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
            /// <summary>PCL:AstrometricSolution:ProjectionSystem (String); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<string> ProjectionSystem { get; } = new("PCL:AstrometricSolution:ProjectionSystem", "String", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
            /// <summary>PCL:AstrometricSolution:ReferenceCelestialCoordinates (F64Vector); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<double[]> ReferenceCelestialCoordinates { get; } = new("PCL:AstrometricSolution:ReferenceCelestialCoordinates", "F64Vector", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
            /// <summary>PCL:AstrometricSolution:ReferenceImageCoordinates (F64Vector); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<double[]> ReferenceImageCoordinates { get; } = new("PCL:AstrometricSolution:ReferenceImageCoordinates", "F64Vector", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
            /// <summary>PCL:AstrometricSolution:ReferenceNativeCoordinates (F64Vector); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<double[]> ReferenceNativeCoordinates { get; } = new("PCL:AstrometricSolution:ReferenceNativeCoordinates", "F64Vector", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");

            /// <summary>Typed metadata in the SplineWorldTransformation group.</summary>
            public static class SplineWorldTransformation
            {
                /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:LinearApproximation (F64Matrix); application metadata or a library storage hint, not a reserved XISF property.</summary>
                public static XisfPropertyKey<double[,]> LinearApproximation { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:LinearApproximation", "F64Matrix", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:MaxSplinePoints (Int32); application metadata or a library storage hint, not a reserved XISF property.</summary>
                public static XisfPropertyKey<int> MaxSplinePoints { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:MaxSplinePoints", "Int32", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:RBFType (String); application metadata or a library storage hint, not a reserved XISF property.</summary>
                public static XisfPropertyKey<string> RBFType { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:RBFType", "String", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:SimplifierRejectFraction (Float32); application metadata or a library storage hint, not a reserved XISF property.</summary>
                public static XisfPropertyKey<float> SimplifierRejectFraction { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:SimplifierRejectFraction", "Float32", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:SplineOrder (Int32); application metadata or a library storage hint, not a reserved XISF property.</summary>
                public static XisfPropertyKey<int> SplineOrder { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:SplineOrder", "Int32", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:SplineSmoothness (Float32); application metadata or a library storage hint, not a reserved XISF property.</summary>
                public static XisfPropertyKey<float> SplineSmoothness { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:SplineSmoothness", "Float32", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:Truncated (Boolean); application metadata or a library storage hint, not a reserved XISF property.</summary>
                public static XisfPropertyKey<bool> Truncated { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:Truncated", "Boolean", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:UseSimplifiers (Boolean); application metadata or a library storage hint, not a reserved XISF property.</summary>
                public static XisfPropertyKey<bool> UseSimplifiers { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:UseSimplifiers", "Boolean", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:Version (String); application metadata or a library storage hint, not a reserved XISF property.</summary>
                public static XisfPropertyKey<string> Version { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:Version", "String", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");

                /// <summary>Typed metadata in the ControlPoints group.</summary>
                public static class ControlPoints
                {
                    /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:ControlPoints:Image (F64Vector); application metadata or a library storage hint, not a reserved XISF property.</summary>
                    public static XisfPropertyKey<double[]> Image { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:ControlPoints:Image", "F64Vector", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                    /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:ControlPoints:World (F64Vector); application metadata or a library storage hint, not a reserved XISF property.</summary>
                    public static XisfPropertyKey<double[]> World { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:ControlPoints:World", "F64Vector", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                }

                /// <summary>Typed metadata in the PointGridInterpolation group.</summary>
                public static class PointGridInterpolation
                {

                    /// <summary>Typed metadata in the ImageToNative group.</summary>
                    public static class ImageToNative
                    {
                        /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointGridInterpolation:ImageToNative:Delta (Float64); application metadata or a library storage hint, not a reserved XISF property.</summary>
                        public static XisfPropertyKey<double> Delta { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointGridInterpolation:ImageToNative:Delta", "Float64", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                        /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointGridInterpolation:ImageToNative:GridX (F64Matrix); application metadata or a library storage hint, not a reserved XISF property.</summary>
                        public static XisfPropertyKey<double[,]> GridX { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointGridInterpolation:ImageToNative:GridX", "F64Matrix", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                        /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointGridInterpolation:ImageToNative:GridY (F64Matrix); application metadata or a library storage hint, not a reserved XISF property.</summary>
                        public static XisfPropertyKey<double[,]> GridY { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointGridInterpolation:ImageToNative:GridY", "F64Matrix", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                        /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointGridInterpolation:ImageToNative:Rect (F64Vector); application metadata or a library storage hint, not a reserved XISF property.</summary>
                        public static XisfPropertyKey<double[]> Rect { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointGridInterpolation:ImageToNative:Rect", "F64Vector", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                    }

                    /// <summary>Typed metadata in the NativeToImage group.</summary>
                    public static class NativeToImage
                    {
                        /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointGridInterpolation:NativeToImage:Delta (Float64); application metadata or a library storage hint, not a reserved XISF property.</summary>
                        public static XisfPropertyKey<double> Delta { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointGridInterpolation:NativeToImage:Delta", "Float64", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                        /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointGridInterpolation:NativeToImage:GridX (F64Matrix); application metadata or a library storage hint, not a reserved XISF property.</summary>
                        public static XisfPropertyKey<double[,]> GridX { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointGridInterpolation:NativeToImage:GridX", "F64Matrix", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                        /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointGridInterpolation:NativeToImage:GridY (F64Matrix); application metadata or a library storage hint, not a reserved XISF property.</summary>
                        public static XisfPropertyKey<double[,]> GridY { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointGridInterpolation:NativeToImage:GridY", "F64Matrix", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                        /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointGridInterpolation:NativeToImage:Rect (F64Vector); application metadata or a library storage hint, not a reserved XISF property.</summary>
                        public static XisfPropertyKey<double[]> Rect { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointGridInterpolation:NativeToImage:Rect", "F64Vector", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                    }
                }

                /// <summary>Typed metadata in the PointSurfaceSpline group.</summary>
                public static class PointSurfaceSpline
                {

                    /// <summary>Typed metadata in the ImageToNative group.</summary>
                    public static class ImageToNative
                    {
                        /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:LinearFunction (F64Matrix); application metadata or a library storage hint, not a reserved XISF property.</summary>
                        public static XisfPropertyKey<double[,]> LinearFunction { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:LinearFunction", "F64Matrix", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                        /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SimplificationErrorX (Float64); application metadata or a library storage hint, not a reserved XISF property.</summary>
                        public static XisfPropertyKey<double> SimplificationErrorX { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SimplificationErrorX", "Float64", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                        /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SimplificationErrorY (Float64); application metadata or a library storage hint, not a reserved XISF property.</summary>
                        public static XisfPropertyKey<double> SimplificationErrorY { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SimplificationErrorY", "Float64", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                        /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:TruncatedX (Boolean); application metadata or a library storage hint, not a reserved XISF property.</summary>
                        public static XisfPropertyKey<bool> TruncatedX { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:TruncatedX", "Boolean", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                        /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:TruncatedY (Boolean); application metadata or a library storage hint, not a reserved XISF property.</summary>
                        public static XisfPropertyKey<bool> TruncatedY { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:TruncatedY", "Boolean", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");

                        /// <summary>Typed metadata in the SurfaceSplineX group.</summary>
                        public static class SurfaceSplineX
                        {
                            /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SurfaceSplineX:NodeXCoordinates (F64Vector); application metadata or a library storage hint, not a reserved XISF property.</summary>
                            public static XisfPropertyKey<double[]> NodeXCoordinates { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SurfaceSplineX:NodeXCoordinates", "F64Vector", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                            /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SurfaceSplineX:NodeYCoordinates (F64Vector); application metadata or a library storage hint, not a reserved XISF property.</summary>
                            public static XisfPropertyKey<double[]> NodeYCoordinates { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SurfaceSplineX:NodeYCoordinates", "F64Vector", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                            /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SurfaceSplineX:ScalingFactor (Float64); application metadata or a library storage hint, not a reserved XISF property.</summary>
                            public static XisfPropertyKey<double> ScalingFactor { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SurfaceSplineX:ScalingFactor", "Float64", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                            /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SurfaceSplineX:Serialization (String); application metadata or a library storage hint, not a reserved XISF property.</summary>
                            public static XisfPropertyKey<string> Serialization { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SurfaceSplineX:Serialization", "String", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                            /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SurfaceSplineX:ZeroOffsetX (Float64); application metadata or a library storage hint, not a reserved XISF property.</summary>
                            public static XisfPropertyKey<double> ZeroOffsetX { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SurfaceSplineX:ZeroOffsetX", "Float64", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                            /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SurfaceSplineX:ZeroOffsetY (Float64); application metadata or a library storage hint, not a reserved XISF property.</summary>
                            public static XisfPropertyKey<double> ZeroOffsetY { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SurfaceSplineX:ZeroOffsetY", "Float64", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                        }

                        /// <summary>Typed metadata in the SurfaceSplineY group.</summary>
                        public static class SurfaceSplineY
                        {
                            /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SurfaceSplineY:NodeXCoordinates (F64Vector); application metadata or a library storage hint, not a reserved XISF property.</summary>
                            public static XisfPropertyKey<double[]> NodeXCoordinates { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SurfaceSplineY:NodeXCoordinates", "F64Vector", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                            /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SurfaceSplineY:NodeYCoordinates (F64Vector); application metadata or a library storage hint, not a reserved XISF property.</summary>
                            public static XisfPropertyKey<double[]> NodeYCoordinates { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SurfaceSplineY:NodeYCoordinates", "F64Vector", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                            /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SurfaceSplineY:ScalingFactor (Float64); application metadata or a library storage hint, not a reserved XISF property.</summary>
                            public static XisfPropertyKey<double> ScalingFactor { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SurfaceSplineY:ScalingFactor", "Float64", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                            /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SurfaceSplineY:Serialization (String); application metadata or a library storage hint, not a reserved XISF property.</summary>
                            public static XisfPropertyKey<string> Serialization { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SurfaceSplineY:Serialization", "String", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                            /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SurfaceSplineY:ZeroOffsetX (Float64); application metadata or a library storage hint, not a reserved XISF property.</summary>
                            public static XisfPropertyKey<double> ZeroOffsetX { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SurfaceSplineY:ZeroOffsetX", "Float64", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                            /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SurfaceSplineY:ZeroOffsetY (Float64); application metadata or a library storage hint, not a reserved XISF property.</summary>
                            public static XisfPropertyKey<double> ZeroOffsetY { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:ImageToNative:SurfaceSplineY:ZeroOffsetY", "Float64", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                        }
                    }

                    /// <summary>Typed metadata in the NativeToImage group.</summary>
                    public static class NativeToImage
                    {
                        /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:LinearFunction (F64Matrix); application metadata or a library storage hint, not a reserved XISF property.</summary>
                        public static XisfPropertyKey<double[,]> LinearFunction { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:LinearFunction", "F64Matrix", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                        /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SimplificationErrorX (Float64); application metadata or a library storage hint, not a reserved XISF property.</summary>
                        public static XisfPropertyKey<double> SimplificationErrorX { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SimplificationErrorX", "Float64", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                        /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SimplificationErrorY (Float64); application metadata or a library storage hint, not a reserved XISF property.</summary>
                        public static XisfPropertyKey<double> SimplificationErrorY { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SimplificationErrorY", "Float64", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                        /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:TruncatedX (Boolean); application metadata or a library storage hint, not a reserved XISF property.</summary>
                        public static XisfPropertyKey<bool> TruncatedX { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:TruncatedX", "Boolean", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                        /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:TruncatedY (Boolean); application metadata or a library storage hint, not a reserved XISF property.</summary>
                        public static XisfPropertyKey<bool> TruncatedY { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:TruncatedY", "Boolean", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");

                        /// <summary>Typed metadata in the SurfaceSplineX group.</summary>
                        public static class SurfaceSplineX
                        {
                            /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SurfaceSplineX:NodeXCoordinates (F64Vector); application metadata or a library storage hint, not a reserved XISF property.</summary>
                            public static XisfPropertyKey<double[]> NodeXCoordinates { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SurfaceSplineX:NodeXCoordinates", "F64Vector", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                            /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SurfaceSplineX:NodeYCoordinates (F64Vector); application metadata or a library storage hint, not a reserved XISF property.</summary>
                            public static XisfPropertyKey<double[]> NodeYCoordinates { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SurfaceSplineX:NodeYCoordinates", "F64Vector", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                            /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SurfaceSplineX:ScalingFactor (Float64); application metadata or a library storage hint, not a reserved XISF property.</summary>
                            public static XisfPropertyKey<double> ScalingFactor { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SurfaceSplineX:ScalingFactor", "Float64", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                            /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SurfaceSplineX:Serialization (String); application metadata or a library storage hint, not a reserved XISF property.</summary>
                            public static XisfPropertyKey<string> Serialization { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SurfaceSplineX:Serialization", "String", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                            /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SurfaceSplineX:ZeroOffsetX (Float64); application metadata or a library storage hint, not a reserved XISF property.</summary>
                            public static XisfPropertyKey<double> ZeroOffsetX { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SurfaceSplineX:ZeroOffsetX", "Float64", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                            /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SurfaceSplineX:ZeroOffsetY (Float64); application metadata or a library storage hint, not a reserved XISF property.</summary>
                            public static XisfPropertyKey<double> ZeroOffsetY { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SurfaceSplineX:ZeroOffsetY", "Float64", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                        }

                        /// <summary>Typed metadata in the SurfaceSplineY group.</summary>
                        public static class SurfaceSplineY
                        {
                            /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SurfaceSplineY:NodeXCoordinates (F64Vector); application metadata or a library storage hint, not a reserved XISF property.</summary>
                            public static XisfPropertyKey<double[]> NodeXCoordinates { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SurfaceSplineY:NodeXCoordinates", "F64Vector", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                            /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SurfaceSplineY:NodeYCoordinates (F64Vector); application metadata or a library storage hint, not a reserved XISF property.</summary>
                            public static XisfPropertyKey<double[]> NodeYCoordinates { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SurfaceSplineY:NodeYCoordinates", "F64Vector", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                            /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SurfaceSplineY:ScalingFactor (Float64); application metadata or a library storage hint, not a reserved XISF property.</summary>
                            public static XisfPropertyKey<double> ScalingFactor { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SurfaceSplineY:ScalingFactor", "Float64", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                            /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SurfaceSplineY:Serialization (String); application metadata or a library storage hint, not a reserved XISF property.</summary>
                            public static XisfPropertyKey<string> Serialization { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SurfaceSplineY:Serialization", "String", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                            /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SurfaceSplineY:ZeroOffsetX (Float64); application metadata or a library storage hint, not a reserved XISF property.</summary>
                            public static XisfPropertyKey<double> ZeroOffsetX { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SurfaceSplineY:ZeroOffsetX", "Float64", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                            /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SurfaceSplineY:ZeroOffsetY (Float64); application metadata or a library storage hint, not a reserved XISF property.</summary>
                            public static XisfPropertyKey<double> ZeroOffsetY { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:PointSurfaceSpline:NativeToImage:SurfaceSplineY:ZeroOffsetY", "Float64", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                        }
                    }
                }

                /// <summary>Typed metadata in the Projective group.</summary>
                public static class Projective
                {
                    /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:Projective:ImageToNative (F64Matrix); application metadata or a library storage hint, not a reserved XISF property.</summary>
                    public static XisfPropertyKey<double[,]> ImageToNative { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:Projective:ImageToNative", "F64Matrix", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                    /// <summary>PCL:AstrometricSolution:SplineWorldTransformation:Projective:NativeToImage (F64Matrix); application metadata or a library storage hint, not a reserved XISF property.</summary>
                    public static XisfPropertyKey<double[,]> NativeToImage { get; } = new("PCL:AstrometricSolution:SplineWorldTransformation:Projective:NativeToImage", "F64Matrix", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
                }
            }
        }

        /// <summary>Typed metadata in the Noise group.</summary>
        public static class Noise
        {
            /// <summary>PCL:Noise:Algorithms (String); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<string> Algorithms { get; } = new("PCL:Noise:Algorithms", "String", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
            /// <summary>PCL:Noise:MStar (F64Vector); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<double[]> MStar { get; } = new("PCL:Noise:MStar", "F64Vector", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
            /// <summary>PCL:Noise:NStar (F64Vector); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<double[]> NStar { get; } = new("PCL:Noise:NStar", "F64Vector", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
            /// <summary>PCL:Noise:ScaleHigh (F64Vector); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<double[]> ScaleHigh { get; } = new("PCL:Noise:ScaleHigh", "F64Vector", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
            /// <summary>PCL:Noise:ScaleLow (F64Vector); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<double[]> ScaleLow { get; } = new("PCL:Noise:ScaleLow", "F64Vector", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
            /// <summary>PCL:Noise:Sigmas (F64Vector); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<double[]> Sigmas { get; } = new("PCL:Noise:Sigmas", "F64Vector", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        }

        /// <summary>Typed metadata in the Signal group.</summary>
        public static class Signal
        {
            /// <summary>PCL:Signal:PSFCounts (UI32Vector); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<uint[]> PSFCounts { get; } = new("PCL:Signal:PSFCounts", "UI32Vector", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
            /// <summary>PCL:Signal:PSFTotalFlux (F64Vector); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<double[]> PSFTotalFlux { get; } = new("PCL:Signal:PSFTotalFlux", "F64Vector", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
            /// <summary>PCL:Signal:PSFTotalMeanFlux (F64Vector); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<double[]> PSFTotalMeanFlux { get; } = new("PCL:Signal:PSFTotalMeanFlux", "F64Vector", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
            /// <summary>PCL:Signal:PSFTotalMeanPowerFlux (F64Vector); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<double[]> PSFTotalMeanPowerFlux { get; } = new("PCL:Signal:PSFTotalMeanPowerFlux", "F64Vector", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
            /// <summary>PCL:Signal:PSFTotalPowerFlux (F64Vector); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<double[]> PSFTotalPowerFlux { get; } = new("PCL:Signal:PSFTotalPowerFlux", "F64Vector", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
            /// <summary>PCL:Signal:PSFType (String); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<string> PSFType { get; } = new("PCL:Signal:PSFType", "String", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        }

        /// <summary>Typed metadata in the Signature group.</summary>
        public static class Signature
        {
            /// <summary>PCL:Signature:Integration (String); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<string> Integration { get; } = new("PCL:Signature:Integration", "String", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
            /// <summary>PCL:Signature:NoiseEvaluation (String); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<string> NoiseEvaluation { get; } = new("PCL:Signature:NoiseEvaluation", "String", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
            /// <summary>PCL:Signature:Preprocessing (String); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<string> Preprocessing { get; } = new("PCL:Signature:Preprocessing", "String", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
            /// <summary>PCL:Signature:Selection (String); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<string> Selection { get; } = new("PCL:Signature:Selection", "String", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
            /// <summary>PCL:Signature:SignalEvaluation (String); application metadata or a library storage hint, not a reserved XISF property.</summary>
            public static XisfPropertyKey<string> SignalEvaluation { get; } = new("PCL:Signature:SignalEvaluation", "String", MetadataDefinitionSource.ObservedExamples, "Examples/metadata-inventory.json");
        }
    }
}
