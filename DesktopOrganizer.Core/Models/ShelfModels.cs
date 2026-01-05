using System.Text.Json.Serialization;
using DesktopOrganizer.Core.Interop;

namespace DesktopOrganizer.Core.Models;

/// <summary>
/// 棚の種類を定義する列挙型
/// </summary>
public enum ShelfType
{
    /// <summary>手動でアイテムを追加する通常の棚</summary>
    Manual,
    /// <summary>フォルダと同期するスマートシェルフ</summary>
    SmartFolder,
    /// <summary>最近使ったファイルを自動表示する棚</summary>
    Recents,
    /// <summary>一定時間後に自動削除される一時保管棚</summary>
    Temp,
    /// <summary>テキストメモを保持するクイックメモ棚</summary>
    Memo,
    /// <summary>時計ウィジェット</summary>
    Clock,
    /// <summary>アナログ時計ウィジェット</summary>
    AnalogClock
}

public enum ShelfItemType
{
    Shortcut,
    Executable,
    Folder,
    Url,
    File,
    Memo
}

public class ShelfItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public ShelfItemType Type { get; set; } = ShelfItemType.File;
    public string OriginalIconPath { get; set; } = string.Empty; // 必要に応じてアイコンを抽出するためのパス

    /// <summary>
    /// 一時保管棚用：アイテムの有効期限（この時刻を過ぎると自動削除）
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// メモ棚用：メモの本文テキスト
    /// </summary>
    public string? MemoContent { get; set; }

    [JsonIgnore]
    public bool IsBroken { get; set; } // 実行時状態
}

public class Shelf
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "New Shelf";

    /// <summary>
    /// 棚の種類（デフォルトは手動）
    /// </summary>
    public ShelfType Type { get; set; } = ShelfType.Manual;

    // モニターワークエリアに対する正規化座標 (0.0 - 1.0)
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 0.2;
    public double Height { get; set; } = 0.3;

    // Phase 3: Smart Shelf
    public string? DirectoryPath { get; set; }

    // Phase 3: Theming
    public string ThemeColor { get; set; } = "#CC1E1E24";

    // Phase 2: Smart Rules
    public string? FilterPattern { get; set; } // e.g. "*.jpg;*.png"

    public string TargetMonitorDeviceId { get; set; } = string.Empty;

    public int ZIndex { get; set; } = 0;

    // Phase 4: Roll-up Shelf
    public bool IsCollapsed { get; set; } = false;

    // Phase 4: Smart Sorting
    public ShelfSortOption SortOption { get; set; } = ShelfSortOption.None;

    // Phase 4: Ghost Mode
    public bool IsGhostModeEnabled { get; set; } = false;

    // Per-Shelf Feature Toggles
    public bool IsSearchEnabled { get; set; } = false;

    // Display Mode
    public ShelfDisplayMode DisplayMode { get; set; } = ShelfDisplayMode.Grid;

    // Appearance
    public double IconSize { get; set; } = 48.0;

    public List<ShelfItem> Items { get; set; } = new();
}

/// <summary>
/// シェルの表示モード
/// </summary>
public enum ShelfDisplayMode
{
    /// <summary>アイコングリッド表示</summary>
    Grid,
    /// <summary>リスト（詳細）表示</summary>
    List,
    /// <summary>コンパクト（小アイコン）表示</summary>
    Compact
}

public enum ShelfSortOption
{
    None,
    Name,
    DateModified,
    Type
}

public class LayoutData
{
    public List<Shelf> Shelves { get; set; } = new();

    /// <summary>
    /// 保存されたテーマ設定（Dark/Light）
    /// </summary>
    public string Theme { get; set; } = "Dark";

    /// <summary>
    /// カスタムパレットの色設定 (WinForms Color Dialog CustomColors format)
    /// </summary>
    public int[] CustomColors { get; set; } = new int[0];
}
