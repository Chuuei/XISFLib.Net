using System.Reflection;
using System.Xml.Linq;

namespace XISFLib.Tests;

public class KnownPropertyTests
{
    [Fact]
    public void TypedObservationAndInstrumentPropertiesRoundTripWithSpecifiedTypes()
    {
        var image = XisfImageData.FromSamples(new byte[] { 1 }, [1], 1);
        var start = new DateTimeOffset(2026, 10, 10, 20, 30, 0, TimeSpan.FromHours(2));
        image.Properties.Set(XisfKnownProperties.Instrument.ExposureTime, 30.5f, comment: "seconds");
        image.Properties.Set(XisfKnownProperties.Instrument.Camera.XBinning, 2);
        image.Properties.Set(XisfKnownProperties.Instrument.Sensor.Temperature, -10.5f);
        image.Properties.Set(XisfKnownProperties.Observation.Location.Latitude, 52.1);
        image.Properties.Set(XisfKnownProperties.Observation.Object.Name, "M31");
        image.Properties.Set(XisfKnownProperties.Observation.Time.Start, start);
        var content = new XisfDocumentData();
        content.Images.Add(image);
        content.Properties.Set(XisfKnownProperties.Processing.Description, "Calibration");
        content.Metadata.Set(XisfKnownProperties.Xisf.Title, "Session");
        content.Metadata.Set(XisfKnownProperties.Xisf.MaxInlineBlockSize, (ushort)256);
        var loaded = RoundTrip(content);
        var properties = loaded.Images[0].Properties;
        Assert.Equal(30.5f, properties.GetRequiredValue(XisfKnownProperties.Instrument.ExposureTime));
        Assert.Equal("Float32", properties["Instrument:ExposureTime"].Type);
        Assert.Equal("seconds", properties["Instrument:ExposureTime"].Comment);
        Assert.False(properties.ContainsKey("Observation:ExposureTime"));
        Assert.Equal(2, properties.GetRequiredValue(XisfKnownProperties.Instrument.Camera.XBinning));
        Assert.Equal("Int32", properties["Instrument:Camera:XBinning"].Type);
        Assert.Equal(start.ToUniversalTime(), properties.GetRequiredValue(XisfKnownProperties.Observation.Time.Start));
        Assert.Equal("M31", properties.GetRequiredValue(XisfKnownProperties.Observation.Object.Name));
        Assert.Equal(52.1, properties.GetRequiredValue(XisfKnownProperties.Observation.Location.Latitude));
        Assert.Equal("Calibration", loaded.Properties.GetRequiredValue(XisfKnownProperties.Processing.Description));
        Assert.Equal("Session", loaded.Metadata.GetRequiredValue(XisfKnownProperties.Xisf.Title));
        Assert.Equal((ushort)256, loaded.Metadata.GetRequiredValue(XisfKnownProperties.Xisf.MaxInlineBlockSize));
        Assert.Equal("UInt16", loaded.Metadata["XISF:MaxInlineBlockSize"].Type);
        var astronomy = AstronomyMetadata.From(loaded.Images[0]);
        Assert.Equal(30.5, astronomy.ExposureSeconds);
        Assert.Equal(2, astronomy.BinningX);
        Assert.Equal(-10.5, astronomy.SensorTemperatureCelsius);
        var fits = BatchConversion.CreateDocument(loaded);
        Assert.Equal(30.5, fits.Hdus[0].Header.GetRequiredValue<double>("EXPTIME"));
        Assert.Equal(2, fits.Hdus[0].Header.GetRequiredValue<int>("XBINNING"));
        Assert.Equal(-10.5, fits.Hdus[0].Header.GetRequiredValue<double>("CCD-TEMP"));
    }

    [Fact]
    public void MissingZeroUnavailableAndIncompatibleValuesStayDistinct()
    {
        var properties = new XisfPropertyCollection();
        var key = XisfKnownProperties.Instrument.ExposureTime;
        Assert.False(properties.TryGetValue(key, out float _));
        Assert.Throws<KeyNotFoundException>(() => properties.GetRequiredValue(key));
        properties.Set(key, 0f);
        Assert.True(properties.TryGetValue(key, out float zero));
        Assert.Equal(0f, zero);
        Assert.True(properties.ContainsKey(key));
        Assert.True(properties.Remove(key));
        Assert.False(properties.ContainsKey(key));
        properties[key.Id] = "invalid";
        Assert.False(properties.TryGetValue(key, out float _));
        Assert.Throws<InvalidOperationException>(() => properties.GetRequiredValue(key));
        // Compatible legacy numeric representations remain readable.
        properties[key.Id] = new XisfPropertyData("Float64", 30.5);
        Assert.Equal(30.5f, properties.GetRequiredValue(key));
        Assert.Throws<ArgumentNullException>(() => properties.Set(XisfKnownProperties.Observation.Object.Name, null!));

        var content = new XisfDocumentData();
        content.Images.Add(XisfImageData.FromSamples(new byte[] { 1 }, [1], 1));
        content.Images[0].Properties.Set(key, 30f);
        var parsed = RoundTrip(content).EditXml(xml => xml.Descendants(XName.Get("Property", "http://www.pixinsight.com/xisf"))
            .Single(element => (string?)element.Attribute("id") == key.Id).SetAttributeValue("type", "FutureExposure")).Images[0].Properties;
        Assert.False(parsed.TryGetValue(key, out float _));
        Assert.Throws<InvalidOperationException>(() => parsed.GetRequiredValue(key));
    }

