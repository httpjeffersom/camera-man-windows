using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CameraMan.Windows.Views.Controls;

namespace CameraMan.Windows.Tests;

public sealed class SettingsPageFrameTests
{
    [Fact]
    public void DefaultViewportKeepsLeftMargin()
    {
        RunOnSta(() =>
        {
            var frame = CreateFrame(outerWidth: 668);

            frame.Measure(new Size(651, 600));
            frame.Arrange(new Rect(0, 0, 651, 600));

            // The scrollbar is visible: the viewport is 17 px narrower than the
            // frame, but the content width and left edge must not change.
            AssertChild(frame, expectedWidth: 604, expectedLeft: 32);
        });
    }

    [Fact]
    public void WideViewportCapsContentAndKeepsLeftMargin()
    {
        RunOnSta(() =>
        {
            var frame = CreateFrame(outerWidth: 968);

            frame.Measure(new Size(951, 700));
            frame.Arrange(new Rect(0, 0, 951, 700));

            // Content is capped at 640 px and stays 32 px from the left edge.
            AssertChild(frame, expectedWidth: 640, expectedLeft: 32);
        });
    }

    [Fact]
    public void NarrowViewportShrinksContentAndKeepsLeftMargin()
    {
        RunOnSta(() =>
        {
            var frame = CreateFrame(outerWidth: 528);

            frame.Measure(new Size(511, 600));
            frame.Arrange(new Rect(0, 0, 511, 600));

            AssertChild(frame, expectedWidth: 464, expectedLeft: 32);
        });
    }

    private static SettingsPageFrame CreateFrame(double outerWidth)
    {
        return new SettingsPageFrame
        {
            Width = outerWidth,
            Child = new Border { Height = 10 },
        };
    }

    private static void AssertChild(SettingsPageFrame frame, double expectedWidth, double expectedLeft)
    {
        var child = Assert.IsType<Border>(frame.Child);
        var offset = VisualTreeHelper.GetOffset(child);

        Assert.Equal(expectedWidth, child.RenderSize.Width, 3);
        Assert.Equal(expectedLeft, offset.X, 3);
    }

    private static void RunOnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "STA layout test timed out.");

        if (error is not null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }
}
