using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using matrix.Models;
using matrix.Services;

namespace matrix.ViewModels;

public partial class FactsViewModel : ObservableObject
{
    private readonly LudocApiService _api;

    public ObservableCollection<FactEntry> Facts { get; } = [];

    [ObservableProperty] private string _searchQuery;
    [ObservableProperty] private string _newKey;
    [ObservableProperty] private string _newValue;
    [ObservableProperty] private bool _isAddVisible;
    [ObservableProperty] private string _statusText;

    public FactsViewModel(LudocApiService api)
    {
        _api = api;
        _searchQuery = "";
        _newKey = "";
        _newValue = "";
        _isAddVisible = false;
        _statusText = "";
        _ = LoadAllAsync();
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            await LoadAllAsync();
            return;
        }
        StatusText = "buscando...";
        var result = await _api.SearchFactsAsync(SearchQuery);
        Facts.Clear();
        if (result != null)
        {
            foreach (var f in result.Results) Facts.Add(f);
            StatusText = $"{result.Total} resultado(s)";
        }
        else
        {
            StatusText = "erro na busca";
        }
    }

    [RelayCommand]
    public async Task LoadAllAsync()
    {
        StatusText = "carregando...";
        var facts = await _api.GetFactsAsync(limit: 30);
        Facts.Clear();
        foreach (var f in facts) Facts.Add(f);
        StatusText = facts.Count > 0 ? $"{facts.Count} fato(s)" : "";
    }

    [RelayCommand]
    public async Task RecordAsync()
    {
        if (string.IsNullOrWhiteSpace(NewKey) || string.IsNullOrWhiteSpace(NewValue)) return;
        StatusText = "gravando...";
        var (id, ok) = await _api.RecordFactAsync(NewKey.Trim(), NewValue.Trim());
        if (ok)
        {
            NewKey = "";
            NewValue = "";
            IsAddVisible = false;
            StatusText = $"gravado: {id}";
            await LoadAllAsync();
        }
        else
        {
            StatusText = "erro ao gravar";
        }
    }

    [RelayCommand]
    public void ToggleAdd()
    {
        IsAddVisible = !IsAddVisible;
        if (!IsAddVisible)
        {
            NewKey = "";
            NewValue = "";
        }
    }
}