    [Fact]
    public void AstrometricShapesAndArrayOwnershipAreEnforced()
    {
        var properties = new XisfPropertyCollection();
        double[] coordinates = [10, 20];
        var key = XisfKnownProperties.AstrometricSolution.ReferenceCelestialCoordinates;
        properties.Set(key, coordinates);
        coordinates[0] = 99;
        double[] copy = properties.GetRequiredValue(key);
        Assert.Equal(new double[] { 10, 20 }, copy);
        copy[0] = 77;
        Assert.Equal(10, properties.GetRequiredValue(key)[0]);
        Assert.Throws<ArgumentException>(() => properties.Set(key, new double[] { 1 }));
        Assert.Throws<ArgumentException>(() => properties.Set(XisfKnownProperties.AstrometricSolution.LinearTransformationMatrix, new double[3, 3]));
        Assert.Throws<ArgumentException>(() => properties.Set(XisfKnownProperties.AstrometricSolution.ControlPoints.Image, new double[2, 3]));
        Assert.Throws<ArgumentException>(() => properties.Set(XisfKnownProperties.AstrometricSolution.DistortionModel.ImageToProjection.Global.X.Normalization, new double[2]));
        properties[key.Id] = new XisfPropertyData("F64Vector", new double[3]);
        Assert.False(properties.TryGetValue(key, out double[] _));

        var image = XisfImageData.FromSamples(new byte[] { 1 }, [1], 1);
        image.Properties.Set(key, new double[] { 10, 20 });
        image.Properties.Set(XisfKnownProperties.AstrometricSolution.ProjectiveTransformation.ImageToProjection,
            new double[,] { { 1, 0, 2 }, { 0, 1, 3 }, { 0, 0, 1 } });
        image.Properties.Set(XisfKnownProperties.AstrometricSolution.DistortionModel.ProjectionToImage.Polynomial, true);
        image.Properties.Set(XisfKnownProperties.AstrometricSolution.DistortionModel.ImageToProjection.Local.X.NodeOffsets, new int[] { 0, 2 });
        var content = new XisfDocumentData(); content.Images.Add(image);
        var parsed = RoundTrip(content).Images[0].Properties;
        var parsedCopy = parsed.GetRequiredValue(key); parsedCopy[0] = 42;
        Assert.Equal(10, parsed.GetRequiredValue(key)[0]);
        Assert.Equal(2, parsed.GetRequiredValue(XisfKnownProperties.AstrometricSolution.ProjectiveTransformation.ImageToProjection)[0, 2]);
        Assert.True(parsed.GetRequiredValue(XisfKnownProperties.AstrometricSolution.DistortionModel.ProjectionToImage.Polynomial));
        Assert.Equal(new[] { 0, 2 }, parsed.GetRequiredValue(XisfKnownProperties.AstrometricSolution.DistortionModel.ImageToProjection.Local.X.NodeOffsets));
    }

    [Fact]
    public void CatalogIsUniqueReadOnlyAndIncludesBothDistortionDirections()
    {
        Assert.Equal(159, XisfKnownProperties.All.Count);
        Assert.Equal(159, XisfKnownProperties.All.Select(key => key.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(typeof(float), XisfKnownProperties.Instrument.ExposureTime.ValueType);
        Assert.Equal("seconds", XisfKnownProperties.Instrument.ExposureTime.Unit);
        Assert.Equal("meters", XisfKnownProperties.Instrument.Telescope.FocalLength.Unit);
        Assert.Contains(XisfKnownProperties.AstrometricSolution.DistortionModel.ImageToProjection.Fallback.Y.Nodes, XisfKnownProperties.All);
        Assert.Contains(XisfKnownProperties.AstrometricSolution.DistortionModel.ProjectionToImage.Fallback.Y.Nodes, XisfKnownProperties.All);
        Assert.Throws<NotSupportedException>(() => ((IList<XisfPropertyKey>)XisfKnownProperties.All).Clear());
        // Every discoverable static key is included in the catalog.
        Assert.Equal(CollectKeys(typeof(XisfKnownProperties)).OrderBy(key => key.Id), XisfKnownProperties.All.OrderBy(key => key.Id));
    }

    private static IEnumerable<XisfPropertyKey> CollectKeys(Type type) => type.GetProperties(BindingFlags.Public | BindingFlags.Static)
        .Where(property => typeof(XisfPropertyKey).IsAssignableFrom(property.PropertyType))
        .Select(property => (XisfPropertyKey)property.GetValue(null)!)
        .Concat(type.GetNestedTypes(BindingFlags.Public).SelectMany(CollectKeys));

    private static XisfDocument RoundTrip(XisfDocumentData content)
    {
        using var stream = new MemoryStream();
        XisfWriter.Write(stream, content);
        stream.Position = 0;
        return XisfReader.Load(stream);
    }
}
