using System.IO;
using System.Text;
using DesktopOrganizer.Core.Interop;

namespace DesktopOrganizer.UI.Utilities;

public static class ShortcutHelper
{
    private const int SLGP_UNCPRIORITY = 0x0002;
    private const int STGM_READ = 0x0000;

    /// <summary>
    /// ショートカットを解決し、ターゲットパスを返します。
    /// 引数がある場合や解決に失敗した場合はnullを返します。
    /// </summary>
    public static string? ResolveShortcut(string shortcutPath)
    {
        if (!System.IO.File.Exists(shortcutPath)) return null;

        try
        {
            var shellLink = (IShellLinkW)new ShellLink();
            var persistFile = (IPersistFile)shellLink;

            persistFile.Load(shortcutPath, STGM_READ);

            // 引数のチェック
            var argsBuilder = new StringBuilder(260);
            shellLink.GetArguments(argsBuilder, argsBuilder.Capacity);
            if (!string.IsNullOrWhiteSpace(argsBuilder.ToString()))
            {
                // 引数がある場合は解決しない（ショートカットのまま使うべき）
                return null;
            }

            // ターゲットパスの取得
            var targetBuilder = new StringBuilder(260);
            shellLink.GetPath(targetBuilder, targetBuilder.Capacity, IntPtr.Zero, SLGP_UNCPRIORITY);
            var targetPath = targetBuilder.ToString();

            if (!string.IsNullOrWhiteSpace(targetPath) && (System.IO.File.Exists(targetPath) || Directory.Exists(targetPath)))
            {
                return targetPath;
            }
        }
        catch
        {
            // COMエラー等は無視
        }

        return null;
    }
}
