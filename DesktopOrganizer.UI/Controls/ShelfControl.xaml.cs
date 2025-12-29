using System.Windows;
using System.Windows.Controls;
using DesktopOrganizer.UI.ViewModels;

namespace DesktopOrganizer.UI.Controls;

public partial class ShelfControl : UserControl
{
    private bool _isDragging;
    private Point _startPoint;
    private Point _startPosition;

    public ShelfControl()
    {
        InitializeComponent();

        this.MouseLeftButtonDown += ShelfControl_MouseLeftButtonDown;
        this.MouseMove += ShelfControl_MouseMove;
        this.MouseLeftButtonUp += ShelfControl_MouseLeftButtonUp;
    }

    private void ShelfControl_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // 編集モードの場合のみドラッグを許可
        // OverlayWindowのロジックにより、Viewモードではマウスイベントを受け取らないため
        // ここに来る時点で編集モードである

        _isDragging = true;
        _startPoint = e.GetPosition(this); // コントロール相対ではなくスクリーン座標が必要

        // マウスキャプチャ推奨
        this.CaptureMouse();

        if (DataContext is ShelfViewModel vm)
        {
            // スクリーン座標を取得
            _startPoint = PointToScreen(e.GetPosition(this));
            _startPosition = new Point(vm.Left, vm.Top);
        }
    }

    private void ShelfControl_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isDragging && DataContext is ShelfViewModel vm)
        {
            var currentPoint = PointToScreen(e.GetPosition(this));
            var deltaX = currentPoint.X - _startPoint.X;
            var deltaY = currentPoint.Y - _startPoint.Y;

            // PointToScreenはデバイスピクセルを返すため
            // ViewModelが期待する論理ピクセルに変換が必要

            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                var transform = source.CompositionTarget.TransformFromDevice;
                var logicalDelta = transform.Transform(new Point(deltaX, deltaY));

                vm.Left = _startPosition.X + logicalDelta.X;
                vm.Top = _startPosition.Y + logicalDelta.Y;
            }
        }
    }

    private void ShelfControl_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            this.ReleaseMouseCapture();

            if (DataContext is ShelfViewModel vm)
            {
                vm.OnMoved();
            }
        }
    }

    private void MenuItem_Open_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is ShelfItemViewModel itemVm)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = itemVm.TargetPath,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open: {ex.Message}");
            }
        }
    }

    private void MenuItem_Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is ShelfItemViewModel itemVm)
        {
            if (this.DataContext is ShelfViewModel shelfVm)
            {
                shelfVm.RemoveItem(itemVm);
            }
        }
    }

    private void UserControl_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is ShelfViewModel vm)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string file in files)
                {
                    vm.AddFile(file);
                }
            }
        }
    }
}
