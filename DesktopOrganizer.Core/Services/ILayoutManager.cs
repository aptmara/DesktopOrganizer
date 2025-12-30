using DesktopOrganizer.Core.Interop;
using DesktopOrganizer.Core.Models;

namespace DesktopOrganizer.Core.Services;

/// <summary>
/// レイアウト管理のインターフェース。
/// シェルフの位置・サイズのロード/保存/モニター間計算を抽象化。
/// </summary>
public interface ILayoutManager
{
    /// <summary>
    /// 現在のレイアウトデータ
    /// </summary>
    LayoutData CurrentLayout { get; }

    /// <summary>
    /// レイアウトファイルからデータをロードする
    /// </summary>
    void LoadLayout();

    /// <summary>
    /// 現在のレイアウトデータをファイルに保存する
    /// </summary>
    void SaveLayout();

    /// <summary>
    /// レイアウトをリセットし、ファイルを削除する
    /// </summary>
    void ResetLayout();

    /// <summary>
    /// シェルフの物理ピクセル矩形を計算する
    /// </summary>
    NativeMethods.RECT CalculatePhysicalRect(Shelf shelf, MonitorItem monitor);

    /// <summary>
    /// シェルフに最適なモニターを見つける
    /// </summary>
    MonitorItem FindBestMonitor(Shelf shelf, List<MonitorItem> monitors);

    /// <summary>
    /// シェルフの位置を更新し、正規化座標を計算する
    /// </summary>
    void UpdateShelfPosition(Shelf shelf, NativeMethods.RECT currentRect, MonitorItem monitor);
}
