using Avalonia.Collections;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using Newtonsoft.Json;
using ReactiveUI;
using SukiUI.Dialogs;
using SukiUI.Toasts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using UotanToolbox.Common;
using UotanToolbox.Common.Devices;
using UotanToolbox.Features.Settings;
using UotanToolbox.Utilities;

namespace UotanToolbox.Features.Home;

public partial class HomeViewModel : MainPageBase, IDisposable
{
    [ObservableProperty]
    private string _progressDisk = "0", _memLevel = "0", _status = "--", _bLStatus = "--",
    _vABStatus = "--", _codeName = "--", _vNDKVersion = "--", _cPUCode = "--",
    _powerOnTime = "--", _deviceBrand = "--", _deviceModel = "--", _systemSDK = "--",
    _cPUABI = "--", _displayHW = "--", _density = "--", _boardID = "--", _platform = "--",
    _compile = "--", _kernel = "--", _selectedSimpleContent = null, _diskType = "--",
    _batteryLevel = "0", _batteryInfo = "--", _useMem = "--", _diskInfo = "--";
    [ObservableProperty] private bool _IsConnecting;
    [ObservableProperty] private bool _commonDevicesList;
    [ObservableProperty] private static AvaloniaList<string> _simpleContent;
    public IAvaloniaReadOnlyList<MainPageBase> DemoPages { get; }

    [ObservableProperty] private bool _animationsEnabled;
    [ObservableProperty] private MainPageBase _activePage;
    [ObservableProperty] private bool _windowLocked = false;

    private static string GetTranslation(string key)
    {
        return FeaturesHelper.GetTranslation(key);
    }

    public HomeViewModel() : base(GetTranslation("Sidebar_HomePage"), MaterialIconKind.HomeOutline, int.MinValue)
    {
        _ = CheckDeviceList();

        // subscribe to device manager events so UI updates automatically
        if (Global.DeviceManager != null)
        {
            Global.DeviceManager.DeviceAdded += DeviceManager_DeviceAdded;
            Global.DeviceManager.DeviceRemoved += DeviceManager_DeviceRemoved;
            Global.DeviceManager.DeviceUpdated += DeviceManager_DeviceUpdated;
            Global.DeviceManager.ScanCompleted += DeviceManager_ScanCompleted;
        }

        this.WhenAnyValue(x => x.SelectedSimpleContent)
            .Subscribe(option =>
            {
                if (option != null && option != Global.thisdevice && SimpleContent != null && SimpleContent.Count != 0)
                {
                    Global.thisdevice = option;
                    _ = ConnectCore();
                }
            });

        _ = CheckForUpdate();
    }

