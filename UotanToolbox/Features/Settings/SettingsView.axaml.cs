using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SukiUI.Dialogs;
using UotanToolbox.Common;

namespace UotanToolbox.Features.Settings;

public partial class SettingsView : UserControl
{
    private static string GetTranslation(string key)
    {
        return FeaturesHelper.GetTranslation(key);
    }
    public SettingsView()
    {
        InitializeComponent();
    }

    private static FilePickerFileType CsvPicker { get; } = new("CSV File")
    {
        Patterns = new[] { "*.csv" },
        AppleUniformTypeIdentifiers = new[] { "*.csv" }
    };

    private async void OpenCSVFile(object sender, RoutedEventArgs args)
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
        {
            return;
        }

        System.Collections.Generic.IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open File",
            AllowMultiple = false,
            FileTypeFilter = new[] { CsvPicker }
        });
        if (files.Count >= 1)
        {
            CsvPath.Text = files[0].TryGetLocalPath() ?? string.Empty;
            Global.BootPatchPath = CsvPath.Text ?? string.Empty;
            UotanToolbox.Settings.Default.BootPatchPath = Global.BootPatchPath;
            UotanToolbox.Settings.Default.Save();
        }
    }

    private async void SetBackupFolder(object sender, RoutedEventArgs args)
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
        {
            return;
        }

        System.Collections.Generic.IReadOnlyList<IStorageFolder> files = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open Folder",
            AllowMultiple = false
        });
        if (files.Count >= 1)
        {
            string? localPath = files[0].TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(localPath) && FileHelper.TestPermission(localPath))
            {
                BackPath.Text = localPath;
                Global.backup_path = localPath;
                UotanToolbox.Settings.Default.BackupPath = Global.backup_path;
                UotanToolbox.Settings.Default.Save();
            }
            else
            {
                Global.MainDialogManager.CreateDialog().WithTitle(GetTranslation("Common_Error")).OfType(NotificationType.Error).WithContent(GetTranslation("Common_FolderNoPermission")).Dismiss().ByClickingBackground().TryShow();
            }
        }
    }
}