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

    private void UpdateWindowStyle(bool isEditMode)
    {
        var helper = new WindowInteropHelper(this);
        var exStyle = (int)NativeMethods.GetWindowLongPtr(helper.Handle, NativeMethods.GWL_EXSTYLE);

        // 常にツールウィンドウ・アクティブ化不可
        // WS_EX_TRANSPARENT は設定しない（設定するとマウスイベントを一切受け取らなくなる）
        // WPFのAllowsTransparency=Trueにより、描画ピクセル以外はクリックスルーになるはず。
        int persistentFlags = NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;

        exStyle = (exStyle & ~NativeMethods.WS_EX_TRANSPARENT) | persistentFlags;

        NativeMethods.SetWindowLongPtr(helper.Handle, NativeMethods.GWL_EXSTYLE, (IntPtr)exStyle);
    }

    public void SetEditMode(bool isEditMode)
    {
        // 編集モード時は視覚的なフィードバックのみ変更（背景色）
        // 入力制限はViewModel/Control側で行う
        Background = isEditMode ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x20, 0x00, 0x00, 0x00)) : System.Windows.Media.Brushes.Transparent;
    }
}
