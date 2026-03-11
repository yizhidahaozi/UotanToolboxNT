using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SukiUI.Dialogs;
using SukiUI.Toasts;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UotanToolbox.Common;
using UotanToolbox.Common.Devices;

namespace UotanToolbox.Features.Customizedflash;

public partial class CustomizedflashView : UserControl
{
    private static string GetTranslation(string key)
    {
        return FeaturesHelper.GetTranslation(key);
    }

    public CustomizedflashView()
    {
        InitializeComponent();
    }

    private async Task AppendFastbootOutputAsync(string commandOutput)
    {
        if (commandOutput == null)
        {
            return;
        }

        string normalizedOutput = commandOutput
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Replace("\0", string.Empty);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            CustomizedflashLog.Text += normalizedOutput;
            CustomizedflashLog.CaretIndex = CustomizedflashLog.Text.Length;
        });
    }

    public async Task Fastboot(string fbshell)
    {
        if (Global.DeviceManager != null)
        {
            var dev = Global.DeviceManager.Devices.FirstOrDefault(d => d.Id == Global.thisdevice && d.Transport == TransportType.Fastboot);
            if (dev != null)
            {
                _ = await Global.DeviceManager.ExecuteStreamingAsync(dev, fbshell, chunk => _ = AppendFastbootOutputAsync(chunk));
                return;
            }
        }

        _ = await CallExternalProgram.Fastboot(fbshell, chunk => _ = AppendFastbootOutputAsync(chunk));
    }

    private async void OpenSystemFile(object sender, RoutedEventArgs args)
    {
        TopLevel topLevel = TopLevel.GetTopLevel(this);
        System.Collections.Generic.IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Image File",
            AllowMultiple = false
        });
        if (files.Count >= 1)
        {
            SystemFile.Text = files[0].TryGetLocalPath();
        }
    }

    private async void FlashSystemFile(object sender, RoutedEventArgs args)
    {
        if (await GetDevicesInfo.SetDevicesInfoLittle())
        {
            if (SystemFile.Text != null)
            {
                MainViewModel sukiViewModel = GlobalData.MainViewModelInstance;
                if (sukiViewModel.Status == GetTranslation("Home_Fastboot") || sukiViewModel.Status == GetTranslation("Home_Fastbootd"))
                {
                    Global.checkdevice = false;
                    OpenSystemFileBut.IsEnabled = false;
                    FlashSystemFileBut.IsEnabled = false;
                    CustomizedflashLog.Text = GetTranslation("Customizedflash_Flashing") + "\n";
                    string shell = string.Format($"-s {Global.thisdevice} flash system \"{SystemFile.Text}\"");
                    await Fastboot(shell);
                    OpenSystemFileBut.IsEnabled = true;
                    FlashSystemFileBut.IsEnabled = true;
                    Global.checkdevice = true;
                }
                else
                {
                    Global.MainDialogManager.CreateDialog()
                                                .WithTitle(GetTranslation("Common_Error"))
                                                .OfType(NotificationType.Error)
                                                .WithContent(GetTranslation("Common_EnterFastboot"))
                                                .Dismiss().ByClickingBackground()
                                                .TryShow();
                }
            }
            else
            {
                Global.MainDialogManager.CreateDialog()
                                            .WithTitle(GetTranslation("Common_Error"))
                                            .OfType(NotificationType.Error)
                                            .WithContent(GetTranslation("Customizedflash_SelectFile"))
                                            .Dismiss().ByClickingBackground()
                                            .TryShow();
            }
        }
        else
        {
            Global.MainDialogManager.CreateDialog()
                                        .WithTitle(GetTranslation("Common_Error"))
                                        .OfType(NotificationType.Error)
                                        .WithContent(GetTranslation("Common_NotConnected"))
                                        .Dismiss().ByClickingBackground()
                                        .TryShow();
        }
    }

    private async void OpenProductFile(object sender, RoutedEventArgs args)
    {
        TopLevel topLevel = TopLevel.GetTopLevel(this);
        System.Collections.Generic.IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Image File",
            AllowMultiple = false
        });
        if (files.Count >= 1)
        {
            ProductFile.Text = files[0].TryGetLocalPath();
        }
    }
    private async void FlashProductFile(object sender, RoutedEventArgs args)
    {
        if (await GetDevicesInfo.SetDevicesInfoLittle())
        {
            if (ProductFile.Text != null)
            {
                MainViewModel sukiViewModel = GlobalData.MainViewModelInstance;
                if (sukiViewModel.Status == GetTranslation("Home_Fastboot") || sukiViewModel.Status == GetTranslation("Home_Fastbootd"))
                {
                    Global.checkdevice = false;
                    OpenProductFileBut.IsEnabled = false;
                    FlashProductFileBut.IsEnabled = false;
                    CustomizedflashLog.Text = GetTranslation("Customizedflash_Flashing") + "\n";
                    string shell = string.Format($"-s {Global.thisdevice} flash product \"{ProductFile.Text}\"");
                    await Fastboot(shell);
                    OpenProductFileBut.IsEnabled = true;
                    FlashProductFileBut.IsEnabled = true;
                    Global.checkdevice = true;
                }
                else
                {
                    Global.MainDialogManager.CreateDialog()
                                                .WithTitle(GetTranslation("Common_Error"))
                                                .OfType(NotificationType.Error)
                                                .WithContent(GetTranslation("Common_EnterFastboot"))
                                                .Dismiss().ByClickingBackground()
                                                .TryShow();
                }
            }
            else
            {
                Global.MainDialogManager.CreateDialog()
                                            .WithTitle(GetTranslation("Common_Error"))
                                            .OfType(NotificationType.Error)
                                            .WithContent(GetTranslation("Customizedflash_SelectFile"))
                                            .Dismiss().ByClickingBackground()
                                            .TryShow();
            }
        }
        else
        {
            Global.MainDialogManager.CreateDialog()
                                        .WithTitle(GetTranslation("Common_Error"))
                                        .OfType(NotificationType.Error)
                                        .WithContent(GetTranslation("Common_NotConnected"))
                                        .Dismiss().ByClickingBackground()
                                        .TryShow();
        }
    }
    private async void OpenVenderFile(object sender, RoutedEventArgs args)
    {
        TopLevel topLevel = TopLevel.GetTopLevel(this);
        System.Collections.Generic.IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Image File",
            AllowMultiple = false
        });
        if (files.Count >= 1)
        {
            VenderFile.Text = files[0].TryGetLocalPath();
        }
    }
    private async void FlashVenderFile(object sender, RoutedEventArgs args)
    {
        if (await GetDevicesInfo.SetDevicesInfoLittle())
        {
            if (VenderFile.Text != null)
            {
                MainViewModel sukiViewModel = GlobalData.MainViewModelInstance;
                if (sukiViewModel.Status == GetTranslation("Home_Fastboot") || sukiViewModel.Status == GetTranslation("Home_Fastbootd"))
                {
                    Global.checkdevice = false;
                    OpenVenderFileBut.IsEnabled = false;
                    FlashVenderFileBut.IsEnabled = false;
                    CustomizedflashLog.Text = GetTranslation("Customizedflash_Flashing") + "\n";
                    string shell = string.Format($"-s {Global.thisdevice} flash vendor \"{VenderFile.Text}\"");
                    await Fastboot(shell);
                    OpenVenderFileBut.IsEnabled = true;
                    FlashVenderFileBut.IsEnabled = true;
                    Global.checkdevice = true;
                }
                else
                {
                    Global.MainDialogManager.CreateDialog()
                                                .WithTitle(GetTranslation("Common_Error"))
                                                .OfType(NotificationType.Error)
                                                .WithContent(GetTranslation("Common_EnterFastboot"))
                                                .Dismiss().ByClickingBackground()
                                                .TryShow();
                }
            }
            else
            {
                Global.MainDialogManager.CreateDialog()
                                            .WithTitle(GetTranslation("Common_Error"))
                                            .OfType(NotificationType.Error)
                                            .WithContent(GetTranslation("Customizedflash_SelectFile"))
                                            .Dismiss().ByClickingBackground()
                                            .TryShow();
            }
        }
        else
        {
            Global.MainDialogManager.CreateDialog()
                                        .WithTitle(GetTranslation("Common_Error"))
                                        .OfType(NotificationType.Error)
                                        .WithContent(GetTranslation("Common_NotConnected"))
                                        .Dismiss().ByClickingBackground()
                                        .TryShow();
        }
    }
    private async void OpenBootFile(object sender, RoutedEventArgs args)
    {
        TopLevel topLevel = TopLevel.GetTopLevel(this);
        System.Collections.Generic.IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Image File",
            AllowMultiple = false
        });
        if (files.Count >= 1)
        {
            BootFile.Text = files[0].TryGetLocalPath();
        }
    }
    private async void FlashBootFile(object sender, RoutedEventArgs args)
    {
        if (await GetDevicesInfo.SetDevicesInfoLittle())
        {
            if (BootFile.Text != null)
            {
                MainViewModel sukiViewModel = GlobalData.MainViewModelInstance;
                if (sukiViewModel.Status == GetTranslation("Home_Fastboot") || sukiViewModel.Status == GetTranslation("Home_Fastbootd"))
                {
                    Global.checkdevice = false;
                    OpenBootFileBut.IsEnabled = false;
                    FlashBootFileBut.IsEnabled = false;
                    CustomizedflashLog.Text = GetTranslation("Customizedflash_Flashing") + "\n";
                    string shell = string.Format($"-s {Global.thisdevice} flash boot \"{BootFile.Text}\"");
                    await Fastboot(shell);
                    OpenBootFileBut.IsEnabled = true;
                    FlashBootFileBut.IsEnabled = true;
                    Global.checkdevice = true;
                }
                else
                {
                    Global.MainDialogManager.CreateDialog()
                                                .WithTitle(GetTranslation("Common_Error"))
                                                .OfType(NotificationType.Error)
                                                .WithContent(GetTranslation("Common_EnterFastboot"))
                                                .Dismiss().ByClickingBackground()
                                                .TryShow();
                }
            }
            else
            {
                Global.MainDialogManager.CreateDialog()
                                            .WithTitle(GetTranslation("Common_Error"))
                                            .OfType(NotificationType.Error)
                                            .WithContent(GetTranslation("Customizedflash_SelectFile"))
                                            .Dismiss().ByClickingBackground()
                                            .TryShow();
            }
        }
        else
        {
            Global.MainDialogManager.CreateDialog()
                                        .WithTitle(GetTranslation("Common_Error"))
                                        .OfType(NotificationType.Error)
                                        .WithContent(GetTranslation("Common_NotConnected"))
                                        .Dismiss().ByClickingBackground()
                                        .TryShow();
        }
    }
    private async void OpenSystemextFile(object sender, RoutedEventArgs args)
    {
        TopLevel topLevel = TopLevel.GetTopLevel(this);
        System.Collections.Generic.IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Image File",
            AllowMultiple = false
        });
        if (files.Count >= 1)
        {
            SystemextFile.Text = files[0].TryGetLocalPath();
        }
    }
    private async void FlashSystemextFile(object sender, RoutedEventArgs args)
    {
        if (await GetDevicesInfo.SetDevicesInfoLittle())
        {
            if (SystemextFile.Text != null)
            {
                MainViewModel sukiViewModel = GlobalData.MainViewModelInstance;
                if (sukiViewModel.Status == GetTranslation("Home_Fastboot") || sukiViewModel.Status == GetTranslation("Home_Fastbootd"))
                {
                    Global.checkdevice = false;
                    OpenSystemextFileBut.IsEnabled = false;
                    FlashSystemextFileBut.IsEnabled = false;
                    CustomizedflashLog.Text = GetTranslation("Customizedflash_Flashing") + "\n";
                    string shell = string.Format($"-s {Global.thisdevice} flash system_ext \"{SystemextFile.Text}\"");
                    await Fastboot(shell);
                    OpenSystemextFileBut.IsEnabled = true;
                    FlashSystemextFileBut.IsEnabled = true;
                    Global.checkdevice = true;
                }
                else
                {
                    Global.MainDialogManager.CreateDialog()
                                                .WithTitle(GetTranslation("Common_Error"))
                                                .OfType(NotificationType.Error)
                                                .WithContent(GetTranslation("Common_EnterFastboot"))
                                                .Dismiss().ByClickingBackground()
                                                .TryShow();
                }
            }
            else
            {
                Global.MainDialogManager.CreateDialog()
                                            .WithTitle(GetTranslation("Common_Error"))
                                            .OfType(NotificationType.Error)
                                            .WithContent(GetTranslation("Customizedflash_SelectFile"))
                                            .Dismiss().ByClickingBackground()
                                            .TryShow();
            }
        }
        else
        {
            Global.MainDialogManager.CreateDialog()
                                        .WithTitle(GetTranslation("Common_Error"))
                                        .OfType(NotificationType.Error)
                                        .WithContent(GetTranslation("Common_NotConnected"))
                                        .Dismiss().ByClickingBackground()
                                        .TryShow();
        }
    }
    private async void OpenOdmFile(object sender, RoutedEventArgs args)
    {
        TopLevel topLevel = TopLevel.GetTopLevel(this);
        System.Collections.Generic.IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Image File",
            AllowMultiple = false
        });
        if (files.Count >= 1)
        {
            OdmFile.Text = files[0].TryGetLocalPath();
        }
    }
    private async void FlashOdmFile(object sender, RoutedEventArgs args)
    {
        if (await GetDevicesInfo.SetDevicesInfoLittle())
        {
            if (OdmFile.Text != null)
            {
                MainViewModel sukiViewModel = GlobalData.MainViewModelInstance;
                if (sukiViewModel.Status == GetTranslation("Home_Fastboot") || sukiViewModel.Status == GetTranslation("Home_Fastbootd"))
                {
                    Global.checkdevice = false;
                    OpenOdmFileBut.IsEnabled = false;
                    FlashOdmFileBut.IsEnabled = false;
                    CustomizedflashLog.Text = GetTranslation("Customizedflash_Flashing") + "\n";
                    string shell = string.Format($"-s {Global.thisdevice} flash odm \"{OdmFile.Text}\"");
                    await Fastboot(shell);
                    OpenOdmFileBut.IsEnabled = true;
                    FlashOdmFileBut.IsEnabled = true;
                    Global.checkdevice = true;
                }
                else
                {
                    Global.MainDialogManager.CreateDialog()
                                                .WithTitle(GetTranslation("Common_Error"))
                                                .OfType(NotificationType.Error)
                                                .WithContent(GetTranslation("Common_EnterFastboot"))
                                                .Dismiss().ByClickingBackground()
                                                .TryShow();
                }
            }
            else
            {
                Global.MainDialogManager.CreateDialog()
                                            .WithTitle(GetTranslation("Common_Error"))
                                            .OfType(NotificationType.Error)
                                            .WithContent(GetTranslation("Customizedflash_SelectFile"))
                                            .Dismiss().ByClickingBackground()
                                            .TryShow();
            }
        }
        else
        {
            Global.MainDialogManager.CreateDialog()
                                        .WithTitle(GetTranslation("Common_Error"))
                                        .OfType(NotificationType.Error)
                                        .WithContent(GetTranslation("Common_NotConnected"))
                                        .Dismiss().ByClickingBackground()
                                        .TryShow();
        }
    }
    private async void OpenVenderbootFile(object sender, RoutedEventArgs args)
    {
        TopLevel topLevel = TopLevel.GetTopLevel(this);
        System.Collections.Generic.IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Image File",
            AllowMultiple = false
        });
        if (files.Count >= 1)
        {
            VenderbootFile.Text = files[0].TryGetLocalPath();
        }
    }
    private async void FlashVenderbootFile(object sender, RoutedEventArgs args)
    {
        if (await GetDevicesInfo.SetDevicesInfoLittle())
        {
            if (VenderbootFile.Text != null)
            {
                MainViewModel sukiViewModel = GlobalData.MainViewModelInstance;
                if (sukiViewModel.Status == GetTranslation("Home_Fastboot") || sukiViewModel.Status == GetTranslation("Home_Fastbootd"))
                {
                    Global.checkdevice = false;
                    OpenVenderbootFileBut.IsEnabled = false;
                    FlashVenderbootFileBut.IsEnabled = false;
                    CustomizedflashLog.Text = GetTranslation("Customizedflash_Flashing") + "\n";
                    string shell = string.Format($"-s {Global.thisdevice} flash vendor_boot \"{VenderbootFile.Text}\"");
                    await Fastboot(shell);
                    OpenVenderbootFileBut.IsEnabled = true;
                    FlashVenderbootFileBut.IsEnabled = true;
                    Global.checkdevice = true;
                }
                else
                {
                    Global.MainDialogManager.CreateDialog()
                                                .WithTitle(GetTranslation("Common_Error"))
                                                .OfType(NotificationType.Error)
                                                .WithContent(GetTranslation("Common_EnterFastboot"))
                                                .Dismiss().ByClickingBackground()
                                                .TryShow();
                }
            }
            else
            {
                Global.MainDialogManager.CreateDialog()
                                            .WithTitle(GetTranslation("Common_Error"))
                                            .OfType(NotificationType.Error)
                                            .WithContent(GetTranslation("Customizedflash_SelectFile"))
                                            .Dismiss().ByClickingBackground()
                                            .TryShow();
            }
        }
        else
        {
            Global.MainDialogManager.CreateDialog()
                                        .WithTitle(GetTranslation("Common_Error"))
                                        .OfType(NotificationType.Error)
                                        .WithContent(GetTranslation("Common_NotConnected"))
                                        .Dismiss().ByClickingBackground()
                                        .TryShow();
        }
    }
    private async void OpenInitbootFile(object sender, RoutedEventArgs args)
    {
        TopLevel topLevel = TopLevel.GetTopLevel(this);
        System.Collections.Generic.IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Image File",
            AllowMultiple = false
        });
        if (files.Count >= 1)
        {
            InitbootFile.Text = files[0].TryGetLocalPath();
        }
    }
    private async void FlashInitbootFile(object sender, RoutedEventArgs args)
    {
        if (await GetDevicesInfo.SetDevicesInfoLittle())
        {
            if (InitbootFile.Text != null)
            {
                MainViewModel sukiViewModel = GlobalData.MainViewModelInstance;
                if (sukiViewModel.Status == GetTranslation("Home_Fastboot") || sukiViewModel.Status == GetTranslation("Home_Fastbootd"))
                {
                    Global.checkdevice = false;
                    OpenInitbootFileBut.IsEnabled = false;
                    FlashInitbootFileBut.IsEnabled = false;
                    CustomizedflashLog.Text = GetTranslation("Customizedflash_Flashing") + "\n";
                    string shell = string.Format($"-s {Global.thisdevice} flash init_boot \"{InitbootFile.Text}\"");
                    await Fastboot(shell);
                    OpenInitbootFileBut.IsEnabled = true;
                    FlashInitbootFileBut.IsEnabled = true;
                    Global.checkdevice = true;
                }
                else
                {
                    Global.MainDialogManager.CreateDialog()
                                                .WithTitle(GetTranslation("Common_Error"))
                                                .OfType(NotificationType.Error)
                                                .WithContent(GetTranslation("Common_EnterFastboot"))
                                                .Dismiss().ByClickingBackground()
                                                .TryShow();
                }
            }
            else
            {
                Global.MainDialogManager.CreateDialog()
                                            .WithTitle(GetTranslation("Common_Error"))
                                            .OfType(NotificationType.Error)
                                            .WithContent(GetTranslation("Customizedflash_SelectFile"))
                                            .Dismiss().ByClickingBackground()
                                            .TryShow();
            }
        }
        else
        {
            Global.MainDialogManager.CreateDialog()
                                        .WithTitle(GetTranslation("Common_Error"))
                                        .OfType(NotificationType.Error)
                                        .WithContent(GetTranslation("Common_NotConnected"))
                                        .Dismiss().ByClickingBackground()
                                        .TryShow();
        }
    }
    private async void OpenImageFile(object sender, RoutedEventArgs args)
    {
        TopLevel topLevel = TopLevel.GetTopLevel(this);
        System.Collections.Generic.IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Image File",
            AllowMultiple = false
        });
        if (files.Count >= 1)
        {
            ImageFile.Text = files[0].TryGetLocalPath();
        }
    }
    private async void FlashImageFile(object sender, RoutedEventArgs args)
    {
        if (await GetDevicesInfo.SetDevicesInfoLittle())
        {
            if (ImageFile.Text != null)
            {
                MainViewModel sukiViewModel = GlobalData.MainViewModelInstance;
                if (sukiViewModel.Status == GetTranslation("Home_Fastboot") || sukiViewModel.Status == GetTranslation("Home_Fastbootd"))
                {
                    Global.checkdevice = false;
                    OpenImageFileBut.IsEnabled = false;
                    FlashImageFileBut.IsEnabled = false;
                    CustomizedflashLog.Text = GetTranslation("Customizedflash_Flashing") + "\n";
                    string shell = string.Format($"-s {Global.thisdevice} flash {Part.Text} \"{ImageFile.Text}\"");
                    await Fastboot(shell);
                    OpenImageFileBut.IsEnabled = true;
                    FlashImageFileBut.IsEnabled = true;
                    Global.checkdevice = true;
                }
                else
                {
                    Global.MainDialogManager.CreateDialog()
                                                .WithTitle(GetTranslation("Common_Error"))
                                                .OfType(NotificationType.Error)
                                                .WithContent(GetTranslation("Common_EnterFastboot"))
                                                .Dismiss().ByClickingBackground()
                                                .TryShow();
                }
            }
            else
            {
                Global.MainDialogManager.CreateDialog()
                                            .WithTitle(GetTranslation("Common_Error"))
                                            .OfType(NotificationType.Error)
                                            .WithContent(GetTranslation("Customizedflash_SelectFile"))
                                            .Dismiss().ByClickingBackground()
                                            .TryShow();
            }
        }
        else
        {
            Global.MainDialogManager.CreateDialog()
                                        .WithTitle(GetTranslation("Common_Error"))
                                        .OfType(NotificationType.Error)
                                        .WithContent(GetTranslation("Common_NotConnected"))
                                        .Dismiss().ByClickingBackground()
                                        .TryShow();
        }
    }

    private static FilePickerFileType VbmetaPicker { get; } = new("Vbmeta File")
    {
        Patterns = new[] { "*vbmeta*.img", "*VBMETA*.img" },
        AppleUniformTypeIdentifiers = new[] { "*vbmeta*.img", "*VBMETA*.img" }
    };

    private async void DisableVbmeta(object sender, RoutedEventArgs args)
    {
        if (await GetDevicesInfo.SetDevicesInfoLittle())
        {
            MainViewModel sukiViewModel = GlobalData.MainViewModelInstance;
            if (sukiViewModel.Status == GetTranslation("Home_Fastboot") || sukiViewModel.Status == GetTranslation("Home_Fastbootd"))
            {
                Global.MainDialogManager.CreateDialog()
                                        .WithTitle(GetTranslation("Common_Warn"))
                                        .WithContent(GetTranslation("Customizedflash_ChoiceVbmeta"))
                                        .OfType(NotificationType.Warning)
                                        .WithActionButton(GetTranslation("Customizedflash_SelectVbmeta"), async _ =>
                                        {
                                            string deviceId = Global.thisdevice;
                                            if (string.IsNullOrWhiteSpace(deviceId))
                                            {
                                                Global.MainDialogManager.CreateDialog()
                                                                    .WithTitle(GetTranslation("Common_Error"))
                                                                    .OfType(NotificationType.Error)
                                                                    .WithContent(GetTranslation("Common_NotConnected"))
                                                                    .Dismiss().ByClickingBackground()
                                                                    .TryShow();
                                                return;
                                            }

                                            TopLevel topLevel = TopLevel.GetTopLevel(this);
                                            System.Collections.Generic.IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                                            {
                                                Title = "Open File",
                                                AllowMultiple = true,
                                                FileTypeFilter = new[] { VbmetaPicker, FilePickerFileTypes.TextPlain }
                                            });
                                            if (files.Count >= 1)
                                            {
                                                Global.checkdevice = false;
                                                try
                                                {
                                                    for (int i = 0; i < files.Count; i++)
                                                    {
                                                        await Fastboot($"-s {deviceId} --disable-verity --disable-verification flash {Path.GetFileNameWithoutExtension(files[i].Name)} \"{files[i].TryGetLocalPath()}\"");
                                                    }
                                                }
                                                finally
                                                {
                                                    Global.checkdevice = true;
                                                }
                                            }
                                        }, true)
                                        .WithActionButton(GetTranslation("ConnectionDialog_Continue"), async _ =>
                                        {
                                            string deviceId = Global.thisdevice;
                                            if (string.IsNullOrWhiteSpace(deviceId))
                                            {
                                                Global.MainDialogManager.CreateDialog()
                                                                    .WithTitle(GetTranslation("Common_Error"))
                                                                    .OfType(NotificationType.Error)
                                                                    .WithContent(GetTranslation("Common_NotConnected"))
                                                                    .Dismiss().ByClickingBackground()
                                                                    .TryShow();
                                                return;
                                            }

                                            CustomizedflashLog.Text = "";
                                            Global.checkdevice = false;
                                            try
                                            {
                                                await Fastboot($"-s {deviceId} --disable-verity --disable-verification flash vbmeta \"{Path.Combine(Global.runpath, "Image", "vbmeta.img")}\"");
                                                await Fastboot($"-s {deviceId} --disable-verity --disable-verification flash vbmeta_system \"{Path.Combine(Global.runpath, "Image", "vbmeta.img")}\"");
                                                await Fastboot($"-s {deviceId} --disable-verity --disable-verification flash vbmeta_vendor \"{Path.Combine(Global.runpath, "Image", "vbmeta.img")}\"");
                                            }
                                            finally
                                            {
                                                Global.checkdevice = true;
                                            }
                                        }, true)
                                        .WithActionButton(GetTranslation("ConnectionDialog_Cancel"), _ => { }, true)
                                        .TryShow();
            }
            else
            {
                Global.MainDialogManager.CreateDialog()
                                            .WithTitle(GetTranslation("Common_Error"))
                                            .OfType(NotificationType.Error)
                                            .WithContent(GetTranslation("Common_EnterFastboot"))
                                            .Dismiss().ByClickingBackground()
                                            .TryShow();
            }
        }
        else
        {
            Global.MainDialogManager.CreateDialog()
                                        .WithTitle(GetTranslation("Common_Error"))
                                        .OfType(NotificationType.Error)
                                        .WithContent(GetTranslation("Common_NotConnected"))
                                        .Dismiss().ByClickingBackground()
                                        .TryShow();
        }
    }
    private async void SetOther(object sender, RoutedEventArgs args)
    {
        if (await GetDevicesInfo.SetDevicesInfoLittle())
        {
            MainViewModel sukiViewModel = GlobalData.MainViewModelInstance;
            if (sukiViewModel.Status == GetTranslation("Home_Fastboot") || sukiViewModel.Status == GetTranslation("Home_Fastbootd"))
            {
                CustomizedflashLog.Text = "";
                string shell = string.Format($"-s {Global.thisdevice} set_active other");
                await Fastboot(shell);
            }
            else
            {
                Global.MainDialogManager.CreateDialog()
                                            .WithTitle(GetTranslation("Common_Error"))
                                            .OfType(NotificationType.Error)
                                            .WithContent(GetTranslation("Common_EnterFastboot"))
                                            .Dismiss().ByClickingBackground()
                                            .TryShow();
            }
        }
        else
        {
            Global.MainDialogManager.CreateDialog()
                                        .WithTitle(GetTranslation("Common_Error"))
                                        .OfType(NotificationType.Error)
                                        .WithContent(GetTranslation("Common_NotConnected"))
                                        .Dismiss().ByClickingBackground()
                                        .TryShow();
        }
    }
}