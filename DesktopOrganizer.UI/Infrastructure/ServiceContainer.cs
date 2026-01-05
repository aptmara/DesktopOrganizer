using Microsoft.Extensions.DependencyInjection;
using DesktopOrganizer.Core.Services;
using DesktopOrganizer.UI.Services;

namespace DesktopOrganizer.UI.Infrastructure;

/// <summary>
/// アプリケーションのDIコンテナ。
/// サービス登録とサービス取得を担当。
/// </summary>
public static class ServiceContainer
{
    private static IServiceProvider? _provider;

    /// <summary>
    /// サービスプロバイダー
    /// </summary>
    public static IServiceProvider Provider => _provider
        ?? throw new InvalidOperationException("ServiceContainer not initialized. Call Initialize() first.");

    /// <summary>
    /// DIコンテナを初期化する
    /// </summary>
    public static void Initialize()
    {
        var services = new ServiceCollection();

        // Core Services (Singleton)
        services.AddSingleton<ILayoutManager, LayoutManager>();
        services.AddSingleton<IMonitorService, MonitorService>();

        // UI Services

        services.AddTransient<ShelfViewModelFactory>();
        services.AddSingleton<InputService>();
        services.AddSingleton<TaskTrayIcon>(sp => new TaskTrayIcon(sp.GetRequiredService<ILayoutManager>()));

        _provider = services.BuildServiceProvider();
    }

    /// <summary>
    /// 指定された型のサービスを取得する
    /// </summary>
    public static T GetService<T>() where T : notnull
        => Provider.GetRequiredService<T>();

    /// <summary>
    /// 指定された型のサービスを取得する（nullable）
    /// </summary>
    public static T? GetServiceOrDefault<T>() where T : class
        => Provider.GetService<T>();
}
