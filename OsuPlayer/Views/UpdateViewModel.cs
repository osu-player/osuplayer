using System.Reactive.Disposables;
using OsuPlayer.ViewModels;
using ReactiveUI;

namespace OsuPlayer.Views;

public class UpdateViewModel : BaseViewModel
{
    private string _infoString;
    private string? _newVersion;
    private string? _updateUrl;

    public UpdateViewModel()
    {
        Activator = new ViewModelActivator();

        this.WhenActivated(disposables => { Disposable.Create(() => { }).DisposeWith(disposables); });
    }

    public string InfoString
    {
        get => $"Head to GitHub to download v{_newVersion}";
        set => this.RaiseAndSetIfChanged(ref _infoString, value);
    }

    public string? UpdateUrl
    {
        get => _updateUrl;
        set => this.RaiseAndSetIfChanged(ref _updateUrl, value);
    }

    public string? NewVersion
    {
        get => _newVersion;
        set => this.RaiseAndSetIfChanged(ref _newVersion, value);
    }
}