using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using OsuPlayer.Data.OsuPlayer.Classes;
using OsuPlayer.Extensions;
using OsuPlayer.IO.Storage.Playlists;
using OsuPlayer.ViewModels;
using ReactiveUI;

namespace OsuPlayer.Views;

public class PlaylistViewModel : BaseViewModel
{
    private ObservableCollection<Playlist> _playlists;
    private Playlist _selectedPlaylist;

    public PlaylistViewModel()
    {
        Activator = new ViewModelActivator();
        this.WhenActivated(disposables =>
        {
            Disposable.Create(() => { }).DisposeWith(disposables);

            Playlists = PlaylistManager.GetAllPlaylists().ToObservableCollection();

            if (Playlists.Count > 0 && SelectedPlaylist == default) SelectedPlaylist = Playlists[0];
        });
    }

    public ObservableCollection<Playlist> Playlists
    {
        get => _playlists;
        set => this.RaiseAndSetIfChanged(ref _playlists, value);
    }

    public Playlist? SelectedPlaylist
    {
        get => _selectedPlaylist;
        set => this.RaiseAndSetIfChanged(ref _selectedPlaylist, value);
    }
}