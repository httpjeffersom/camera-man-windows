using CameraMan.Windows.Camera;

namespace CameraMan.Windows.Tests;

public class FrameBufferTests
{
    [Fact]
    public void EmptyBufferHasNoFrame()
    {
        var buffer = new FrameBuffer();

        Assert.Null(buffer.LatestInfo);
        Assert.False(buffer.TryCopyLatest(new byte[16], out _));
    }

    [Fact]
    public void CommittedFrameIsCopiedAndVersioned()
    {
        var buffer = new FrameBuffer();
        var source = buffer.BeginWrite(8);
        source[0] = 1;
        source[1] = 2;
        source[2] = 3;
        source[3] = 4;
        source[4] = 5;
        source[5] = 6;
        source[6] = 7;
        source[7] = 8;
        buffer.CommitWrite(2, 1, 8);

        var destination = new byte[8];
        Assert.True(buffer.TryCopyLatest(destination, out var info));

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, destination);
        Assert.Equal(2, info.Width);
        Assert.Equal(1, info.Height);
        Assert.Equal(8, info.Stride);
        Assert.Equal(1, info.Version);
    }

    [Fact]
    public void DestinationMustBeLargeEnough()
    {
        var buffer = new FrameBuffer();
        buffer.BeginWrite(8);
        buffer.CommitWrite(2, 1, 8);

        Assert.False(buffer.TryCopyLatest(new byte[7], out _));
    }

    [Fact]
    public void ProducerAndConsumerUseDifferentSlots()
    {
        var buffer = new FrameBuffer();
        var first = buffer.BeginWrite(4);
        first[0] = 10;
        buffer.CommitWrite(1, 1, 4);

        // Writing the next frame must not touch the published bytes while the UI copies them.
        var second = buffer.BeginWrite(4);
        second[0] = 20;
        var destination = new byte[4];
        Assert.True(buffer.TryCopyLatest(destination, out _));
        Assert.Equal(10, destination[0]);

        buffer.CommitWrite(1, 1, 4);
        Assert.True(buffer.TryCopyLatest(destination, out _));
        Assert.Equal(20, destination[0]);
    }

    [Fact]
    public void ClearDropsThePublishedFrame()
    {
        var buffer = new FrameBuffer();
        buffer.BeginWrite(4);
        buffer.CommitWrite(1, 1, 4);
        var version = buffer.Version;

        buffer.Clear();

        Assert.Null(buffer.LatestInfo);
        Assert.True(buffer.Version > version);
        Assert.False(buffer.TryCopyLatest(new byte[4], out _));
    }

    [Fact]
    public void GrowingFramesReallocateTheWriteSlot()
    {
        var buffer = new FrameBuffer();
        buffer.BeginWrite(4);
        buffer.CommitWrite(1, 1, 4);

        var larger = buffer.BeginWrite(16);
        Assert.True(larger.Length >= 16);
        larger[15] = 42;
        buffer.CommitWrite(2, 2, 8);

        var destination = new byte[16];
        Assert.True(buffer.TryCopyLatest(destination, out var info));
        Assert.Equal(16, info.Stride * info.Height);
        Assert.Equal(42, destination[15]);
    }
}
