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

    /// <summary>
    /// 保存されているプロファイル名のリストを取得
    /// </summary>
    List<string> GetProfileNames();

    /// <summary>
    /// 現在のレイアウトを名前付きプロファイルとして保存
    /// </summary>
    void SaveProfileAs(string name);

    /// <summary>
    /// 指定されたプロファイルを読み込む
    /// </summary>
    void LoadProfile(string name);

    /// <summary>
    /// 指定されたプロファイルを削除
    /// </summary>
    void DeleteProfile(string name);

    /// <summary>
    /// 現在のレイアウトを指定されたパスにエクスポート
    /// </summary>
    void ExportLayout(string filePath);

    /// <summary>
    /// 指定されたパスからレイアウトをインポート
    /// </summary>
    void ImportLayout(string filePath);
}
