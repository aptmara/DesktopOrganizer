using System.Text.Json.Serialization;
using DesktopOrganizer.Core.Interop;

namespace DesktopOrganizer.Core.Models;

public enum ShelfItemType
{
    Shortcut,
    Executable,
    Folder,
    Url,
    File
}

public class ShelfItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public ShelfItemType Type { get; set; } = ShelfItemType.File;
    public string OriginalIconPath { get; set; } = string.Empty; // 必要に応じてアイコンを抽出するためのパス

    [JsonIgnore]
    public bool IsBroken { get; set; } // 実行時状態
}

public class Shelf
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "New Shelf";

    // モニターワークエリアに対する正規化座標 (0.0 - 1.0)
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 0.2;
    public double Height { get; set; } = 0.3;

    public string TargetMonitorDeviceId { get; set; } = string.Empty;

    public List<ShelfItem> Items { get; set; } = new();
}

public class LayoutData
{
    public List<Shelf> Shelves { get; set; } = new();
}
