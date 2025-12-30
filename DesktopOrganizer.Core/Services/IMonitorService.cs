using DesktopOrganizer.Core.Models;

namespace DesktopOrganizer.Core.Services;

/// <summary>
/// モニター情報取得のインターフェース。
/// マルチモニター環境でのディスプレイ情報を抽象化。
/// </summary>
public interface IMonitorService
{
    /// <summary>
    /// 現在接続されているすべてのモニター情報を取得する
    /// </summary>
    List<MonitorItem> GetMonitors();
}
