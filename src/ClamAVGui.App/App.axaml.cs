using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ClamAVGui.App.Services;
using ClamAVGui.App.ViewModels;
using ClamAVGui.App.Views;
using ClamAVGui.Core.Configuration;
using ClamAVGui.Core.History;
using ClamAVGui.Core.Interfaces;
using ClamAVGui.Core.Models;
using ClamAVGui.Core.Parsing;
using ClamAVGui.Core.Protocol;
using ClamAVGui.Core.Quarantine;
using ClamAVGui.Core.Scanning;
using ClamAVGui.Core.Services;
using ClamAVGui.Platform.Interfaces;
using ClamAVGui.Platform.Linux;
using ClamAVGui.Platform.MacOS;
using ClamAVGui.Platform.Services;
using ClamAVGui.Platform.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClamAVGui.App;

public partial class App : Application
{
    public IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Services = serviceCollection.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = Services.GetRequiredService<MainViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm
            };

            desktop.MainWindow.Opened += async (_, _) =>
            {
                await mainVm.InitializeAsync();

                if (desktop.Args != null && desktop.Args.Length > 1 &&
                    (string.Equals(desktop.Args[0], "-scan", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(desktop.Args[0], "--scan", StringComparison.OrdinalIgnoreCase)))
                {
                    var targetPath = desktop.Args[1];
                    mainVm.SelectedTabIndex = 1;
                    var isDir = Directory.Exists(targetPath);
                    await mainVm.Scan.StartScanAsync(targetPath, isDir ? ScanTargetType.Directory : ScanTargetType.File);
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder => builder.AddConsole());

        // Core parsers and process runner
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IClamAvOutputParser, ClamAvOutputParser>();
        services.AddSingleton<IClamAvConfigParser, ClamAvConfigParser>();

        // Platform services registration
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddSingleton<IPlatformService, WindowsPlatformService>();
            services.AddSingleton<IClamAvBinaryLocator, WindowsClamAvBinaryLocator>();
            services.AddSingleton<IStartupService, WindowsStartupService>();
            services.AddSingleton<ISchedulerService, WindowsSchedulerService>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            services.AddSingleton<IPlatformService, MacOsPlatformService>();
            services.AddSingleton<IClamAvBinaryLocator, MacOsClamAvBinaryLocator>();
            services.AddSingleton<IStartupService, MacOsStartupService>();
            services.AddSingleton<ISchedulerService, MacOsSchedulerService>();
        }
        else
        {
            services.AddSingleton<IPlatformService, LinuxPlatformService>();
            services.AddSingleton<IClamAvBinaryLocator, LinuxClamAvBinaryLocator>();
            services.AddSingleton<IStartupService, LinuxStartupService>();
            services.AddSingleton<ISchedulerService, LinuxSchedulerService>();
        }

        services.AddSingleton<IFileSystemMonitor, FileSystemMonitor>();
        services.AddSingleton<IFileDialogService, AvaloniaFileDialogService>();
        services.AddSingleton<INotificationService, AvaloniaNotificationService>();
        services.AddSingleton<ITrayService, AvaloniaTrayService>();

        // Core data storage services
        services.AddSingleton<ISettingsService>(sp =>
        {
            var platform = sp.GetRequiredService<IPlatformService>();
            return new SettingsService(platform.UserDataDirectory);
        });

        services.AddSingleton<IHistoryService>(sp =>
        {
            var platform = sp.GetRequiredService<IPlatformService>();
            return new HistoryService(platform.UserDataDirectory);
        });

        services.AddSingleton<IQuarantineService>(sp =>
        {
            var platform = sp.GetRequiredService<IPlatformService>();
            var settings = sp.GetRequiredService<ISettingsService>().LoadSettingsAsync().GetAwaiter().GetResult();
            var dir = settings.CustomQuarantinePath ?? Path.Combine(platform.UserDataDirectory, "Quarantine");
            return new QuarantineService(dir);
        });

        // Protocol and transports
        services.AddSingleton<IClamdTransportFactory, ClamdTransportFactory>();
        services.AddSingleton<IClamdProtocol, ClamdProtocolClient>();

        // Configuration provider
        services.AddSingleton<IClamAvConfigurationProvider, ClamAvConfigurationProvider>();

        // Daemon manager
        services.AddSingleton<IClamAvDaemon>(sp =>
        {
            var protocol = sp.GetRequiredService<IClamdProtocol>();
            var binaryLocator = sp.GetRequiredService<IClamAvBinaryLocator>();
            var settingsService = sp.GetRequiredService<ISettingsService>();
            var configProvider = sp.GetRequiredService<IClamAvConfigurationProvider>();
            var platform = sp.GetRequiredService<IPlatformService>();

            return new ClamAvDaemonManager(
                protocol,
                async () =>
                {
                    var s = await settingsService.LoadSettingsAsync();
                    return await binaryLocator.FindInstallationAsync(s.CustomClamAvPath);
                },
                () =>
                {
                    var s = settingsService.LoadSettingsAsync().GetAwaiter().GetResult();
                    var inst = binaryLocator.FindInstallationAsync(s.CustomClamAvPath).GetAwaiter().GetResult();
                    var cfg = configProvider.LoadAsync(inst).GetAwaiter().GetResult();
                    return cfg.DaemonEndpoint ?? (platform.Platform == PlatformKind.Windows
                        ? new TcpClamdEndpoint("127.0.0.1", 3310)
                        : new UnixClamdEndpoint(Path.Combine(platform.RuntimeDirectory, "clamd.ctl")));
                });
        });

        // Scanners & Backends
        services.AddSingleton<IScanBackend>(sp =>
        {
            var runner = sp.GetRequiredService<IProcessRunner>();
            var parser = sp.GetRequiredService<IClamAvOutputParser>();
            var locator = sp.GetRequiredService<IClamAvBinaryLocator>();
            var settings = sp.GetRequiredService<ISettingsService>();
            return new ClamScanBackend(runner, parser, async () =>
            {
                var s = await settings.LoadSettingsAsync();
                return await locator.FindInstallationAsync(s.CustomClamAvPath);
            });
        });

        services.AddSingleton<IScanBackend>(sp =>
        {
            var runner = sp.GetRequiredService<IProcessRunner>();
            var parser = sp.GetRequiredService<IClamAvOutputParser>();
            var locator = sp.GetRequiredService<IClamAvBinaryLocator>();
            var settings = sp.GetRequiredService<ISettingsService>();
            return new ClamdScanBackend(runner, parser, async () =>
            {
                var s = await settings.LoadSettingsAsync();
                return await locator.FindInstallationAsync(s.CustomClamAvPath);
            });
        });

        services.AddSingleton<IScanBackend>(sp =>
        {
            var protocol = sp.GetRequiredService<IClamdProtocol>();
            var configProvider = sp.GetRequiredService<IClamAvConfigurationProvider>();
            var locator = sp.GetRequiredService<IClamAvBinaryLocator>();
            var settings = sp.GetRequiredService<ISettingsService>();
            var parser = sp.GetRequiredService<IClamAvOutputParser>();
            return new ClamdBackend(protocol, () =>
            {
                var s = settings.LoadSettingsAsync().GetAwaiter().GetResult();
                var inst = locator.FindInstallationAsync(s.CustomClamAvPath).GetAwaiter().GetResult();
                var cfg = configProvider.LoadAsync(inst).GetAwaiter().GetResult();
                return cfg.DaemonEndpoint;
            }, parser);
        });

        services.AddSingleton<IClamAvScanner, ClamAvScanner>();
        services.AddSingleton<IScanCoordinator, ScanCoordinator>();

        // Updater
        services.AddSingleton<IClamAvUpdater>(sp =>
        {
            var runner = sp.GetRequiredService<IProcessRunner>();
            var parser = sp.GetRequiredService<IClamAvOutputParser>();
            var locator = sp.GetRequiredService<IClamAvBinaryLocator>();
            var settings = sp.GetRequiredService<ISettingsService>();
            return new ClamAvUpdater(runner, parser, async () =>
            {
                var s = await settings.LoadSettingsAsync();
                return await locator.FindInstallationAsync(s.CustomClamAvPath);
            });
        });

        services.AddSingleton<IDiagnosticsService, DiagnosticsService>();

        // ViewModels
        services.AddSingleton<DashboardViewModel>(sp => new DashboardViewModel(
            sp.GetRequiredService<IClamAvBinaryLocator>(),
            sp.GetRequiredService<IClamAvDaemon>(),
            sp.GetRequiredService<IHistoryService>(),
            sp.GetRequiredService<ISettingsService>(),
            () => sp.GetRequiredService<MainViewModel>().SelectedTabIndex = 1,
            () => sp.GetRequiredService<MainViewModel>().SelectedTabIndex = 2
        ));

        services.AddSingleton<ScanViewModel>();
        services.AddSingleton<UpdateViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<QuarantineViewModel>();
        services.AddSingleton<DaemonViewModel>();
        services.AddSingleton<MonitoringViewModel>();
        services.AddSingleton<SchedulerViewModel>();
        services.AddSingleton<DiagnosticsViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();
    }
}
