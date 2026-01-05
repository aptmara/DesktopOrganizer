using System.Windows;
using System.Windows.Controls;
using DesktopOrganizer.UI.ViewModels;
using Point = System.Windows.Point;
using UserControl = System.Windows.Controls.UserControl;
using MessageBox = System.Windows.MessageBox;
using MenuItem = System.Windows.Controls.MenuItem;
using DragEventArgs = System.Windows.DragEventArgs;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using System.Windows.Input;
using TextBox = System.Windows.Controls.TextBox;
using System.Windows.Documents;
using System.Windows.Media;
using System.Globalization;
using System.Windows.Data;
using DesktopOrganizer.Core.Services;
using DesktopOrganizer.UI.Infrastructure;

namespace DesktopOrganizer.UI.Controls;

public partial class ShelfControl : UserControl
{
    private bool _isDragging;
    private Point _startPoint;
    private Point _startPosition;

    public static RoutedCommand OpenItemCommand = new RoutedCommand();
    public static RoutedCommand RenameShelfCommand = new RoutedCommand();
    public static IValueConverter ClockTypeConverter { get; } = new IsClockShelfConverter();

    private InsertionAdorner? _insertionAdorner;
    private AdornerLayer? _itemAdornerLayer;
    private int _targetInsertionIndex = -1;

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
        if (DataContext is ShelfViewModelBase vm)
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

    private Point _startDragPointPhysical; // Start point in Screen Physical Pixels
    private Vector _offsetFromControlOriginToMousePhysical; // Physical offset from control's top-left to mouse click
    private DragGhostWindow? _ghostWindow;

