using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using FirmwareKit.Lp;
using FirmwareKit.Sparse.Core;
using FirmwareKit.Sparse.Models;
using FirmwareKit.Sparse.Streams;
using ReactiveUI;
using SharpCompress.Common;
using SukiUI.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UotanToolbox.Common;
using UotanToolbox.Common.ROMHelper;
using UotanToolbox.Utilities;
using static QRCoder.PayloadGenerator;

namespace UotanToolbox.Features.Advancedflash;

public partial class AdvancedflashView : UserControl
{
    private static string GetTranslation(string key)
    {
        return FeaturesHelper.GetTranslation(key);
    }

    public AvaloniaList<string> ScriptList = [".bat", ".sh", ".xml", ".txt"];
    private LpMetadata? _metadata;

    public AdvancedflashView()
    {
        InitializeComponent();
        Total.Text = "0";
        ExportScr.ItemsSource = ScriptList;
        _ = this.WhenAnyValue(part => part.SearchBox.Text)
            .Subscribe(option =>
            {
                if (ImgList.ItemsSource != null)
                {
                    var vm = GetViewModel();
                    AvaloniaList<FalshPartModel> Parts = vm.FalshPartModel;
                    if (!string.IsNullOrEmpty(SearchBox.Text))
                    {
                        ImgList.ItemsSource = Parts.Where(part => part.Name.Contains(SearchBox.Text, StringComparison.OrdinalIgnoreCase)).ToList();
                    }
                    else
                    {
                        ImgList.ItemsSource = Parts.Where(info => info != null).ToList();
                    }
                }
            });
    }

    private AdvancedflashViewModel GetViewModel()
    {
        return (AdvancedflashViewModel)DataContext;
    }

