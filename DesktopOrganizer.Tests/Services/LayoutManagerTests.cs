namespace DesktopOrganizer.Tests.Services;

/// <summary>
/// LayoutManagerのユニットテスト
/// </summary>
public class LayoutManagerTests
{
    private readonly MonitorItem _testMonitor;

    public LayoutManagerTests()
    {
        _testMonitor = new MonitorItem
        {
            DeviceName = "TEST_MONITOR",
            Bounds = new NativeMethods.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 },
            WorkArea = new NativeMethods.RECT { Left = 0, Top = 40, Right = 1920, Bottom = 1080 },
            DpiScaleX = 1.0,
            DpiScaleY = 1.0,
            IsPrimary = true
        };
    }

    [Fact]
    public void CalculatePhysicalRect_WithNormalizedCoordinates_ReturnsCorrectPixelRect()
    {
        // Arrange
        var layoutManager = new LayoutManager();
        var shelf = new Shelf
        {
            X = 0.5,  // 50% from left
            Y = 0.5,  // 50% from top
            Width = 0.2,  // 20% width
            Height = 0.2  // 20% height
        };

        // Act
        var rect = layoutManager.CalculatePhysicalRect(shelf, _testMonitor);

        // Assert
        // X: 0 + 1920 * 0.5 = 960
        // Y: 40 + 1040 * 0.5 = 560
        // Width: 1920 * 0.2 = 384
        // Height: 1040 * 0.2 = 208
        rect.Left.Should().Be(960);
        rect.Top.Should().Be(560);
        rect.Width.Should().Be(384);
        rect.Height.Should().Be(208);
    }

    [Fact]
    public void FindBestMonitor_WithMatchingDeviceId_ReturnsCorrectMonitor()
    {
        // Arrange
        var layoutManager = new LayoutManager();
        var monitors = new List<MonitorItem>
        {
            new MonitorItem { DeviceName = "MONITOR_1", IsPrimary = true },
            new MonitorItem { DeviceName = "MONITOR_2", IsPrimary = false },
        };
        var shelf = new Shelf { TargetMonitorDeviceId = "MONITOR_2" };

        // Act
        var result = layoutManager.FindBestMonitor(shelf, monitors);

        // Assert
        result.DeviceName.Should().Be("MONITOR_2");
    }

    [Fact]
    public void FindBestMonitor_WithMissingDeviceId_ReturnsPrimaryMonitor()
    {
        // Arrange
        var layoutManager = new LayoutManager();
        var monitors = new List<MonitorItem>
        {
            new MonitorItem { DeviceName = "MONITOR_1", IsPrimary = true },
            new MonitorItem { DeviceName = "MONITOR_2", IsPrimary = false },
        };
        var shelf = new Shelf { TargetMonitorDeviceId = "MISSING_MONITOR" };

        // Act
        var result = layoutManager.FindBestMonitor(shelf, monitors);

        // Assert
        result.DeviceName.Should().Be("MONITOR_1"); // Primary fallback
    }

    [Fact]
    public void UpdateShelfPosition_CalculatesNormalizedCoordinates()
    {
        // Arrange
        var layoutManager = new LayoutManager();
        var shelf = new Shelf { X = 0, Y = 0, Width = 0.1, Height = 0.1 };
        var newRect = new NativeMethods.RECT
        {
            Left = 480,  // 25% of 1920
            Top = 300,   // (300 - 40) / 1040 = 25%
            Right = 864, // 45% of 1920
            Bottom = 560 // (560 - 40) / 1040 = 50%
        };

        // Act
        layoutManager.UpdateShelfPosition(shelf, newRect, _testMonitor);

        // Assert
        shelf.X.Should().BeApproximately(0.25, 0.01);
        shelf.Y.Should().BeApproximately(0.25, 0.01);
        shelf.Width.Should().BeApproximately(0.2, 0.01);
        shelf.Height.Should().BeApproximately(0.25, 0.01);
    }
}
