using System.Windows;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;
using TextBox = System.Windows.Controls.TextBox;

namespace DesktopOrganizer.UI.Behaviors;

/// <summary>
/// インライン名前変更の共通ロジックを提供するBehavior。
/// 機能:
/// - Loaded時に元の値をバックアップ & フォーカス & 全選択
/// - LostFocus時にVOIDチェック（空なら復元）
/// - Enter: 確定、Escape: キャンセル
/// - 完了後にOnCompleteCommandを実行
/// </summary>
public class InlineRenameBehavior : Behavior<TextBox>
{
    private string? _backupValue;

    /// <summary>
    /// 確定/キャンセル時に実行するコマンド。
    /// </summary>
    public static readonly DependencyProperty OnCompleteCommandProperty =
        DependencyProperty.Register(
            nameof(OnCompleteCommand),
            typeof(ICommand),
            typeof(InlineRenameBehavior),
            new PropertyMetadata(null));

    public ICommand? OnCompleteCommand
    {
        get => (ICommand?)GetValue(OnCompleteCommandProperty);
        set => SetValue(OnCompleteCommandProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.Loaded += OnLoaded;
        AssociatedObject.LostFocus += OnLostFocus;
        AssociatedObject.KeyDown += OnKeyDown;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Loaded -= OnLoaded;
        AssociatedObject.LostFocus -= OnLostFocus;
        AssociatedObject.KeyDown -= OnKeyDown;
        base.OnDetaching();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 元の値をバックアップ
        _backupValue = AssociatedObject.Text;

        // フォーカス & 全選択
        AssociatedObject.Focus();
        AssociatedObject.SelectAll();
    }

    private void OnLostFocus(object sender, RoutedEventArgs e)
    {
        Complete(isCancelled: false);
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Complete(isCancelled: false);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            // キャンセル: 元の値に戻す
            if (!string.IsNullOrEmpty(_backupValue))
            {
                AssociatedObject.Text = _backupValue;
            }
            Complete(isCancelled: true);
            e.Handled = true;
        }
    }

    /// <summary>
    /// 名前変更を完了する。
    /// VOIDチェック: 空文字の場合は元の値に復元。
    /// </summary>
    private void Complete(bool isCancelled)
    {
        // VOIDチェック: 空文字の場合は元の値に戻す
        if (!isCancelled && string.IsNullOrWhiteSpace(AssociatedObject.Text))
        {
            if (!string.IsNullOrEmpty(_backupValue))
            {
                AssociatedObject.Text = _backupValue;
            }
        }

        // コマンド実行
        OnCompleteCommand?.Execute(null);

        _backupValue = null;
    }
}
