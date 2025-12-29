using System.Windows;
using System.Windows.Controls;
using DesktopOrganizer.UI.ViewModels;
using Point = System.Windows.Point;
using UserControl = System.Windows.Controls.UserControl;
using MessageBox = System.Windows.MessageBox;
using MenuItem = System.Windows.Controls.MenuItem;
using DragEventArgs = System.Windows.DragEventArgs;
using DataFormats = System.Windows.DataFormats;

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

    private void Item_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) { }
    private void Item_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) { }

    private void ShelfControl_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // 親Window (OverlayWindow) のDataContext (OverlayViewModel) から IsEditMode を取得して判定
        var window = Window.GetWindow(this);
        if (window?.DataContext is OverlayViewModel overlayVm && !overlayVm.IsEditMode)
        {
            // View Mode: ドラッグ不可、クリックは通す（内部のアイテムクリック等）
            return;
        }

        // Edit Mode: ドラッグ開始
        _isDragging = true;
        // Window（Canvas相当）からの相対座標を取得。論理ピクセル。
        _startPoint = e.GetPosition(null);
        this.CaptureMouse();

        if (DataContext is ShelfViewModel vm)
        {
            _startPosition = new Point(vm.Left, vm.Top);
        }
    }

    private void ShelfControl_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isDragging && DataContext is ShelfViewModel vm)
        {
            // Windowからの相対座標（論理ピクセル）
            var currentPoint = e.GetPosition(null);
            var deltaX = currentPoint.X - _startPoint.X;
            var deltaY = currentPoint.Y - _startPoint.Y;

            vm.Left = _startPosition.X + deltaX;
            vm.Top = _startPosition.Y + deltaY;
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

    private void UserControl_Drop(object sender, System.Windows.DragEventArgs e)
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

    private Point _dragStartPoint;

    private void ListBoxItem_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void ListBoxItem_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            Point position = e.GetPosition(null);
            if (Math.Abs(position.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(position.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                if (sender is ListBoxItem listBoxItem && listBoxItem.DataContext is ShelfItemViewModel itemVm)
                {
                    // ドラッグ開始
                    var data = new System.Windows.DataObject();
                    data.SetData("ShelfItemReorder", itemVm); // 内部移動用マーカー
                    System.Windows.DragDrop.DoDragDrop(listBoxItem, data, System.Windows.DragDropEffects.Move);
                    e.Handled = true;
                }
            }
        }
    }

    private void ListBoxItem_Drop(object sender, DragEventArgs e)
    {
        if (sender is ListBoxItem targetItem && targetItem.DataContext is ShelfItemViewModel targetVm)
        {
            if (e.Data.GetDataPresent("ShelfItemReorder"))
            {
                var sourceVm = e.Data.GetData("ShelfItemReorder") as ShelfItemViewModel;
                if (sourceVm != null && sourceVm != targetVm && DataContext is ShelfViewModel shelfVm)
                {
                    shelfVm.MoveItem(sourceVm, targetVm);
                    e.Handled = true;
                }
            }
        }
    }

    private void MenuItem_RenameShelf_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ShelfViewModel vm)
        {
            vm.RequestRename();
        }
    }

    private void MenuItem_DeleteShelf_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ShelfViewModel vm)
        {
            var result = MessageBox.Show($"Are you sure you want to delete shelf '{vm.Title}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                vm.RequestDelete();
            }
        }
    }

    private void MenuItem_Color_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string colorCode && DataContext is ShelfViewModel vm)
        {
            vm.ThemeColor = colorCode;
        }
    }

    private void MenuItem_SmartShelf_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ShelfViewModel vm)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "Select a folder to link to this shelf (Smart Shelf)";
            dialog.UseDescriptionForTitle = true;
            dialog.ShowNewFolderButton = true;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                vm.Title = System.IO.Path.GetFileName(dialog.SelectedPath); // タイトルをフォルダ名に更新
                vm.DirectoryPath = dialog.SelectedPath; // これでSmart Shelf化される
            }
        }
    }
}
