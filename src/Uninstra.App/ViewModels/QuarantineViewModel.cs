namespace Uninstra.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Uninstra.Core.Models;

public sealed partial class QuarantineViewModel : ObservableObject
{
    [ObservableProperty] private ObservableCollection<QuarantineManifest> _items = [];
    [ObservableProperty] private string _statusText = "Quarantine is empty";

    [RelayCommand]
    private async Task LoadAsync()
    {
        // Would load from IQuarantineService
        await Task.CompletedTask;
        StatusText = Items.Count == 0 ? "Quarantine is empty" : $"{Items.Count} quarantined items";
    }
}
