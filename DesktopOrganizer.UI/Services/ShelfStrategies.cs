using System.IO;
using DesktopOrganizer.Core.Models;
using DesktopOrganizer.UI.ViewModels;

namespace DesktopOrganizer.UI.Services;

/// <summary>
/// シェルフ初期化戦略のインターフェース
/// </summary>
public interface IShelfInitializationStrategy
{
    /// <summary>
    /// シェルフを初期化する
    /// </summary>
    void Initialize(ShelfViewModelBase viewModel, Shelf model, Action? saveLayoutAction);

    /// <summary>
    /// 対象のシェルフタイプかどうか
    /// </summary>
    bool CanHandle(ShelfType type);
}

/// <summary>
/// 手動シェルフの初期化戦略
/// </summary>
public class ManualShelfStrategy : IShelfInitializationStrategy
{
    public bool CanHandle(ShelfType type) => type == ShelfType.Manual;

    public void Initialize(ShelfViewModelBase viewModel, Shelf model, Action? saveLayoutAction)
    {
        DesktopOrganizer.Core.Utilities.Logger.Log($"Initializing Manual Shelf: {model.Title}");
        foreach (var item in model.Items)
        {
            viewModel.AddItemFromModel(item, saveLayoutAction);
        }
    }
}

/// <summary>
/// スマートフォルダシェルフの初期化戦略
/// </summary>
public class SmartFolderStrategy : IShelfInitializationStrategy
{
    public bool CanHandle(ShelfType type) => type == ShelfType.SmartFolder;

    public void Initialize(ShelfViewModelBase viewModel, Shelf model, Action? saveLayoutAction)
    {
        DesktopOrganizer.Core.Utilities.Logger.Log($"Initializing Smart Folder: {model.Title}");
        // SmartShelfの初期化はviewModel内で完結（FileSystemWatcherの管理が必要なため）
        // ここではフラグのみ設定
    }
}

/// <summary>
/// シェルフ初期化戦略のファクトリ/レジストリ
/// </summary>
public static class ShelfStrategyRegistry
{
    private static readonly List<IShelfInitializationStrategy> _strategies = new()
    {
        new ManualShelfStrategy(),
        new SmartFolderStrategy()
    };

    /// <summary>
    /// シェルフタイプに対応する戦略を取得
    /// </summary>
    public static IShelfInitializationStrategy? GetStrategy(ShelfType type)
    {
        return _strategies.FirstOrDefault(s => s.CanHandle(type));
    }

    /// <summary>
    /// カスタム戦略を追加
    /// </summary>
    public static void RegisterStrategy(IShelfInitializationStrategy strategy)
    {
        _strategies.Add(strategy);
    }
}
