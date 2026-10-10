using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace XISFLib;

public static class BatchConversion
{
    private static readonly (string Property, string Keyword)[] Mappings =
    [
        ("Observation:Object:Name", "OBJECT"),
        ("Observation:Observer:Name", "OBSERVER"),
        ("Instrument:Camera:Name", "INSTRUME"),
        ("Instrument:Telescope:Name", "TELESCOP"),
        ("Instrument:Filter:Name", "FILTER"),
        ("Observation:ExposureTime", "EXPTIME"),
        ("Observation:Time:Start", "DATE-OBS"),
        ("Instrument:Camera:Gain", "GAIN"),
        ("Instrument:Camera:Offset", "OFFSET"),
        ("Instrument:Camera:BinningX", "XBINNING"),
        ("Instrument:Camera:BinningY", "YBINNING"),
        ("Instrument:Camera:Temperature", "CCD-TEMP"),
        ("Observation:Center:RA", "RA"),
        ("Observation:Center:Dec", "DEC")
    ];

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        int parallelConversions = 8;
        if (args.Length is < 1 or > 2 || !Directory.Exists(args[0]) ||
            (args.Length == 2 && (!int.TryParse(args[1], out parallelConversions) || parallelConversions <= 0)))
        {
            error.WriteLine("Usage: XISFLib.Convert <folder> [parallel-conversions] (an existing folder and a positive integer count are required; default: 8)");
            return 2;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(args[0]).Where(path =>
                Path.GetExtension(path).Equals(".xisf", StringComparison.OrdinalIgnoreCase)).Order().ToArray();
        }
        catch (Exception exception)
        {
            error.WriteLine($"Cannot enumerate folder: {exception.Message}");
            return 1;
        }

        int converted = 0, failed = 0;
        object logLock = new();
        Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = parallelConversions }, source =>
        {
            string destination = Path.ChangeExtension(source, ".fits");
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                if (File.Exists(destination)) throw new IOException("Output already exists; not overwritten.");
                FitsDocument document = CreateDocument(XisfReader.Load(source));
                using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    FitsWriter.Write(stream, document, new FitsWriterOptions { WriteChecksums = true });
                File.Move(temporary, destination, false);
                Interlocked.Increment(ref converted);
                lock (logLock) output.WriteLine($"Converted: {source} -> {destination}");
            }
            catch (Exception exception)
            {
                Interlocked.Increment(ref failed);
                lock (logLock) error.WriteLine($"Failed: {source}: {exception.Message}");
            }
            finally
            {
                try
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
                catch (Exception exception)
                {
                    lock (logLock) error.WriteLine($"Cannot remove temporary output {temporary}: {exception.Message}");
                }
            }
        });
        output.WriteLine($"Converted {converted}; failed {failed}; found {files.Length} XISF file(s).");
        return failed == 0 ? 0 : 1;
    }

    public static FitsDocument CreateDocument(XisfDocument source)
    {
        List<FitsHdu> hdus = FitsConverter.FromXisf(source.Images, ConversionLossPolicy.Allow).Hdus.ToList();
        for (int index = 0; index < source.Images.Count; index++)
        {
            FitsHeader header = hdus[index].Header;
            foreach ((string property, string keyword) in Mappings)
            {
                if (header.FindFirst(keyword) is not null) continue;
                XisfProperty? value = source.Images[index].Properties.GetValueOrDefault(property)
                    ?? source.Properties.GetValueOrDefault(property);
                if (value?.Value is null or Array) continue;
                object scalar = value.Value is DateTime time ? time.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture)
                    : value.Value is DateTimeOffset offset ? offset.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture)
                    : value.Value;
                header.Add(new FitsHeaderCard(keyword, scalar, value.Comment));
            }
            header.Add(new FitsHeaderCard("HISTORY", "XISF metadata archive in XISFMETA extension (UTF-8 XML)."));
        }

        XElement archive = new("XisfMetadataArchive", new XAttribute("version", 1),
            new XElement("OriginalHeader", source.Xml.Root),
            Properties("Metadata", source.Metadata), Properties("Properties", source.Properties),
            source.Images.Select((image, index) => new XElement("Image", new XAttribute("index", index),
                Properties("Properties", image.Properties))));
        byte[] data = Encoding.UTF8.GetBytes(archive.ToString(SaveOptions.DisableFormatting));
        hdus.Add(new FitsHdu(FitsHduType.BinaryTable,
        [
            new FitsHeaderCard("XTENSION", "BINTABLE"), new FitsHeaderCard("BITPIX", 8),
            new FitsHeaderCard("NAXIS", 2), new FitsHeaderCard("NAXIS1", data.Length),
            new FitsHeaderCard("NAXIS2", 1), new FitsHeaderCard("PCOUNT", 0), new FitsHeaderCard("GCOUNT", 1),
            new FitsHeaderCard("TFIELDS", 1), new FitsHeaderCard("TTYPE1", "METADATA"),
            new FitsHeaderCard("TFORM1", $"{data.Length}B"), new FitsHeaderCard("EXTNAME", "XISFMETA")
        ], data));
        return new FitsDocument(hdus);
    }

    private static XElement Properties(string name, IReadOnlyDictionary<string, XisfProperty> properties) =>
        new(name, properties.Values.Select(property => new XElement("Property",
            new XAttribute("id", property.Id), new XAttribute("type", property.Type), Value(property.Value))));

    private static XElement Value(object? value) => value is Array array
        ? new XElement("Array", new XAttribute("dimensions", string.Join(" ", Enumerable.Range(0, array.Rank).Select(array.GetLength))),
            array.Cast<object?>().Select(Value))
        : new XElement("Value", value switch
        {
            null => (object)new XAttribute("null", true),
            DateTime time => time.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset time => time.ToString("O", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        });
}