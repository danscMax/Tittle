using CommunityToolkit.Mvvm.ComponentModel;

namespace SeriousView.Shared;

/// <summary>
/// Base for all view models. <see cref="ObservableObject"/> supplies
/// INotifyPropertyChanged; derived types use [ObservableProperty]/[RelayCommand]
/// source generators (hence <c>partial</c>).
/// </summary>
public abstract class ViewModelBase : ObservableObject;