    public async Task CheckForUpdate()
    {
        try
        {
            using HttpClient client = new HttpClient();
            string url = "https://toolbox.uotan.cn/api/list";
            StringContent content = new StringContent("{}", System.Text.Encoding.UTF8);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            HttpResponseMessage response = await client.PostAsync(url, content);
            _ = response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();

            dynamic convertedBody = JsonConvert.DeserializeObject<dynamic>(responseBody);
            SettingsViewModel vm = new SettingsViewModel();
            string version = convertedBody.release_version;



            if (version.Contains("beta"))
            {
                if (convertedBody.beta_version != vm.CurrentVersion)
                {
                    string serializedContent = (String)JsonConvert.SerializeObject(convertedBody.beta_content).Replace("\\n", "\n");
                    if (serializedContent.Length > 1) serializedContent = serializedContent.Substring(1, serializedContent.Length - 2);
                    Global.MainToastManager.CreateToast()
                          .OfType(NotificationType.Information)
                          .WithTitle(GetTranslation("Settings_NewVersionAvailable"))
                          .WithContent(GetTranslation("ConnectionDialog_Updates"))
                          .WithActionButton(GetTranslation("ConnectionDialog_ViewUpdates"), _ =>
                              Global.MainDialogManager.CreateDialog()
                                    .WithTitle(GetTranslation("Settings_NewVersionAvailable"))
                                    .WithContent(serializedContent)
                                    .OfType(NotificationType.Information)
                                    .WithActionButton(GetTranslation("ConnectionDialog_GetUpdate"), _ => UrlUtilities.OpenURL("https://toolbox.uotan.cn"), true)
                                    .WithActionButton(GetTranslation("ConnectionDialog_Cancel"), _ => { }, true)
                                    .TryShow(), true)
                          .WithActionButton(GetTranslation("ConnectionDialog_Cancel"), _ => { }, true)
                          .Queue();
                }
            }
            else
            {
                if (convertedBody.release_version != vm.CurrentVersion)
                {
                    string serializedContent = (String)JsonConvert.SerializeObject(convertedBody.release_content).Replace("\\n", "\n");
                    if (serializedContent.Length > 1) serializedContent = serializedContent.Substring(1, serializedContent.Length - 2);
                    Global.MainToastManager.CreateToast()
                          .OfType(NotificationType.Information)
                          .WithTitle(GetTranslation("Settings_NewVersionAvailable"))
                          .WithContent(GetTranslation("ConnectionDialog_Updates"))
                          .WithActionButton(GetTranslation("ConnectionDialog_ViewUpdates"), _ =>
                              Global.MainDialogManager.CreateDialog()
                                    .WithTitle(GetTranslation("Settings_NewVersionAvailable"))
                                    .WithContent(serializedContent)
                                    .OfType(NotificationType.Information)
                                    .WithActionButton(GetTranslation("ConnectionDialog_GetUpdate"), _ => UrlUtilities.OpenURL("https://toolbox.uotan.cn"), true)
                                    .WithActionButton(GetTranslation("ConnectionDialog_Cancel"), _ => { }, true)
                                    .TryShow(), true)
                          .WithActionButton(GetTranslation("ConnectionDialog_Cancel"), _ => { }, true)
                          .Queue();
                }
            }
        }
        catch (HttpRequestException e) { }
    }

    public async Task CheckDeviceList()
    {
        // periodically scan devices using DeviceManager; event handlers will update UI as needed
        while (true)
        {
            if (Global.DeviceManager != null)
            {
                await Global.DeviceManager.ScanAsync();
                // refresh displayed list if there is any change (do not animate busy when automatic)
                _ = await GetDevicesList(_hasWarnedNoDevice == false); // show warning first time only
            }
            await Task.Delay(1000);
        }
    }

    public async Task<bool> ListChecker()
    {
        // retained for compatibility but underlying scan is now driven by DeviceManager
        if (Global.DeviceManager != null)
        {
            await Global.DeviceManager.ScanAsync();
            return true;
        }
        return false;
    }

    private void DeviceManager_DeviceAdded(object sender, UotanToolbox.Common.Devices.DeviceEventArgs e)
    {
        // if new device appears refresh list, keep selection (do not warn)
        _ = GetDevicesList(false);
        Global.MainToastManager?.CreateToast()
            .WithTitle(GetTranslation("Home_Prompt"))
            .WithContent(string.Format(GetTranslation("Home_DeviceConnected"), e.Device.Id))
            .OfType(NotificationType.Information)
            .Dismiss().After(TimeSpan.FromSeconds(3))
            .Queue();
    }

    private void DeviceManager_DeviceRemoved(object sender, UotanToolbox.Common.Devices.DeviceEventArgs e)
    {
        // refresh list; if removed device was selected, pick another or clear (do not warn)
        _ = GetDevicesList(false);
        if (Global.thisdevice == e.Device.Id)
        {
            Global.thisdevice = SimpleContent?.FirstOrDefault();
        }
        Global.MainToastManager?.CreateToast()
            .WithTitle(GetTranslation("Home_Prompt"))
            .WithContent(string.Format(GetTranslation("Home_DeviceDisconnected"), e.Device.Id))
            .OfType(NotificationType.Warning)
            .Dismiss().After(TimeSpan.FromSeconds(3))
            .Queue();
    }

    private bool _hasWarnedNoDevice = false;

