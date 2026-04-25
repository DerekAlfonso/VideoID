using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Windows;
using VideoID.Core.Models;
using VideoID.Core.Processing;
using VideoID.Core.Services;
using VideoID.Core.Storage;
using VideoID.UI.ViewModels;

namespace VideoID.UI;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var collection = new ServiceCollection();
        ConfigureServices(collection);
        Services = collection.BuildServiceProvider();

        var window = Services.GetRequiredService<MainWindow>();
        window.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(b =>
        {
            b.AddConsole();
            b.SetMinimumLevel(LogLevel.Information);
        });

        // Singleton options — can be changed by user via Settings view
        services.AddSingleton<ProcessingOptions>();

        // Core services
        services.AddSingleton<VideoScanService>();
        services.AddSingleton<FaceClusterer>();
        services.AddSingleton(sp =>
            new FaceDatabase(sp.GetRequiredService<ProcessingOptions>().DatabasePath));
        services.AddSingleton<VideoTagWriter>();
        services.AddSingleton<VideoProcessingPipeline>(sp => new VideoProcessingPipeline(
            sp.GetRequiredService<ProcessingOptions>(),
            sp.GetRequiredService<FaceClusterer>(),
            sp.GetRequiredService<FaceDatabase>(),
            sp.GetRequiredService<VideoTagWriter>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<VideoProcessingPipeline>>()));

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (Services is IDisposable d) d.Dispose();
        base.OnExit(e);
    }
}