    private async void OpenFile(object sender, RoutedEventArgs args)
    {
        TopLevel topLevel = TopLevel.GetTopLevel(this);
        System.Collections.Generic.IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Image File",
            AllowMultiple = false
        });
        if (files.Count >= 1)
        {
            string path = File.Text = files[0].TryGetLocalPath();
            BusyFlash.IsBusy = true;
            string fileExtension = Path.GetExtension(files[0].TryGetLocalPath());
            switch (fileExtension)
            {
                case ".zip":
                case ".bin":
                    try
                    {
                        var parts = await PayloadParser.GetPartitionInfoAsync(path);
                        var vm = GetViewModel();
                        vm.FalshPartModel.Clear();
                        await Task.Delay(100);
                        foreach (var p in parts)
                        {
                            vm.FalshPartModel.Add(new FalshPartModel
                            {
                                Select = false,
                                Name = p.Name,
                                Size = p.SizeReadable,
                                Command = "",
                                FileName = ""
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        AdvancedflashLog.Text += $"Error: {ex.Message}";
                    }
                    break;
                case ".img":
                    try
                    {
                        _metadata = await Task.Run(() =>
                        {
                            using var fs = System.IO.File.OpenRead(path);
                            var magicBuf = new byte[4];
                            var isSparse = false;
                            if (fs.Read(magicBuf, 0, 4) == 4)
                            {
                                if (BitConverter.ToUInt32(magicBuf, 0) == SparseFormat.SparseHeaderMagic)
                                {
                                    isSparse = true;
                                }
                            }
                            fs.Close();

                            if (isSparse)
                            {
                                using var sparseFile = SparseFile.FromImageFile(path);
                                using var inputStream = new SparseStream(sparseFile);
                                var metadataReader = new MetadataReader();
                                var result = metadataReader.ReadFromImageStream(inputStream);
                                return result;
                            }
                            else
                            {
                                using var inputStream = System.IO.File.OpenRead(path);
                                var metadataReader = new MetadataReader();
                                var result = metadataReader.ReadFromImageStream(inputStream);
                                return result;
                            }
                        });

                        if (_metadata != null)
                        {
                            var vm = GetViewModel();
                            vm.FalshPartModel.Clear();
                            await Task.Delay(100);
                            foreach (var p in _metadata.Partitions)
                            {
                                ulong totalSize = 0;
                                for (uint i = 0; i < p.NumExtents; i++)
                                {
                                    totalSize += _metadata.Extents[(int)(p.FirstExtentIndex + i)].NumSectors * 512;
                                }

                                vm.FalshPartModel.Add(new FalshPartModel
                                {
                                    Select = false,
                                    Name = p.GetName(),
                                    Size = StringHelper.byte2AUnit(totalSize),
                                    Command = "",
                                    FileName = ""
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AdvancedflashLog.Text += $"Error: {ex.Message}";
                    }
                    break;
                case ".bat":
                    

                case ".sh":
                    

                case ".xml":
                   

                case ".txt":
                    

                default:
                    break;
            }
            BusyFlash.IsBusy = false;
        }
    }

    private async void OpenFolder(object sender, RoutedEventArgs args)
    {
        TopLevel topLevel = TopLevel.GetTopLevel(this);
        System.Collections.Generic.IReadOnlyList<IStorageFolder> files = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open Folder",
            AllowMultiple = false
        });
        if (files.Count >= 1)
        {
            File.Text = files[0].TryGetLocalPath();
        }
    }

    private async void OpenUrl(object sender, RoutedEventArgs args)
    {
        BusyFlash.IsBusy = true;
        try
        {
            if (Uri.IsWellFormedUriString(File.Text, UriKind.Absolute))
            {
                var parts = await PayloadParser.GetPartitionInfoFromUrlV2Async(File.Text);
                var vm = GetViewModel();
                foreach (var p in parts)
                {
                    if (p.SizeBytes > 314572800)
                    {
                        vm.FalshPartModel.Add(new FalshPartModel
                        {
                            Select = false,
                            SelectDis = false,
                            Name = p.Name,
                            Size = p.SizeReadable,
                            Command = "请下载完整包",
                            FileName = ""
                        });
                    }
                    else
                    {
                        vm.FalshPartModel.Add(new FalshPartModel
                        {
                            Select = false,
                            Name = p.Name,
                            Size = p.SizeReadable,
                            Command = "",
                            FileName = ""
                        });
                    }
                }
            }
            else
            {
                UrlUtilities.OpenURL("https://xiaomirom.com/series/");
            }
        }
        catch (Exception ex)
        {
            AdvancedflashLog.Text += $"Error: {ex.Message}";
        }
        BusyFlash.IsBusy = false;
    }

    private async void Extract(object sender, RoutedEventArgs args)
    {
        BusyFlash.IsBusy = true;
        ExtractSelect.IsEnabled = false;
        foreach (var item in GetViewModel().FalshPartModel)
        {
            if (item.Select == true)
            {
                if (Uri.IsWellFormedUriString(File.Text, UriKind.Absolute))
                {

                }
                else
                {

                }
            }
        }
        BusyFlash.IsBusy = false;
        ExtractSelect.IsEnabled = true;
    }

    private async void SetAll(object sender, RoutedEventArgs args)
    {
        if (SelectAll.IsChecked == true)
        {
            foreach (var item in GetViewModel().FalshPartModel)
            {
                item.Select = true;
            }
        }
        else
        {
            foreach (var item in GetViewModel().FalshPartModel)
            {
                item.Select = false;
            }
        }
        int total = 0;
        foreach (var item in GetViewModel().FalshPartModel)
        {
            if (item.Select == true)
            {
                total++;
            }
        }
        Total.Text = total.ToString();
    }

    private async void Selected(object sender, RoutedEventArgs args)
    {
        await Task.Delay(100);
        int total = 0;
        foreach (var item in GetViewModel().FalshPartModel)
        {
            if (item.Select == true)
            {
                total++;
            }
        }
        Total.Text = total.ToString();
    }

    private async void OpenImageFile(object sender, RoutedEventArgs args)
    {
        Button button = (Button)sender;
        FalshPartModel falshPartModel = (FalshPartModel)button.DataContext;
        TopLevel topLevel = TopLevel.GetTopLevel(this);
        System.Collections.Generic.IReadOnlyList<IStorageFile> file = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Image File",
            AllowMultiple = false
        });
        if (file.Count >= 1)
        {
            falshPartModel.FileName = Path.GetFileName(StringHelper.FilePath(file[0].Path.ToString()));
        }
    }
}