    public async Task<bool> GetDevicesList(bool showWarning = false)
    {
        if (Global.DeviceManager == null)
        {
            if (showWarning && !_hasWarnedNoDevice)
            {
                Global.MainDialogManager.CreateDialog().WithTitle(GetTranslation("Common_Error")).OfType(NotificationType.Error).WithContent(GetTranslation("Home_CheckDevice")).Dismiss().ByClickingBackground().TryShow();
                _hasWarnedNoDevice = true;
            }
            return false;
        }
        await Global.DeviceManager.ScanAsync();
        var devices = Global.DeviceManager.Devices.Select(d => d.Id).ToArray();
        if (devices.Length != 0)
        {
            Global.deviceslist = new AvaloniaList<string>(devices);
            SimpleContent = Global.deviceslist;
            if (SelectedSimpleContent == null || !string.Join("", SimpleContent).Contains(SelectedSimpleContent))
            {
                SelectedSimpleContent = Global.thisdevice != null && Global.deviceslist.Contains(Global.thisdevice) ? Global.thisdevice : SimpleContent.First();
            }
            return true;
        }
        else
        {
            if (showWarning && !_hasWarnedNoDevice)
            {
                Global.MainDialogManager.CreateDialog().WithTitle(GetTranslation("Common_Error")).OfType(NotificationType.Error).WithContent(GetTranslation("Home_CheckDevice")).Dismiss().ByClickingBackground().TryShow();
                _hasWarnedNoDevice = true;
            }
            return false;
        }
    }

    public async Task ConnectCore()
    {
        IsConnecting = true;
        MainViewModel sukiViewModel = GlobalData.MainViewModelInstance;
        Dictionary<string, string> DevicesInfo = await GetDevicesInfo.DevicesInfo(Global.thisdevice);
        Status = sukiViewModel.Status = DevicesInfo["Status"];
        BLStatus = sukiViewModel.BLStatus = DevicesInfo["BLStatus"];
        VABStatus = sukiViewModel.VABStatus = DevicesInfo["VABStatus"];
        CodeName = sukiViewModel.CodeName = DevicesInfo["CodeName"];
        VNDKVersion = DevicesInfo["VNDKVersion"];
        CPUCode = DevicesInfo["CPUCode"];
        PowerOnTime = DevicesInfo["PowerOnTime"];
        DeviceBrand = DevicesInfo["DeviceBrand"];
        DeviceModel = DevicesInfo["DeviceModel"];
        SystemSDK = DevicesInfo["SystemSDK"];
        CPUABI = DevicesInfo["CPUABI"];
        DisplayHW = DevicesInfo["DisplayHW"];
        Density = DevicesInfo["Density"];
        DiskType = DevicesInfo["DiskType"];
        BoardID = DevicesInfo["BoardID"];
        Platform = DevicesInfo["Platform"];
        Compile = DevicesInfo["Compile"];
        Kernel = DevicesInfo["Kernel"];
        BatteryLevel = DevicesInfo["BatteryLevel"];
        BatteryInfo = DevicesInfo["BatteryInfo"];
        MemLevel = DevicesInfo["MemLevel"];
        UseMem = DevicesInfo["UseMem"];
        DiskInfo = DevicesInfo["DiskInfo"];
        ProgressDisk = DevicesInfo["ProgressDisk"];
        IsConnecting = false;
    }

    // track last-known device sets per transport for manual refresh notifications
    private Dictionary<TransportType, HashSet<string>> _previousDeviceSets = new();

