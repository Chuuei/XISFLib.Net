using XISFLib.Viewer;

namespace XISFLib.Viewer.Core.Tests;

public class ViewerMetadataTests
{
    [Fact]
    public void XisfTreeIncludesTypedPropertiesTablesAncillaryDataAndUnknownXml()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xish");
        try
        {
            File.WriteAllText(path, """
                <xisf version="1.0" xmlns="http://www.pixinsight.com/xisf">
                  <Metadata><Property id="XISF:Title" type="String">Test session</Property></Metadata>
                  <Property id="Test:Global" type="String">Global value</Property>
                  <Table id="stars" caption="Catalog"><Structure><Field id="name" type="String" header="Star" format="%s" /></Structure><Row><Cell value="Vega" /></Row></Table>
                  <Image id="light" geometry="1:1:1" sampleFormat="UInt8" colorSpace="Gray" location="inline:base64">Kg==
                    <Property id="Test:Exposure" type="Float32" value="30" format="%.1f" comment="Seconds" />
                    <FITSKeyword name="OBJECT" value="'M31'" comment="Target" />
                    <Resolution horizontal="100" vertical="150" unit="inch" />
                    <ColorFilterArray pattern="RGGB" width="2" height="2" name="Bayer" />
                    <ICCProfile location="inline:base64">AA==</ICCProfile>
                    <Thumbnail geometry="1:1:1" sampleFormat="UInt8" colorSpace="Gray" location="inline:base64">Kg==</Thumbnail>
                    <UnknownExtension customValue="retained">opaque content</UnknownExtension>
                  </Image>
                </xisf>
                """);
            var roots = ViewerMetadataBuilder.BuildMetadata(XisfReader.Load(path), path);
            Assert.Equal(new[] { "File", "Document", "Metadata", "Document properties", "Tables", "Images", "Complete XISF header" }, roots.Select(n => n.Name));
            Assert.True(roots[0].IsExpanded); Assert.True(roots[1].IsExpanded);
            Assert.False(roots[5].IsExpanded);
            Assert.Equal("Test session", Child(roots[2], "XISF:Title").Value);
            Assert.Equal("Global value", Child(roots[3], "Test:Global").Value);
            var table = Child(roots[4], "stars");
            Assert.Equal("Catalog", table.Value);
            Assert.Equal("Vega", Child(Child(Child(table, "Rows"), "Row 1"), "name").Value);
            Assert.Equal("Star", Child(Child(Child(table, "Fields"), "name"), "Header").Value);
            var image = Assert.Single(roots[5].Children);
            var property = Child(Child(image, "Properties"), "Test:Exposure");
            Assert.Equal("Float32", Child(property, "Type").Value);
            Assert.Equal("%.1f", Child(property, "Format").Value);
            Assert.Equal("Seconds", Child(property, "Comment").Value);
            Assert.Equal("Target", Child(Child(Child(image, "FITS keywords"), "OBJECT"), "Comment").Value);
            Assert.Contains(image.Children, n => n.Name == "Resolution");
            Assert.Contains(image.Children, n => n.Name == "Color filter array");
            Assert.Equal("1 bytes", Child(image, "ICC profile").Value);
            Assert.Contains(image.Children, n => n.Name == "Thumbnail");
            Assert.Contains(Flatten(roots), n => n.Name == "Unknown extension" && n.Value == "opaque content");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void FitsTreePreservesHduAndRepeatedCardOrderAndComments()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".fits");
        try
        {
            var hdu = FitsHdu.CreateImage(FitsImage.FromPhysicalSamples(new byte[] { 42 }, [1, 1]), true);
            hdu.Header.Add(new FitsHeaderCard("HISTORY", "first")); hdu.Header.Add(new FitsHeaderCard("HISTORY", "second"));
            hdu.Header.Set("OBJECT", "M31", "Target");
            FitsWriter.Write(path, new FitsDocument([hdu]));
            var document = FitsReader.Load(path);
            var roots = ViewerMetadataBuilder.BuildFitsMetadata(document, path);
            var cards = Assert.Single(roots[2].Children).Children;
            Assert.Equal(document.Hdus[0].Header.Select(c => c.Keyword), cards.Select(n => n.Name));
            Assert.Equal(new[] { "first", "second" }, cards.Where(n => n.Name == "HISTORY").Select(n => n.Value));
            Assert.Equal("Target", Child(Child(roots[2].Children[0], "OBJECT"), "Comment").Value);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task MetadataIsPublishedAfterPreviewYieldAndRetainedOnFailedLoad()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xisf");
        try
        {
            XisfWriter.Write(path, [XisfImageData.FromSamples(new byte[] { 42 }, [1, 1], 1)]);
            WorkbenchViewModel? vm = null;
            bool presented = false;
            vm = new(_ => { presented = true; return Task.CompletedTask; }, () =>
            {
                Assert.True(presented); Assert.Empty(vm!.Metadata); Assert.True(vm.IsBusy);
                return Task.CompletedTask;
            });
            vm.FilePath = path;
            await vm.LoadCommand.ExecuteAsync(null);
            Assert.NotEmpty(vm.Metadata);
            var tree = vm.Metadata;
            await vm.ValidateCommand.ExecuteAsync(null);
            Assert.StartsWith("Validation complete", vm.Status);
            vm.FilePath = path + ".missing";
            await vm.LoadCommand.ExecuteAsync(null);
            Assert.Same(tree, vm.Metadata);
            await vm.UnloadCommand.ExecuteAsync(null);
            Assert.Empty(vm.Metadata);
            Assert.False(vm.ValidateCommand.CanExecute(null));
        }
        finally { File.Delete(path); }
    }

    private static MetadataNode Child(MetadataNode parent, string name) => Assert.Single(parent.Children, n => n.Name == name);
    private static IEnumerable<MetadataNode> Flatten(IEnumerable<MetadataNode> nodes) =>
        nodes.SelectMany(n => new[] { n }.Concat(Flatten(n.Children)));
}
