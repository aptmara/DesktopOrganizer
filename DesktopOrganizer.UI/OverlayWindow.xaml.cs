using System.Windows;
using System.Windows.Interop;
using DesktopOrganizer.Core.Interop;

namespace DesktopOrganizer.UI;

public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateWindowStyle(isEditMode: false);
    }

    private System.Windows.Point _lastRightClickPosition;

    protected override void OnMouseRightButtonUp(System.Windows.Input.MouseButtonEventArgs e)
    {
        _lastRightClickPosition = e.GetPosition(this);
        base.OnMouseRightButtonUp(e);
    }

    public event EventHandler? RequestExitEditMode;

    public void SetEditMode(bool isEditMode)
    {
        // 編集モード時は視覚的なフィードバックのみ変更（背景色）
        // 入力制限はViewModel/Control側で行う
        Background = isEditMode ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x20, 0x00, 0x00, 0x00)) : System.Windows.Media.Brushes.Transparent;

        UpdateWindowStyle(isEditMode);

        if (isEditMode)
        {
            this.Activate();
            this.Focus();
        }
    }

    private void UpdateWindowStyle(bool isEditMode)
    {
        var helper = new WindowInteropHelper(this);
        var exStyle = (int)NativeMethods.GetWindowLongPtr(helper.Handle, NativeMethods.GWL_EXSTYLE);

        // 基本スタイル: ツールウィンドウ
        int baseFlags = NativeMethods.WS_EX_TOOLWINDOW;

        if (isEditMode)
        {
            // Edit Mode: アクティブ化可能にする (NOACTIVATEを外す)
            exStyle = (exStyle & ~NativeMethods.WS_EX_NOACTIVATE);
            exStyle = (exStyle & ~NativeMethods.WS_EX_TRANSPARENT); // 念のため
            exStyle |= baseFlags;
        }
        else
        {
            // View Mode: アクティブ化しない
            exStyle |= baseFlags | NativeMethods.WS_EX_NOACTIVATE;
            // WS_EX_TRANSPARENT は削除済みだが、Viewモードでもクリックを受け付けるため設定しない
            exStyle = (exStyle & ~NativeMethods.WS_EX_TRANSPARENT);
        }

        NativeMethods.SetWindowLongPtr(helper.Handle, NativeMethods.GWL_EXSTYLE, (IntPtr)exStyle);

        // Z-order制御: Viewモード時はデスクトップ直上に配置
        if (!isEditMode)
        {
            NativeMethods.SetWindowPos(
                helper.Handle,
                NativeMethods.HWND_BOTTOM,
                0, 0, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
        }
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            RequestExitEditMode?.Invoke(this, EventArgs.Empty);
        }
        base.OnKeyDown(e);
    }

    private void MenuItem_NewShelf_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.OverlayViewModel vm)
        {
            // Use captured position if available (from ContextMenu invoke)
            System.Windows.Point targetPoint = _lastRightClickPosition;

            // If sender is Button (FAB), use current mouse position or button location
            if (sender is System.Windows.Controls.Button)
            {
                targetPoint = System.Windows.Input.Mouse.GetPosition(this);
            }
            // Fallback
            else if (targetPoint.X == 0 && targetPoint.Y == 0)
            {
                targetPoint = System.Windows.Input.Mouse.GetPosition(this);
            }

            // Determine Shelf Type from Tag
            DesktopOrganizer.Core.Models.ShelfType shelfType = DesktopOrganizer.Core.Models.ShelfType.Manual;
            if (sender is System.Windows.Controls.MenuItem menuItem && menuItem.Tag is string tagStr)
            {
                if (Enum.TryParse(tagStr, out DesktopOrganizer.Core.Models.ShelfType parsedType))
                {
                    shelfType = parsedType;
                }
            }

            if (shelfType == DesktopOrganizer.Core.Models.ShelfType.Manual)
            {
                vm.RequestCreateShelf(targetPoint);
            }
            else
            {
                vm.RequestCreateTypedShelf(targetPoint, shelfType);
            }
        }
    }

    private void MenuItem_ToggleEditMode_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.OverlayViewModel vm)
        {
            vm.RequestToggleEditMode();
        }
    }

    private void Grid_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // 編集モード時にシェルフ外をクリックしたら編集モードを終了する
        if (DataContext is ViewModels.OverlayViewModel vm && vm.IsEditMode)
        {
            RequestExitEditMode?.Invoke(this, EventArgs.Empty);
        }
    }

    private void MenuItem_ResetAll_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            "すべてのシェルと設定をリセットしますか？\nこの操作は元に戻せません。",
            "全リセットの確認",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            if (DataContext is ViewModels.OverlayViewModel vm)
            {
                vm.RequestResetAll();
            }
        }
    }
}