    [RelayCommand]
    public async Task FreshDeviceList()
    {
        Global.root = true;
        // manually show busy indicator during explicit refresh
        CommonDevicesList = true;

        // capture old sets
        var oldSets = _previousDeviceSets;

        if (await GetDevicesList(true) && Global.thisdevice != null && string.Join("", Global.deviceslist).Contains(Global.thisdevice))
        {
            if (!oldSets.Any())
            {
                // first refresh, treat as full update but still show separate toasts
            }

            // compute new sets after scanning
            var newSets = Global.DeviceManager.Devices
                .GroupBy(d => d.Transport)
                .ToDictionary(g => g.Key, g => new HashSet<string>(g.Select(d => d.Id)));

            // compare and notify transport-specific changes
            foreach (var kv in newSets)
            {
                var transport = kv.Key;
                var set = kv.Value;
                bool changed = !oldSets.ContainsKey(transport) || !oldSets[transport].SetEquals(set);
                if (changed)
                {
                    string toastText = transport switch
                    {
                        TransportType.Adb => GetTranslation("Home_ADBScanCompleted"),
                        TransportType.Fastboot => GetTranslation("Home_FastbootScanCompleted"),
                        TransportType.Hdc => GetTranslation("Home_HDCScanCompleted"),
                        _ => null
                    };
                    if (toastText != null)
                    {
                        Global.MainToastManager?.CreateToast()
                            .WithTitle(GetTranslation("Home_Prompt"))
                            .WithContent(toastText)
                            .OfType(NotificationType.Information)
                            .Dismiss().After(TimeSpan.FromSeconds(2))
                            .Queue();
                    }
                }
            }

            // preserve new sets for next refresh
            _previousDeviceSets = newSets;

            if (!oldSets.SequenceEqual(newSets))
            {
                await ConnectCore();
            }
        }

        CommonDevicesList = false;
    }

    [RelayCommand]
    public async Task RebootSys()
    {
        if (await GetDevicesInfo.SetDevicesInfoLittle())
        {
            var device = Global.DeviceManager?.Devices.FirstOrDefault(d => d.Id == Global.thisdevice);
            if (device != null)
            {
                switch (device.Transport)
                {
                    case TransportType.Adb:
                        await Global.DeviceManager.ExecuteAsync(device, "reboot");
                        break;
                    case TransportType.Fastboot:
                        await Global.DeviceManager.ExecuteAsync(device, "reboot");
                        break;
                    case TransportType.Hdc:
                        await Global.DeviceManager.ExecuteAsync(device, "target boot");
                        break;
                    default:
                        Global.MainDialogManager.CreateDialog().WithTitle(GetTranslation("Common_Error")).OfType(NotificationType.Error).WithContent(GetTranslation("Common_ModeError")).Dismiss().ByClickingBackground().TryShow();
                        break;
                }
            }
            else
            {
                Global.MainDialogManager.CreateDialog().WithTitle(GetTranslation("Common_Error")).OfType(NotificationType.Error).WithContent(GetTranslation("Common_NotConnected")).Dismiss().ByClickingBackground().TryShow();
            }
        }
        else
        {
            Global.MainDialogManager.CreateDialog().WithTitle(GetTranslation("Common_Error")).OfType(NotificationType.Error).WithContent(GetTranslation("Common_NotConnected")).Dismiss().ByClickingBackground().TryShow();
        }
    }

    [RelayCommand]
    public async Task RebootRec()
    {
        if (await GetDevicesInfo.SetDevicesInfoLittle())
        {
            var device = Global.DeviceManager?.Devices.FirstOrDefault(d => d.Id == Global.thisdevice);
            if (device != null)
            {
                switch (device.Transport)
                {
                    case TransportType.Adb:
                        await Global.DeviceManager.ExecuteAsync(device, "reboot recovery");
                        break;
                    case TransportType.Fastboot:
                        {
                            string output = await Global.DeviceManager.ExecuteAsync(device, "oem reboot-recovery");
                            if (output.Contains("unknown command"))
                            {
                                await Global.DeviceManager.ExecuteAsync(device, $"flash misc \"{Path.Combine(Global.runpath, "Image", "misc.img")}\"");
                                await Global.DeviceManager.ExecuteAsync(device, "reboot");
                            }
                            else
                            {
                                await Global.DeviceManager.ExecuteAsync(device, "reboot recovery");
                            }
                        }
                        break;
                    case TransportType.Hdc:
                        await Global.DeviceManager.ExecuteAsync(device, "target boot -recovery");
                        break;
                    default:
                        Global.MainDialogManager.CreateDialog().WithTitle(GetTranslation("Common_Error")).OfType(NotificationType.Error).WithContent(GetTranslation("Common_ModeError")).Dismiss().ByClickingBackground().TryShow();
                        break;
                }
            }
            else
            {
                Global.MainDialogManager.CreateDialog().WithTitle(GetTranslation("Common_Error")).OfType(NotificationType.Error).WithContent(GetTranslation("Common_NotConnected")).Dismiss().ByClickingBackground().TryShow();
            }
        }
        else
        {
            Global.MainDialogManager.CreateDialog().WithTitle(GetTranslation("Common_Error")).OfType(NotificationType.Error).WithContent(GetTranslation("Common_NotConnected")).Dismiss().ByClickingBackground().TryShow();
        }
    }

