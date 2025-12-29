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

    public void SetEditMode(bool isEditMode)
    {
        UpdateWindowStyle(isEditMode);
        // 編集モード時は視覚的なフィードバックを与える（背景色変更など）
        Background = isEditMode ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x40, 0x00, 0x00, 0xFF)) : System.Windows.Media.Brushes.Transparent;
    }

    private void UpdateWindowStyle(bool isEditMode)
    {
        var helper = new WindowInteropHelper(this);
        var exStyle = (int)NativeMethods.GetWindowLongPtr(helper.Handle, NativeMethods.GWL_EXSTYLE);

        // 常にツールウィンドウ・アクティブ化不可
        int persistentFlags = NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;

        if (isEditMode)
        {
            // 編集モード: 透明化フラグを削除
            exStyle = (exStyle & ~NativeMethods.WS_EX_TRANSPARENT) | persistentFlags;
        }
        else
        {
            // 表示モード: 透明化フラグを追加
            exStyle |= persistentFlags | NativeMethods.WS_EX_TRANSPARENT;
        }

        NativeMethods.SetWindowLongPtr(helper.Handle, NativeMethods.GWL_EXSTYLE, (IntPtr)exStyle);
    }
}
