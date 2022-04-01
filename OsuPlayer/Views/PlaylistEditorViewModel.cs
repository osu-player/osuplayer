using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using DynamicData;
using OsuPlayer.Data.OsuPlayer.Classes;
using OsuPlayer.Extensions;
using OsuPlayer.IO.DbReader.DataModels;
using OsuPlayer.Modules.Audio;
using OsuPlayer.ViewModels;
using OsuPlayer.Windows;
using ReactiveUI;
using Splat;

namespace OsuPlayer.Views;

public class PlaylistEditorViewModel : BaseViewModel
{
    private readonly Player _player;
    private Playlist? _currentSelectedPlaylist;
    private bool _isDeletePlaylistPopupOpen;
    private bool _isNewPlaylistPopupOpen;
    private bool _isRenamePlaylistPopupOpen;
    private string _newPlaylistNameText;
    private SourceList<Playlist>? _playlists;
    private List<IMapEntryBase>? _selectedPlaylistItems;

    private List<IMapEntryBase>? _selectedSongListItems;

    public PlaylistEditorViewModel(Player player)
    {
        _player = player;

        Activator = new ViewModelActivator();

        this.WhenActivated(disposables =>
        {
            Disposable.Create(() => { }).DisposeWith(disposables);
        });

        SelectedPlaylistItems = new List<IMapEntryBase>();
        SelectedSongListItems = new List<IMapEntryBase>();
    }

    public bool IsDeletePlaylistPopupOpen
    {
        get => _isDeletePlaylistPopupOpen;
        set => this.RaiseAndSetIfChanged(ref _isDeletePlaylistPopupOpen, value);
    }

    public bool IsRenamePlaylistPopupOpen
    {
        get => _isRenamePlaylistPopupOpen;
        set => this.RaiseAndSetIfChanged(ref _isRenamePlaylistPopupOpen, value);
    }

    public bool IsNewPlaylistPopupOpen
    {
        get => _isNewPlaylistPopupOpen;
        set => this.RaiseAndSetIfChanged(ref _isNewPlaylistPopupOpen, value);
    }

    public string NewPlaylistNameText
    {
        get => _newPlaylistNameText;
        set => this.RaiseAndSetIfChanged(ref _newPlaylistNameText, value);
    }

    public Playlist? CurrentSelectedPlaylist
    {
        get => _currentSelectedPlaylist;
        set
        {
            _currentSelectedPlaylist = value;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(Playlists));
        }
    }

    public SourceList<Playlist> Playlists
    {
        get => _playlists;
        set => this.RaiseAndSetIfChanged(ref _playlists, value);
    }

    public List<IMapEntryBase> SongList => _player.SongSourceList!;

    public List<IMapEntryBase>? SelectedSongListItems
    {
        get => _selectedSongListItems;
        set => this.RaiseAndSetIfChanged(ref _selectedSongListItems, value);
    }

    public List<IMapEntryBase>? SelectedPlaylistItems
    {
        get => _selectedPlaylistItems;
        set => this.RaiseAndSetIfChanged(ref _selectedPlaylistItems, value);
    }
}