    [RelayCommand]
    public async Task RebootBL()
    {
        if (await GetDevicesInfo.SetDevicesInfoLittle())
        {
            var device = Global.DeviceManager?.Devices.FirstOrDefault(d => d.Id == Global.thisdevice);
            if (device != null)
            {
                switch (device.Transport)
                {
                    case TransportType.Adb:
                        await Global.DeviceManager.ExecuteAsync(device, "reboot bootloader");
                        break;
                    case TransportType.Fastboot:
                        await Global.DeviceManager.ExecuteAsync(device, "reboot-bootloader");
                        break;
                    case TransportType.Hdc:
                        await Global.DeviceManager.ExecuteAsync(device, "target boot -bootloader");
                        break;
                    default:
                        Global.MainDialogManager.CreateDialog().WithTitle(GetTranslation("Common_Error")).OfType(NotificationType.Error).WithContent(GetTranslation("Common_ModeError")).Dismiss().ByClickingBackground().TryShow();
                        break;
                }
            }
            else
            {
                Global.MainDialogManager.CreateDialog().WithTitle(GetTranslation("Common_Error")).OfType(NotificationType.Error).WithContent(GetTranslation("Common_NotConnected")).Dismiss().ByClickingBackground().TryShow();
            }
        }
        else
        {
            Global.MainDialogManager.CreateDialog().WithTitle(GetTranslation("Common_Error")).OfType(NotificationType.Error).WithContent(GetTranslation("Common_NotConnected")).Dismiss().ByClickingBackground().TryShow();
        }
    }

    [RelayCommand]
    public async Task RebootFB()
    {
        if (await GetDevicesInfo.SetDevicesInfoLittle())
        {
            var device = Global.DeviceManager?.Devices.FirstOrDefault(d => d.Id == Global.thisdevice);
            if (device != null)
            {
                switch (device.Transport)
                {
                    case TransportType.Adb:
                        await Global.DeviceManager.ExecuteAsync(device, "reboot fastboot");
                        break;
                    case TransportType.Fastboot:
                        await Global.DeviceManager.ExecuteAsync(device, "reboot-fastboot");
                        break;
                    case TransportType.Hdc:
                        await Global.DeviceManager.ExecuteAsync(device, "target boot -fastboot");
                        break;
                    default:
                        Global.MainDialogManager.CreateDialog().WithTitle(GetTranslation("Common_Error")).OfType(NotificationType.Error).WithContent(GetTranslation("Common_ModeError")).Dismiss().ByClickingBackground().TryShow();
                        break;
                }
            }
            else
            {
                Global.MainDialogManager.CreateDialog().WithTitle(GetTranslation("Common_Error")).OfType(NotificationType.Error).WithContent(GetTranslation("Common_NotConnected")).Dismiss().ByClickingBackground().TryShow();
            }
        }
        else
        {
            Global.MainDialogManager.CreateDialog().WithTitle(GetTranslation("Common_Error")).OfType(NotificationType.Error).WithContent(GetTranslation("Common_NotConnected")).Dismiss().ByClickingBackground().TryShow();
        }
    }

