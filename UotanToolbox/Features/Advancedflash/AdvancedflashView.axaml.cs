using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SukiUI.Dialogs;
using SukiUI.Toasts;
using System;
using System.Threading.Tasks;
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
}