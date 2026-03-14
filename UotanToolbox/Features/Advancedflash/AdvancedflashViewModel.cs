using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using Material.Icons;
using UotanToolbox.Common;

namespace UotanToolbox.Features.Advancedflash;

public partial class AdvancedflashViewModel : MainPageBase
{
    private static string GetTranslation(string key)
    {
        return FeaturesHelper.GetTranslation(key);
    }

    [ObservableProperty]
    private AvaloniaList<FalshPartModel> falshPartModel = [];

    public AdvancedflashViewModel() : base(GetTranslation("Advancedflash_Name"), MaterialIconKind.CableData, -500)
    {

    }
}

public partial class FalshPartModel : ObservableObject
{
    [ObservableProperty]
    private bool select;

    [ObservableProperty]
    private bool selectDis = true;

    [ObservableProperty]
    private string command;

    [ObservableProperty]
    private string size;

    [ObservableProperty]
    private string name;

    [ObservableProperty]
    private string fileName;

    [ObservableProperty]
    private string fullFilePath;
}