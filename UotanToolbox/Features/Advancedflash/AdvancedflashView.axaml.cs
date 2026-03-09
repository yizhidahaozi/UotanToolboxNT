using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.IO;
using UotanToolbox.Common;

namespace UotanToolbox.Features.Advancedflash;

public partial class AdvancedflashView : UserControl
{
    private static string GetTranslation(string key)
    {
        return FeaturesHelper.GetTranslation(key);
    }

    public AvaloniaList<string> ScriptList = [".bat", ".sh", ".xml", ".txt"];

    public AdvancedflashView()
    {
        InitializeComponent();
        ExportScr.ItemsSource = ScriptList;
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