using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using SukiUI.Dialogs;
using SukiUI.Toasts;
using System;
using System.Globalization;
using System.Linq;
using UotanToolbox.Common;
using UotanToolbox.Features;
using UotanToolbox.Services;

namespace UotanToolbox;

public partial class App : Application
{
    private IServiceProvider _provider = null!; // initialized in Initialize()

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        _provider = ConfigureServices();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Load Language settings
        CultureInfo CurCulture = Settings.Default.Language is not null and not ""
            ? new CultureInfo(Settings.Default.Language, false)
            : CultureInfo.CurrentCulture;
        Assets.Resources.Culture = CurCulture;

        // set up global device manager reference
        if (_provider is null)
            throw new InvalidOperationException("Service provider not initialized");
        Global.DeviceManager = _provider.GetRequiredService<UotanToolbox.Common.Devices.DeviceManager>();
        // perform initial scan in background
        _ = Global.DeviceManager.ScanAsync();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewLocator = _provider.GetRequiredService<IDataTemplate>();
            var mainVm = _provider.GetRequiredService<MainViewModel>();

            var window = viewLocator.Build(mainVm) as Window;
            if (window == null)
                throw new InvalidOperationException("Failed to build main window");
            desktop.MainWindow = window;
            // MainWindow is guaranteed non-null because we just assigned it from 'window'
            desktop.MainWindow!.Width = 1235;
            desktop.MainWindow!.Height = 840;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider ConfigureServices()
    {
        IDataTemplate? viewlocator = Current?.DataTemplates.First(x => x is ViewLocator);
        ServiceCollection services = new ServiceCollection();

        if (viewlocator is not null)
        {
            _ = services.AddSingleton(viewlocator);
        }

        _ = services.AddSingleton<PageNavigationService>();
        services.AddSingleton<ISukiToastManager, SukiToastManager>();
        services.AddSingleton<ISukiDialogManager, SukiDialogManager>();

        // transport implementations for devices
        services.AddSingleton<UotanToolbox.Common.Devices.IDeviceTransport, UotanToolbox.Common.Devices.AdbTransport>();
        services.AddSingleton<UotanToolbox.Common.Devices.IDeviceTransport, UotanToolbox.Common.Devices.FastbootTransport>();
        services.AddSingleton<UotanToolbox.Common.Devices.IDeviceTransport, UotanToolbox.Common.Devices.HdcTransport>();
        services.AddSingleton<UotanToolbox.Common.Devices.IDeviceTransport, UotanToolbox.Common.Devices.EdlTransport>();

        // device manager singleton
        services.AddSingleton<UotanToolbox.Common.Devices.DeviceManager>(sp =>
            new UotanToolbox.Common.Devices.DeviceManager(sp.GetServices<UotanToolbox.Common.Devices.IDeviceTransport>()));

        // Viewmodels
        _ = services.AddSingleton<MainViewModel>();
        System.Collections.Generic.IEnumerable<Type> types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => !p.IsAbstract && typeof(MainPageBase).IsAssignableFrom(p));
        foreach (Type type in types)
        {
            _ = services.AddSingleton(typeof(MainPageBase), type);
        }

        return services.BuildServiceProvider();
    }
}