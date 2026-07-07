namespace Uninstra.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Models;

public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly IHistoryRepository _repository;

    [ObservableProperty] private ObservableCollection<HistoryRecord> _records = [];
    [ObservableProperty] private string _statusText = "Ready";

    public HistoryViewModel(IHistoryRepository repository) => _repository = repository;

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            var all = await _repository.GetAllAsync();
            Records = new ObservableCollection<HistoryRecord>(all);
            StatusText = $"{all.Count} history records";
        }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
    }
}