    private void ShelfControl_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window?.DataContext is OverlayViewModel overlayVm)
        {
            if (DataContext is ShelfViewModelBase shelfVm)
            {
                overlayVm.BringToFront(shelfVm);
            }

            if (!overlayVm.IsEditMode) return;
        }
        else
        {
            return;
        }

        // Start Global Dragging using Ghost Window
        if (DataContext is ShelfViewModelBase vm)
        {
            _isDragging = true;

            // Capture Start Point (Physical Screen Coordinates) for accurate Delta calculation
            _startDragPointPhysical = PointToScreen(e.GetPosition(this));

            // Calculate the physical offset from the control's top-left to the mouse click point
            var controlOriginPhysical = PointToScreen(new Point(0, 0));
            _offsetFromControlOriginToMousePhysical = _startDragPointPhysical - controlOriginPhysical;

            // Get DPI of current control
            var dpi = VisualTreeHelper.GetDpi(this);

            _ghostWindow = new DragGhostWindow(this, this.ActualWidth, this.ActualHeight);

            // Convert controlOriginPhysical to Logical Coordinates for Window positioning
            // Subtract 20 logical pixels to account for the Margin="20" in DragGhostWindow
            _ghostWindow.Left = (controlOriginPhysical.X / dpi.DpiScaleX) - 20;
            _ghostWindow.Top = (controlOriginPhysical.Y / dpi.DpiScaleY) - 20;
            _ghostWindow.Show();

            // Hide actual control
            this.Opacity = 0.2;

            this.CaptureMouse();
            e.Handled = true;
        }
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
        if (_isDragging && _ghostWindow != null)
        {
            // Current Mouse Position in Physical Screen Pixels
            var currentMousePhysical = PointToScreen(e.GetPosition(this));

            // Calculate the target physical top-left of the ghost window
            // This maintains the initial offset from the mouse pointer
            var targetGhostTopLeftPhysical = currentMousePhysical - _offsetFromControlOriginToMousePhysical;

            // Get the DPI of the monitor where the ghost window currently resides
            var ghostDpi = VisualTreeHelper.GetDpi(_ghostWindow);

            // Convert the target physical position to logical units for the ghost window
            double newLeft = (targetGhostTopLeftPhysical.X / ghostDpi.DpiScaleX);
            double newTop = (targetGhostTopLeftPhysical.Y / ghostDpi.DpiScaleY);

            // Grid Snap for Ghost (apply to logical coordinates)
            if (ShelfViewModelBase.IsGridSnapEnabled)
            {
                double gs = ShelfViewModelBase.GridSize;
                newLeft = Math.Round(newLeft / gs) * gs;
                newTop = Math.Round(newTop / gs) * gs;
            }

            // Apply the -20 logical pixel padding compensation
            _ghostWindow.Left = newLeft - 20;
            _ghostWindow.Top = newTop - 20;
        }
    }

    private void ShelfControl_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            this.ReleaseMouseCapture();
            this.Opacity = 1.0;

            if (_ghostWindow != null)
            {
                double finalLeft = _ghostWindow.Left;
                double finalTop = _ghostWindow.Top;

                _ghostWindow.Close();
                _ghostWindow = null;

                if (DataContext is ShelfViewModelBase vm)
                {
                    var window = Window.GetWindow(this);

                    // Convert Screen Coords back to Window Relative Coords
                    // Window.Left/Top are physical screen coords in WPF usually (DPI aware)?
                    // No, Window.Left/Top are logical units.

                    double relativeLeft = finalLeft - window.Left;
                    double relativeTop = finalTop - window.Top;

                    vm.Left = relativeLeft;
                    vm.Top = relativeTop;

                    vm.OnMoved();
                }
            }
        }
    }

    private Point GetDpiScale()
    {
        var source = PresentationSource.FromVisual(this);
        if (source != null && source.CompositionTarget != null)
        {
            return new Point(source.CompositionTarget.TransformToDevice.M11, source.CompositionTarget.TransformToDevice.M22);
        }
        return new Point(1.0, 1.0);
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
            if (this.DataContext is ShelfViewModelBase shelfVm)
            {
                shelfVm.RemoveItem(itemVm);
            }
        }
    }

    private void UserControl_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (DataContext is ShelfViewModelBase targetShelf)
        {
            // 棚間アイテム移動
            if (e.Data.GetDataPresent("ShelfItemMove"))
            {
                var sourceItem = e.Data.GetData("ShelfItemMove") as ShelfItemViewModel;
                var sourceShelf = e.Data.GetData("SourceShelf") as ShelfViewModelBase;

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

            // External File Drop
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
                    // Check Edit Mode
                    // Note: IsEditMode is a property of the control/UserControl
                    if (!IsEditMode)
                    {
                        return;
                    }

                    if (DataContext is ShelfViewModelBase shelfVm)
                    {
                        // スマートシェルフ（DirectoryPathが設定されている）は並び替え不可（OSファイルシステム順序依存のため）
                        // ただし、もしスマートシェルでもカスタムソートを許容するならここを変更。
                        // 今回の要件「スマートシェルは非対象」に従い、ディレクトリパスがある場合は禁止
                        if (!string.IsNullOrEmpty(shelfVm.DirectoryPath))
                        {
                            // スマートシェルの場合はドラッグ自体を開始しない（または並び替えデータを付与しない）
                            // 外部へのドラッグ（コピー/移動）は許可したいか？
                            // 一旦「並び替え」は禁止するが、外部ドロップは許可するデータを作成する
                        }

                        // ソートモードがNoneでないと手動並び替えは無意味（即座に再ソートされるため）
                        if (shelfVm.SortOption != DesktopOrganizer.Core.Models.ShelfSortOption.None)
                        {
                            return;
                        }

                        // ドラッグ開始
                        var data = new System.Windows.DataObject();

                        // スマートシェルでない場合のみReorderデータをセット
                        if (string.IsNullOrEmpty(shelfVm.DirectoryPath))
                        {
                            data.SetData("ShelfItemReorder", itemVm);
                        }

                        data.SetData("ShelfItemMove", itemVm);    // 棚間移動用
                        data.SetData("SourceShelf", DataContext); // 元の棚
                        var result = System.Windows.DragDrop.DoDragDrop(listBoxItem, data, System.Windows.DragDropEffects.Move);
                        e.Handled = true;
                    }
                }
            }
        }
    }

    private void RemoveAdorner()
    {
        if (_insertionAdorner != null && _itemAdornerLayer != null)
        {
            _itemAdornerLayer.Remove(_insertionAdorner);
            _insertionAdorner = null;
        }
        _targetInsertionIndex = -1;
    }

    private void ItemsListBox_DragLeave(object sender, DragEventArgs e)
    {
        RemoveAdorner();
    }

    private void ItemsListBox_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.None;

        if (!e.Data.GetDataPresent("ShelfItemReorder") && !e.Data.GetDataPresent("ShelfItemMove"))
        {
            return;
        }

        e.Effects = DragDropEffects.Move;

        // Find the item under the mouse
        var listBox = sender as System.Windows.Controls.ListBox;
        if (listBox == null) return;

        var pos = e.GetPosition(listBox);
        var result = VisualTreeHelper.HitTest(listBox, pos);
        if (result == null) return;

        // Find ListBoxItem
        var visual = result.VisualHit;
        ListBoxItem? targetItem = null;
        while (visual != null && visual != listBox)
        {
            if (visual is ListBoxItem item)
            {
                targetItem = item;
                break;
            }
            visual = VisualTreeHelper.GetParent(visual);
        }

        if (_itemAdornerLayer == null)
        {
            _itemAdornerLayer = AdornerLayer.GetAdornerLayer(listBox);
        }

        if (targetItem != null && targetItem.DataContext is ShelfItemViewModel targetVm && listBox.ItemsSource is System.Collections.IList items)
        {
            int index = items.IndexOf(targetVm);

            // Determine before/after
            var itemPos = e.GetPosition(targetItem);
            bool isAfter = itemPos.X > targetItem.ActualWidth / 2;

            _targetInsertionIndex = isAfter ? index + 1 : index;

            // Draw Adorner
            if (_itemAdornerLayer != null)
            {
                if (_insertionAdorner == null)
                {
                    _insertionAdorner = new InsertionAdorner(listBox, new Point(), new Point());
                    _itemAdornerLayer.Add(_insertionAdorner);
                }

                // Calculate line coordinates relative to ListBox
                var transform = targetItem.TransformToAncestor(listBox);
                var itemOrigin = transform.Transform(new Point(0, 0));

                double x = itemOrigin.X + (isAfter ? targetItem.ActualWidth : 0);
                double yTop = itemOrigin.Y;
                double yBottom = itemOrigin.Y + targetItem.ActualHeight;

                _insertionAdorner.UpdatePosition(new Point(x, yTop), new Point(x, yBottom));
            }
        }
        else
        {
            // Hovering empty space - append to end?
            // For now, remove adorner if not over item
            RemoveAdorner();
            _targetInsertionIndex = (listBox.ItemsSource as System.Collections.IList)?.Count ?? 0;
        }
    }

    private void ItemsListBox_Drop(object sender, DragEventArgs e)
    {
        RemoveAdorner();

        if (DataContext is ShelfViewModelBase targetShelf)
        {
            // Internal Reorder
            if (e.Data.GetDataPresent("ShelfItemReorder"))
            {
                var sourceItem = e.Data.GetData("ShelfItemReorder") as ShelfItemViewModel;
                if (sourceItem != null && _targetInsertionIndex != -1)
                {
                    targetShelf.MoveItem(sourceItem, _targetInsertionIndex);
                    e.Handled = true;
                }
            }
            // Move from another Shelf
            else if (e.Data.GetDataPresent("ShelfItemMove"))
            {
                var sourceItem = e.Data.GetData("ShelfItemMove") as ShelfItemViewModel;
                var sourceShelf = e.Data.GetData("SourceShelf") as ShelfViewModelBase;

                if (sourceItem != null && sourceShelf != null && sourceShelf != targetShelf)
                {
                    if (!string.IsNullOrEmpty(targetShelf.DirectoryPath)) return; // No drop on Smart Folder

                    targetShelf.AcceptItem(sourceItem);
                    sourceShelf.RemoveItem(sourceItem);

                    // Move to specific index if we have one
                    if (_targetInsertionIndex != -1)
                    {
                        var newItem = targetShelf.Items.LastOrDefault();
                        if (newItem != null)
                        {
                            int count = targetShelf.Items.Count;
                            int finalIndex = Math.Min(_targetInsertionIndex, count - 1);
                            targetShelf.MoveItem(newItem, finalIndex);
                        }
                    }
                    e.Handled = true;
                }
            }
            // External File Drop
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                DesktopOrganizer.Core.Utilities.Logger.Log($"[ItemsListBox_Drop] FileDrop detected");
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                DesktopOrganizer.Core.Utilities.Logger.Log($"[ItemsListBox_Drop] Files count: {files?.Length ?? 0}");
                if (files != null)
                {
                    foreach (string file in files)
                    {
                        DesktopOrganizer.Core.Utilities.Logger.Log($"[ItemsListBox_Drop] Processing file: {file}");
                        targetShelf.AddFile(file);
                    }
                }
                e.Handled = true;
            }
        }
    }

    private void MenuItem_RenameShelf_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ShelfViewModelBase vm)
        {
            vm.RequestRename();
        }
    }

    private void MenuItem_DeleteShelf_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ShelfViewModelBase vm)
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
        if (sender is MenuItem menuItem && menuItem.Tag is string colorCode && DataContext is ShelfViewModelBase vm)
        {
            vm.ThemeColor = colorCode;
        }
    }

    private void MenuItem_DisplayMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string modeTag && DataContext is ShelfViewModelBase vm)
        {
            if (Enum.TryParse<Core.Models.ShelfDisplayMode>(modeTag, out var mode))
            {
                vm.DisplayMode = mode;
            }
        }
    }

    private void MenuItem_CustomColor_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ShelfViewModelBase vm)
        {
            using var colorDialog = new System.Windows.Forms.ColorDialog();
            colorDialog.FullOpen = true; // フルカラーパレットを表示
            colorDialog.AnyColor = true;

            // LayoutManagerからカスタムカラーを読み込む
            var layoutManager = ServiceContainer.GetService<ILayoutManager>();
            if (layoutManager?.CurrentLayout?.CustomColors != null && layoutManager.CurrentLayout.CustomColors.Length > 0)
            {
                colorDialog.CustomColors = layoutManager.CurrentLayout.CustomColors;
            }

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

                // カスタムカラーを保存
                if (layoutManager != null)
                {
                    layoutManager.CurrentLayout.CustomColors = colorDialog.CustomColors;
                    layoutManager.SaveLayout();
                }
            }
        }
    }

    private void MenuItem_SmartShelf_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ShelfViewModelBase vm)
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

    private void MenuItem_FilterSettings_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.ShelfViewModelBase vm)
        {
            var dialog = new FilterSettingsDialog(vm.FilterPattern ?? "");
            if (dialog.ShowDialog() == true)
            {
                vm.FilterPattern = dialog.ResultPattern;
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
            ShelfViewModelBase.IsGridSnapEnabled = menuItem.IsChecked;
        }
    }

    private void MenuItem_SnapToGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            menuItem.IsChecked = ShelfViewModelBase.IsGridSnapEnabled;
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

    private void AddMemo_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MemoShelfViewModel memoVm)
        {
            memoVm.AddMemo();
        }
    }

    /// <summary>
    /// 並べ替えメニュー項目がクリックされた時の処理
    /// </summary>
    private void MenuItem_Sort_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string sortTag && DataContext is ShelfViewModelBase vm)
        {
            if (Enum.TryParse<DesktopOrganizer.Core.Models.ShelfSortOption>(sortTag, out var option))
            {
                vm.SortOption = option;
            }
        }
    }

    private void MenuItem_IconSize_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && double.TryParse(menuItem.Tag?.ToString(), out double size) && DataContext is ShelfViewModelBase vm)
        {
            vm.IconSize = size;
        }
    }

    private void ResizeHandle_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (DataContext is ShelfViewModelBase vm)
        {
            double newWidth = vm.Width + e.HorizontalChange;
            double newHeight = vm.Height + e.VerticalChange;

            // Minimum Constraints
            newWidth = Math.Max(newWidth, 100);
            newHeight = Math.Max(newHeight, 100);

            if (ShelfViewModelBase.IsGridSnapEnabled)
            {
                double gs = ShelfViewModelBase.GridSize;
                newWidth = Math.Round(newWidth / gs) * gs;
                newHeight = Math.Round(newHeight / gs) * gs;
            }

            vm.Width = newWidth;
            vm.Height = newHeight;
        }
    }

    private void ResizeHandle_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (DataContext is ShelfViewModelBase vm)
        {
            // リサイズ完了時にサイズを保存
            vm.OnMoved();
        }
    }
}

/// <summary>
/// DataContextがClockShelfViewModelかどうかを判定するコンバーター
/// </summary>
public class IsClockShelfConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is ClockShelfViewModel;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// null または空文字列の場合にCollapsedを返すコンバーター
/// </summary>
public class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || (value is string s && string.IsNullOrEmpty(s)))
            return Visibility.Collapsed;
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
