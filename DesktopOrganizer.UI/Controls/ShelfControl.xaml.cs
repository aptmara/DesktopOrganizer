using System.Windows;
using System.Windows.Controls;
using DesktopOrganizer.UI.ViewModels;
using Point = System.Windows.Point;
using UserControl = System.Windows.Controls.UserControl;
using MessageBox = System.Windows.MessageBox;
using MenuItem = System.Windows.Controls.MenuItem;
using DragEventArgs = System.Windows.DragEventArgs;
using DataFormats = System.Windows.DataFormats;
using System.Windows.Input;
using TextBox = System.Windows.Controls.TextBox;

namespace DesktopOrganizer.UI.Controls;

public partial class ShelfControl : UserControl
{
    private bool _isDragging;
    private Point _startPoint;
    private Point _startPosition;

    public static RoutedCommand OpenItemCommand = new RoutedCommand();
    public static RoutedCommand RenameShelfCommand = new RoutedCommand();

    public ShelfControl()
    {
        InitializeComponent();

        this.MouseLeftButtonDown += ShelfControl_MouseLeftButtonDown;
        this.MouseMove += ShelfControl_MouseMove;
        this.MouseLeftButtonUp += ShelfControl_MouseLeftButtonUp;

        // edit mode外ではコンテキストメニューを抑制
        this.ContextMenuOpening += ShelfControl_ContextMenuOpening;

        this.CommandBindings.Add(new System.Windows.Input.CommandBinding(OpenItemCommand, OpenItemExecuted));
        this.CommandBindings.Add(new System.Windows.Input.CommandBinding(RenameShelfCommand, RenameShelfExecuted));
    }

