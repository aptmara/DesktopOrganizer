namespace DesktopOrganizer.Tests.Services;

/// <summary>
/// ShelfFactoryのユニットテスト
/// </summary>
public class ShelfFactoryTests
{
    private readonly Mock<ILayoutManager> _mockLayoutManager;
    private readonly ShelfViewModelFactory _factory;
    private readonly MonitorItem _testMonitor;

    public ShelfFactoryTests()
    {
        _mockLayoutManager = new Mock<ILayoutManager>();
        _mockLayoutManager
            .Setup(lm => lm.CalculatePhysicalRect(It.IsAny<Shelf>(), It.IsAny<MonitorItem>()))
            .Returns(new NativeMethods.RECT { Left = 100, Top = 100, Right = 300, Bottom = 300 });

        _factory = new ShelfViewModelFactory(_mockLayoutManager.Object);

        _testMonitor = new MonitorItem
        {
            DeviceName = "TEST_MONITOR",
            Bounds = new NativeMethods.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 },
            WorkArea = new NativeMethods.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1040 },
            DpiScaleX = 1.0,
            DpiScaleY = 1.0,
            IsPrimary = true
        };
    }

    [Fact]
    public void Create_WithManualType_ReturnsManualShelfViewModel()
    {
        // Arrange
        var shelf = new Shelf { Type = ShelfType.Manual, Title = "Manual" };

        // Act
        var vm = _factory.Create(shelf, _testMonitor, () => { });

        // Assert
        vm.Should().BeOfType<ManualShelfViewModel>();
        vm.Title.Should().Be("Manual");
    }

    [Fact]
    public void Create_WithSmartFolderType_ReturnsSmartFolderViewModel()
    {
        // Arrange
        var shelf = new Shelf { Type = ShelfType.SmartFolder, Title = "Smart" };

        // Act
        var vm = _factory.Create(shelf, _testMonitor, () => { });

        // Assert
        vm.Should().BeOfType<SmartFolderViewModel>();
    }

    [Fact]
    public void Create_WithRecentsType_ReturnsRecentsShelfViewModel()
    {
        // Arrange
        var shelf = new Shelf { Type = ShelfType.Recents, Title = "Recents" };

        // Act
        var vm = _factory.Create(shelf, _testMonitor, () => { });

        // Assert
        vm.Should().BeOfType<RecentsShelfViewModel>();
    }
}
