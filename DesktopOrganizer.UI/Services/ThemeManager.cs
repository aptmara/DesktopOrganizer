using System.Windows;
using System.Windows.Media;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;

namespace DesktopOrganizer.UI.Services;

/// <summary>
/// アプリケーションテーマを管理するサービス。
/// ライト/ダークモード切替とカスタムカラーパレットを提供。
/// </summary>
public static class ThemeManager
{
    /// <summary>
    /// 現在のテーマ
    /// </summary>
    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    /// <summary>
    /// テーマを適用する
    /// </summary>
    public static void ApplyTheme(AppTheme theme)
    {
        CurrentTheme = theme;

        var app = Application.Current;
        if (app == null) return;

        // テーマリソースを更新
        var resources = app.Resources;

        switch (theme)
        {
            case AppTheme.Light:
                resources["TextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                resources["TextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(80, 80, 80));
                resources["BorderBrush"] = new SolidColorBrush(Color.FromArgb(100, 200, 200, 200));
                resources["HoverOverlayBrush"] = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0));
                break;

            case AppTheme.Dark:
            default:
                resources["TextPrimaryBrush"] = new SolidColorBrush(Color.FromRgb(238, 238, 238)); // #EEEEEE
                resources["TextSecondaryBrush"] = new SolidColorBrush(Color.FromRgb(170, 170, 170)); // #AAAAAA
                resources["BorderBrush"] = new SolidColorBrush(Color.FromArgb(51, 255, 255, 255)); // #33FFFFFF
                resources["HoverOverlayBrush"] = new SolidColorBrush(Color.FromArgb(34, 255, 255, 255)); // #22FFFFFF
                break;
        }
    }

    /// <summary>
    /// テーマを切り替える
    /// </summary>
    public static void ToggleTheme()
    {
        ApplyTheme(CurrentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);
    }
}

/// <summary>
/// アプリケーションテーマ列挙
/// </summary>
public enum AppTheme
{
    Dark,
    Light
}
