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
    private enum ParsedFileType
    {
        Unknown,
        Script,
        Payload,
        Super,
        PayloadUrl
    }

    private static string GetTranslation(string key)
    {
        return FeaturesHelper.GetTranslation(key);
    }

    public AvaloniaList<string> ScriptList = [".bat", ".sh", ".xml", ".txt"];
    private LpMetadata? _metadata;
    private ParsedFileType _parsedFileType = ParsedFileType.Unknown;

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
            var localPath = files[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(localPath))
            {
                AdvancedflashLog.Text += "\nInvalid selected file path.";
                return;
            }

            string path = File.Text = localPath;
            BusyFlash.IsBusy = true;

            var extension = Path.GetExtension(path);
            if (ScriptList.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                _parsedFileType = ParsedFileType.Script;
                AdvancedflashLog.Text += $"\nSkip script type: {Path.GetFileName(path)}";
                //检测后的脚本逻辑写这里

                TrySyncFileNamesFromUnpackFolder(path);
                BusyFlash.IsBusy = false;
                return;
            }

            try
            {
                var parsed = await TryParseAndPushToUiAsync(path);
                if (!parsed)
                {
                    AdvancedflashLog.Text += $"\nUnsupported format: {Path.GetFileName(path)}";
                }

                TrySyncFileNamesFromUnpackFolder(path);
            }
            catch (Exception ex)
            {
                AdvancedflashLog.Text += $"Error: {ex.Message}";
            }

            BusyFlash.IsBusy = false;
        }
    }

    private async Task<bool> TryParseAndPushToUiAsync(string path)
    {
        if (await TryParsePayloadAsync(path))
        {
            return true;
        }

        if (await TryParseSuperAsync(path))
        {
            return true;
        }

        _parsedFileType = ParsedFileType.Unknown;
        return false;
    }

    private async Task<bool> TryParsePayloadAsync(string path)
    {
        try
        {
            var parts = await PayloadParser.GetPartitionInfoAsync(path);
            if (parts == null || parts.Count == 0)
            {
                return false;
            }

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
            AdvancedflashLog.Text += $"Payload.bin Detected";
            _parsedFileType = ParsedFileType.Payload;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> TryParseSuperAsync(string path)
    {
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

                if (isSparse)
                {
                    using var sparseFile = SparseFile.FromImageFile(path);
                    using var inputStream = new SparseStream(sparseFile);
                    var metadataReader = new MetadataReader();
                    return metadataReader.ReadFromImageStream(inputStream);
                }

                using var rawInputStream = System.IO.File.OpenRead(path);
                var rawMetadataReader = new MetadataReader();
                return rawMetadataReader.ReadFromImageStream(rawInputStream);
            });

            if (_metadata == null || _metadata.Partitions.Count == 0)
            {
                return false;
            }

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

            _parsedFileType = ParsedFileType.Super;
            AdvancedflashLog.Text += $"Super Detected";
            return true;
        }
        catch
        {
            return false;
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
                vm.FalshPartModel.Clear();
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

                _parsedFileType = ParsedFileType.PayloadUrl;
                TrySyncFileNamesFromUnpackFolder(File.Text, true);
            }
            else
            {
                _parsedFileType = ParsedFileType.Unknown;
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
        try
        {
            var sourcePath = File.Text ?? string.Empty;
            var isUrl = Uri.IsWellFormedUriString(sourcePath, UriKind.Absolute);
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                AdvancedflashLog.Text += "\nInvalid image path.";
                return;
            }

            if (!isUrl && !System.IO.File.Exists(sourcePath))
            {
                AdvancedflashLog.Text += "\nInvalid image path.";
                return;
            }

            if (isUrl && _parsedFileType != ParsedFileType.PayloadUrl)
            {
                AdvancedflashLog.Text += "\nCurrent URL is not in payload_url mode.";
                return;
            }

            var vm = GetViewModel();
            var selectedParts = vm.FalshPartModel.Where(x => x.Select).ToList();
            if (selectedParts.Count == 0)
            {
                AdvancedflashLog.Text += "\nNo partition selected.";
                return;
            }

            var outputDir = GetUnpackOutputDir(sourcePath, isUrl);
            Directory.CreateDirectory(outputDir);

            switch (_parsedFileType)
            {
                case ParsedFileType.Payload:
                    await ExtractPayloadSelectedAsync(sourcePath, outputDir, selectedParts);
                    break;
                case ParsedFileType.Super:
                    await ExtractSuperSelectedAsync(sourcePath, outputDir, selectedParts);
                    break;
                case ParsedFileType.PayloadUrl:
                    await ExtractPayloadUrlSelectedAsync(sourcePath, outputDir, selectedParts);
                    break;
                default:
                    AdvancedflashLog.Text += "\nUnknown image type. Please re-open image file first.";
                    return;
            }

            var successCount = 0;
            foreach (var item in selectedParts)
            {
                var outPath = Path.Combine(outputDir, $"{item.Name}.img");
                if (System.IO.File.Exists(outPath))
                {
                    item.FileName = Path.GetFileName(outPath);
                    successCount++;
                }
            }

            AdvancedflashLog.Text += $"\nExtract finished: {successCount}/{selectedParts.Count} -> {outputDir}";
        }
        catch (Exception ex)
        {
            AdvancedflashLog.Text += $"Error: {ex.Message}";
        }
        finally
        {
            BusyFlash.IsBusy = false;
            ExtractSelect.IsEnabled = true;
        }
    }

    private static async Task ExtractPayloadSelectedAsync(string sourcePath, string outputDir, List<FalshPartModel> selectedParts)
    {
        var names = selectedParts
            .Select(x => x.Name)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var previousDirectory = Directory.GetCurrentDirectory();
        try
        {
            // PayloadHelper writes files to current directory, so switch temporarily.
            Directory.SetCurrentDirectory(outputDir);
            await PayloadParser.ExtractSelectedPartitionsAsync(sourcePath, names);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousDirectory);
        }
    }

    private static async Task ExtractSuperSelectedAsync(string sourcePath, string outputDir, List<FalshPartModel> selectedParts)
    {
        var names = selectedParts
            .Select(x => x.Name)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var selectedNameSet = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);

        await Task.Run(() =>
        {
            using var fs = System.IO.File.OpenRead(sourcePath);
            var magicBuf = new byte[4];
            var isSparse = false;
            if (fs.Read(magicBuf, 0, 4) == 4)
            {
                isSparse = BitConverter.ToUInt32(magicBuf, 0) == SparseFormat.SparseHeaderMagic;
            }

            if (isSparse)
            {
                using var sparseFile = SparseFile.FromImageFile(sourcePath);
                using var sparseStream = new SparseStream(sparseFile);
                ExtractSelectedSuperPartitions(sparseStream, outputDir, selectedNameSet);
            }
            else
            {
                using var rawStream = System.IO.File.OpenRead(sourcePath);
                ExtractSelectedSuperPartitions(rawStream, outputDir, selectedNameSet);
            }
        });
    }

    private static void ExtractSelectedSuperPartitions(Stream superStream, string outputDir, HashSet<string> selectedNameSet)
    {
        var metadataReader = new MetadataReader();
        var metadata = metadataReader.ReadFromImageStream(superStream);

        foreach (var partition in metadata.Partitions)
        {
            var name = partition.GetName();
            if (!selectedNameSet.Contains(name))
            {
                continue;
            }

            var outputPath = Path.Combine(outputDir, $"{name}.img");

            ulong totalSectors = 0;
            for (var i = 0; i < partition.NumExtents; i++)
            {
                totalSectors += metadata.Extents[(int)(partition.FirstExtentIndex + i)].NumSectors;
            }

            var totalSize = (long)totalSectors * MetadataFormat.LP_SECTOR_SIZE;
            using var outFs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            outFs.SetLength(totalSize);

            long currentOutOffset = 0;
            for (var i = 0; i < partition.NumExtents; i++)
            {
                var extent = metadata.Extents[(int)(partition.FirstExtentIndex + i)];
                var size = (long)extent.NumSectors * MetadataFormat.LP_SECTOR_SIZE;

                if (extent.TargetType == MetadataFormat.LP_TARGET_TYPE_LINEAR)
                {
                    var sourceOffset = (long)extent.TargetData * MetadataFormat.LP_SECTOR_SIZE;
                    superStream.Seek(sourceOffset, SeekOrigin.Begin);
                    outFs.Seek(currentOutOffset, SeekOrigin.Begin);
                    CopyStreamPart(superStream, outFs, size);
                }

                currentOutOffset += size;
            }
        }
    }

    private static void CopyStreamPart(Stream input, Stream output, long length)
    {
        var buffer = new byte[1024 * 1024];
        var remaining = length;
        while (remaining > 0)
        {
            var toRead = (int)Math.Min(buffer.Length, remaining);
            var read = input.Read(buffer, 0, toRead);
            if (read <= 0)
            {
                break;
            }

            output.Write(buffer, 0, read);
            remaining -= read;
        }
    }

    private static async Task ExtractPayloadUrlSelectedAsync(string sourceUrl, string outputDir, List<FalshPartModel> selectedParts)
    {
        var names = selectedParts
            .Select(x => x.Name)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        await PayloadParser.ExtractSelectedPartitionsFromUrlV2Async(sourceUrl, outputDir, names);
    }

    private static string GetUnpackOutputDir(string sourcePath, bool isUrl)
    {
        if (isUrl)
        {
            var uri = new Uri(sourcePath, UriKind.Absolute);
            var name = Path.GetFileName(Uri.UnescapeDataString(uri.AbsolutePath));
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "payload_url";
            }

            return Path.Combine(GetBackupRootDir(), $"{name}-unpack");
        }

        var parentDir = Path.GetDirectoryName(sourcePath) ?? Directory.GetCurrentDirectory();
        return Path.Combine(parentDir, $"{Path.GetFileName(sourcePath)}-unpack");
    }

    private static string GetBackupRootDir()
    {
        var toolboxDir = AppContext.BaseDirectory;
        var backupDir = Path.Combine(toolboxDir, "backup");
        Directory.CreateDirectory(backupDir);
        return backupDir;
    }

    private void TrySyncFileNamesFromUnpackFolder(string sourcePath, bool isUrl = false)
    {
        var unpackDir = GetUnpackOutputDir(sourcePath, isUrl);
        if (!Directory.Exists(unpackDir))
        {
            return;
        }

        var vm = GetViewModel();
        if (vm.FalshPartModel.Count == 0)
        {
            AdvancedflashLog.Text += $"\nDetected unpack folder: {unpackDir}";
            return;
        }

        var existingImages = new HashSet<string>(
            Directory.GetFiles(unpackDir, "*.img", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!),
            StringComparer.OrdinalIgnoreCase
        );

        var matched = 0;
        foreach (var item in vm.FalshPartModel)
        {
            var expectedImage = $"{item.Name}.img";
            if (existingImages.Contains(expectedImage))
            {
                item.FileName = expectedImage;
                matched++;
            }
        }

        if (matched > 0)
        {
            AdvancedflashLog.Text += $"\nAuto-matched {matched} extracted images from {Path.GetFileName(unpackDir)}";
        }
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