    /// <summary>
    /// edit mode外ではコンテキストメニューを表示しない
    /// </summary>
    private void ShelfControl_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (!IsEditMode)
        {
            e.Handled = true;
        }
    }

    private void RenameShelfExecuted(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
        if (DataContext is ShelfViewModel vm)
        {
            // edit mode時は名前変更を開始
            if (IsEditMode)
            {
                vm.IsRenaming = true;
            }
            else
            {
                // 通常モードではダブルクリックで折りたたみをトグル
                vm.IsCollapsed = !vm.IsCollapsed;
            }
        }
    }

    private void OpenItemExecuted(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is ShelfItemViewModel itemVm)
        {
            OpenItem(itemVm);
        }
    }

    private void Item_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) { }
    private void Item_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) { }

    private void ShelfControl_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // 親Window (OverlayWindow) のDataContext (OverlayViewModel) から IsEditMode を取得して判定
        var window = Window.GetWindow(this);
        if (window?.DataContext is OverlayViewModel overlayVm)
        {
            if (DataContext is ShelfViewModel shelfVm)
            {
                overlayVm.BringToFront(shelfVm);
            }

            if (!overlayVm.IsEditMode)
            {
                // View Mode: ドラッグ不可、クリックは通す（内部のアイテムクリック等）
                return;
            }
        }
        else
        {
            // Fallback if context is missing
            return;
        }

        if (DataContext is ShelfViewModel vm)
        {
            // ViewModelの値を正とする (Visual Tree計算は不安定な場合があるため廃止)
            _startPosition = new Point(vm.Left, vm.Top);

            // 座標基準をWindowに固定して取得
            if (window != null)
            {
                DesktopOrganizer.Core.Utilities.Logger.Log($"Drag Start: Mouse={e.GetPosition(window)}, VM_Pos=({vm.Left},{vm.Top})");
            }
            else
            {
                DesktopOrganizer.Core.Utilities.Logger.Log($"Drag Start (Fallback): Mouse={e.GetPosition(null)}, VM_Pos=({vm.Left},{vm.Top})");
            }
        }

        // Setup Dragging State LAST to avoid premature MouseMove event
        if (window != null)
        {
            _startPoint = e.GetPosition(window);
        }
        else
        {
            _startPoint = e.GetPosition(null);
        }

        _isDragging = true;
        this.CaptureMouse();

        e.Handled = true; // イベントをここで消費し、OverlayWindowへの伝播（Edit終了）を防ぐ
    }

    private bool IsEditMode
    {
        get
        {
            var window = Window.GetWindow(this);
            if (window?.DataContext is OverlayViewModel overlayVm)
            {
                return overlayVm.IsEditMode;
            }
            return false;
        }
    }

    private void ShelfControl_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isDragging && DataContext is ShelfViewModel vm)
        {
            // Windowからの相対座標（論理ピクセル）
            var window = Window.GetWindow(this);
            Point currentPoint;
            if (window != null)
            {
                currentPoint = e.GetPosition(window);
            }
            else
            {
                currentPoint = e.GetPosition(null);
            }

            var deltaX = currentPoint.X - _startPoint.X;
            var deltaY = currentPoint.Y - _startPoint.Y;

            double newLeft = _startPosition.X + deltaX;
            double newTop = _startPosition.Y + deltaY;

            if (ShelfViewModel.IsGridSnapEnabled)
            {
                double gs = ShelfViewModel.GridSize;
                // Snap logic: Round to nearest multiple of GridSize
                newLeft = Math.Round(newLeft / gs) * gs;
                newTop = Math.Round(newTop / gs) * gs;
            }

            vm.Left = newLeft;
            vm.Top = newTop;
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
            OpenItem(itemVm);
        }
    }

    private void MenuItem_Remove_Click(object sender, RoutedEventArgs e)
    {
        // edit mode時のみ削除を許可
        if (!IsEditMode) return;

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
        if (DataContext is ShelfViewModel targetShelf)
        {
            // 棚間アイテム移動
            if (e.Data.GetDataPresent("ShelfItemMove"))
            {
                var sourceItem = e.Data.GetData("ShelfItemMove") as ShelfItemViewModel;
                var sourceShelf = e.Data.GetData("SourceShelf") as ShelfViewModel;

                if (sourceItem != null && sourceShelf != null && sourceShelf != targetShelf)
                {
                    // Smart Shelfへの移動は不可
                    if (!string.IsNullOrEmpty(targetShelf.DirectoryPath))
                    {
                        return;
                    }

                    // 元棚から削除して新棚に追加
                    targetShelf.AcceptItem(sourceItem);
                    sourceShelf.RemoveItem(sourceItem);
                    e.Handled = true;
                    return;
                }
            }

            // 外部ファイルドロップ
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string file in files)
                {
                    targetShelf.AddFile(file);
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
                    // ドラッグ開始（棚間移動対応）
                    var data = new System.Windows.DataObject();
                    data.SetData("ShelfItemReorder", itemVm); // 同一棚内並び替え用
                    data.SetData("ShelfItemMove", itemVm);    // 棚間移動用
                    data.SetData("SourceShelf", DataContext); // 元の棚
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
            var result = MessageBox.Show($"シェル '{vm.Title}' を削除してもよろしいですか？", "削除の確認", MessageBoxButton.YesNo, MessageBoxImage.Warning);
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

    private void MenuItem_CustomColor_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ShelfViewModel vm)
        {
            using var colorDialog = new System.Windows.Forms.ColorDialog();
            colorDialog.FullOpen = true; // フルカラーパレットを表示
            colorDialog.AnyColor = true;

            // 現在の色を初期値として設定
            try
            {
                var currentColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(vm.ThemeColor);
                colorDialog.Color = System.Drawing.Color.FromArgb(currentColor.A, currentColor.R, currentColor.G, currentColor.B);
            }
            catch { /* 変換失敗時は無視 */ }

            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var selectedColor = colorDialog.Color;
                // 半透明（CC = 80%）を維持しつつ選択色を適用
                var newColor = $"#CC{selectedColor.R:X2}{selectedColor.G:X2}{selectedColor.B:X2}";
                vm.ThemeColor = newColor;
            }
        }
    }

    private void MenuItem_SmartShelf_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ShelfViewModel vm)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "このシェルにリンクするフォルダを選択してください（スマートシェルフ）";
            dialog.UseDescriptionForTitle = true;
            dialog.ShowNewFolderButton = true;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                vm.Title = System.IO.Path.GetFileName(dialog.SelectedPath); // タイトルをフォルダ名に更新
                vm.DirectoryPath = dialog.SelectedPath; // これでSmart Shelf化される
            }
        }
    }

    private void OpenItem(ShelfItemViewModel itemVm)
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
            MessageBox.Show($"開けませんでした: {ex.Message}");
        }
    }

    private void MenuItem_RenameItem_Click(object sender, RoutedEventArgs e)
    {
        if (IsEditMode && sender is MenuItem menuItem && menuItem.DataContext is ShelfItemViewModel itemVm)
        {
            itemVm.IsRenaming = true;
        }
    }

    private void MenuItem_SnapToGrid_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            ShelfViewModel.IsGridSnapEnabled = menuItem.IsChecked;
        }
    }

    private void MenuItem_SnapToGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            menuItem.IsChecked = ShelfViewModel.IsGridSnapEnabled;
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.ContextMenu != null)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.IsOpen = true;
        }
    }

    /// <summary>
    /// 並べ替えメニュー項目がクリックされた時の処理
    /// </summary>
    private void MenuItem_Sort_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string sortTag && DataContext is ShelfViewModel vm)
        {
            if (Enum.TryParse<DesktopOrganizer.Core.Models.ShelfSortOption>(sortTag, out var option))
            {
                vm.SortOption = option;
            }
        }
    }

    private void ResizeHandle_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (DataContext is ShelfViewModel vm)
        {
            double newWidth = vm.Width + e.HorizontalChange;
            double newHeight = vm.Height + e.VerticalChange;

            // Minimum Constraints
            newWidth = Math.Max(newWidth, 100);
            newHeight = Math.Max(newHeight, 100);

            if (ShelfViewModel.IsGridSnapEnabled)
            {
                double gs = ShelfViewModel.GridSize;
                newWidth = Math.Round(newWidth / gs) * gs;
                newHeight = Math.Round(newHeight / gs) * gs;
            }

            vm.Width = newWidth;
            vm.Height = newHeight;
        }
    }
}
