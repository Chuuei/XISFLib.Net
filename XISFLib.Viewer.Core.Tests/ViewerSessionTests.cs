using XISFLib.Viewer;

namespace XISFLib.Viewer.Core.Tests;

public class ViewerSessionTests
{
    [Fact]
    public void XisfMaterializesOnlySelectedImagesAndRetainsCopySource()
    {
        string path = CreateXisf();
        try
        {
            var session = ViewerSession.Load(path);
            Assert.True(session.CanCopyOriginal);
            Assert.NotNull(session.Images[0]);
            ViewerMetadataBuilder.BuildMetadata(session.Xisf!, path);
            Assert.Null(session.Images[1]);
            Assert.Equal("2: second", session.GetImageLabel(1));
            Assert.Equal(new byte[] { 3 }, session.GetImage(1).GetSamples<byte>());
            Assert.Same(session.GetImage(1), session.GetImage(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.GetImage(2));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void FitsLoadingKeepsOriginalHeaderAndMatchesViewerColorInterpretation()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".FITS");
        try
        {
            var hdu = FitsHdu.CreateImage(FitsImage.FromPhysicalSamples(new byte[] { 1, 2, 3 }, [1, 1, 3]), true);
            hdu.Header.Set("OBJECT", "M31");
            FitsWriter.Write(path, new FitsDocument([hdu]));
            var session = ViewerSession.Load(path);
            Assert.False(session.CanCopyOriginal);
            Assert.Equal("M31", session.Fits!.Hdus[0].Header.GetRequiredValue<string>("OBJECT"));
            Assert.Equal(3, session.GetImage(0).ChannelCount);
            Assert.Equal("RGB", session.GetImage(0).ColorSpace);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task FailedLoadKeepsPreviewAndBusyStateRecovers()
    {
        string path = CreateXisf();
        try
        {
            var presentations = new List<XisfBgra32Image?>();
            var vm = new WorkbenchViewModel(image => { presentations.Add(image); return Task.CompletedTask; });
            vm.AutoStretch = false;
            vm.FilePath = path;
            await vm.LoadCommand.ExecuteAsync(null);
            Assert.StartsWith("Loaded ", vm.Status);
            Assert.Equal(2, vm.ImageLabels.Count);
            Assert.Single(presentations);
            vm.SelectedImageIndex = 1;
            await vm.SelectImageCommand.ExecuteAsync(null);
            Assert.Equal(3, presentations[^1]!.Pixels[0]);
            vm.FilePath = path + ".missing";
            await vm.LoadCommand.ExecuteAsync(null);
            Assert.Equal(2, presentations.Count);
            Assert.Equal(2, vm.ImageLabels.Count);
            Assert.False(vm.IsBusy);
            Assert.True(vm.LoadCommand.CanExecute(null));
            await vm.UnloadCommand.ExecuteAsync(null);
            Assert.Null(presentations[^1]);
            Assert.Empty(vm.ImageLabels);
            Assert.False(vm.ApplyCommand.CanExecute(null));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task WindowClosureDuringLoadPreventsLaterPresentation()
    {
        string path = CreateXisf();
        try
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var presentations = 0;
            var vm = new WorkbenchViewModel(_ => { presentations++; return Task.CompletedTask; }, () =>
            { entered.SetResult(); return gate.Task; });
            vm.FilePath = path;
            var load = vm.LoadCommand.ExecuteAsync(null);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            vm.Close();
            gate.SetResult();
            await load;
            Assert.Equal(1, presentations);
            Assert.False(vm.CanInteract);
            Assert.Empty(vm.Diagnostics);
        }
        finally { File.Delete(path); }
    }

    private static string CreateXisf()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xisf");
        var first = XisfImageData.FromSamples(new byte[] { 2 }, [1, 1], 1); first.Id = "first";
        var second = XisfImageData.FromSamples(new byte[] { 3 }, [1, 1], 1); second.Id = "second";
        XisfWriter.Write(path, [first, second]);
        return path;
    }
}