    [RelayCommand]
    public async Task PowerOff()
    {
        if (await GetDevicesInfo.SetDevicesInfoLittle())
        {
            var device = Global.DeviceManager?.Devices.FirstOrDefault(d => d.Id == Global.thisdevice);
            if (device != null)
            {
                switch (device.Transport)
                {
                    case TransportType.Adb:
                        await Global.DeviceManager.ExecuteAsync(device, "reboot -p");
                        break;
                    case TransportType.Fastboot:
                        {
                            string output = await Global.DeviceManager.ExecuteAsync(device, "oem poweroff");
                            _ = output.Contains("unknown command")
                                ? Global.MainDialogManager.CreateDialog().WithTitle(GetTranslation("Common_Error")).OfType(NotificationType.Error).WithContent(GetTranslation("Home_NotSupported")).Dismiss().ByClickingBackground().TryShow()
                                : Global.MainDialogManager.CreateDialog().WithTitle(GetTranslation("Common_Succ")).OfType(NotificationType.Success).WithContent(GetTranslation("Home_ShutDownTip")).Dismiss().ByClickingBackground().TryShow();
                        }
                        break;
                    case TransportType.Hdc:
                        await Global.DeviceManager.ExecuteAsync(device, "target boot shutdown");
                        break;
                    default:
                        Global.MainDialogManager.CreateDialog().WithTitle(GetTranslation("Common_Error")).OfType(NotificationType.Error).WithContent(GetTranslation("Common_ModeError")).Dismiss().ByClickingBackground().TryShow();
                        break;
                }
            }
            else
            {
                Global.MainDialogManager.CreateDialog().WithTitle(GetTranslation("Common_Error")).OfType(NotificationType.Error).WithContent(GetTranslation("Common_NotConnected")).Dismiss().ByClickingBackground().TryShow();
            }
        }
        else
        {
            Global.MainDialogManager.CreateDialog().WithTitle(GetTranslation("Common_Error")).OfType(NotificationType.Error).WithContent(GetTranslation("Common_NotConnected")).Dismiss().ByClickingBackground().TryShow();
        }
    }

    [RelayCommand]
    public async Task RebootEDL()
    {
        if (await GetDevicesInfo.SetDevicesInfoLittle())
        {
            var device = Global.DeviceManager?.Devices.FirstOrDefault(d => d.Id == Global.thisdevice);
            if (device != null)
            {
                switch (device.Transport)
                {
                    case TransportType.Adb:
                        await Global.DeviceManager.ExecuteAsync(device, "reboot edl");
                        break;
                    case TransportType.Fastboot:
                        await Global.DeviceManager.ExecuteAsync(device, "oem edl");
                        break;
                    case TransportType.Hdc:
                        await Global.DeviceManager.ExecuteAsync(device, "target boot -edl");
                        break;
                    default:
                        Global.MainDialogManager.CreateDialog().WithTitle(GetTranslation("Common_Error")).OfType(NotificationType.Error).WithContent(GetTranslation("Common_ModeError")).Dismiss().ByClickingBackground().TryShow();
                        break;
                }
            }
            else
            {
                Global.MainDialogManager.CreateDialog().WithTitle(GetTranslation("Common_Error")).OfType(NotificationType.Error).WithContent(GetTranslation("Common_NotConnected")).Dismiss().ByClickingBackground().TryShow();
            }
        }
        else
        {
            Global.MainDialogManager.CreateDialog().WithTitle(GetTranslation("Common_Error")).OfType(NotificationType.Error).WithContent(GetTranslation("Common_NotConnected")).Dismiss().ByClickingBackground().TryShow();
        }
    }

    private void DeviceManager_DeviceUpdated(object sender, UotanToolbox.Common.Devices.DeviceEventArgs e)
    {
        // update notification when a device's properties change
        Global.MainToastManager?.CreateToast()
            .WithTitle(GetTranslation("Home_Prompt"))
            .WithContent(string.Format(GetTranslation("Home_DeviceUpdated"), e.Device.Id))
            .OfType(NotificationType.Information)
            .Dismiss().After(TimeSpan.FromSeconds(3))
            .Queue();
    }

    private void DeviceManager_ScanCompleted(object sender, EventArgs e)
    {
        // scan completed event is intentionally silent; manual refresh triggers notifications
    }

    public void Dispose()
    {
        if (Global.DeviceManager != null)
        {
            Global.DeviceManager.DeviceAdded -= DeviceManager_DeviceAdded;
            Global.DeviceManager.DeviceRemoved -= DeviceManager_DeviceRemoved;
            Global.DeviceManager.DeviceUpdated -= DeviceManager_DeviceUpdated;
            Global.DeviceManager.ScanCompleted -= DeviceManager_ScanCompleted;
        }
    }
}