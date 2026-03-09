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
        TopLevel topLevel = TopLevel.GetTopLevel(this);
        System.Collections.Generic.IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open File",
            AllowMultiple = false,
            FileTypeFilter = new[] { CsvPicker }
        });
        if (files.Count >= 1)
        {
            CsvPath.Text = files[0].TryGetLocalPath();
            Global.BootPatchPath = CsvPath.Text;
        }
    }

    private async void SetBackupFolder(object sender, RoutedEventArgs args)
    {
        TopLevel topLevel = TopLevel.GetTopLevel(this);
        System.Collections.Generic.IReadOnlyList<IStorageFolder> files = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open Folder",
            AllowMultiple = false
        });
        if (files.Count >= 1)
        {
            if (FileHelper.TestPermission(files[0].TryGetLocalPath()))
            {
                BackPath.Text = files[0].TryGetLocalPath();
            }
            else
            {
                Global.MainDialogManager.CreateDialog().WithTitle(GetTranslation("Common_Error")).OfType(NotificationType.Error).WithContent(GetTranslation("Common_FolderNoPermission")).Dismiss().ByClickingBackground().TryShow();
            }
        }
    }
}