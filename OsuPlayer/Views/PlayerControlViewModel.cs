using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using Avalonia.Media.Imaging;
using OsuPlayer.Data.OsuPlayer.Classes;
using OsuPlayer.Data.OsuPlayer.Enums;
using OsuPlayer.Extensions;
using OsuPlayer.Extensions.Bindables;
using OsuPlayer.IO.DbReader.DataModels;
using OsuPlayer.IO.Storage.Playlists;
using OsuPlayer.Modules.Audio;
using OsuPlayer.ViewModels;
using OsuPlayer.Windows;
using ReactiveUI;
using Splat;

namespace OsuPlayer.Views;

public class PlayerControlViewModel : BaseViewModel
{
    private readonly Bindable<IMapEntry?> _currentSong = new();

    private readonly Bindable<bool> _isPlaying = new();
    private readonly Bindable<RepeatMode> _isRepeating = new();
    private readonly Bindable<bool> _isShuffle = new();
    private readonly Bindable<double> _songLength = new();
    private readonly Bindable<double> _songTime = new();
    private readonly Bindable<double> _volume = new();

    public readonly Player Player;
    private Bitmap? _currentSongImage;
    private string _currentSongLength = "00:00";

    private string _currentSongTime = "00:00";

    private double _playbackSpeed;

    public PlayerControlViewModel(Player player, BassEngine bassEngine)
    {
        Player = player;

        _songTime.BindTo(bassEngine.ChannelPositionB);
        _songTime.BindValueChanged(d => this.RaisePropertyChanged(nameof(SongTime)));

        _songLength.BindTo(bassEngine.ChannelLengthB);
        _songLength.BindValueChanged(d => this.RaisePropertyChanged(nameof(SongLength)));

        _currentSong.BindTo(Player.CurrentSongBinding);
        _currentSong.BindValueChanged(d =>
        {
            this.RaisePropertyChanged(nameof(TitleText));
            this.RaisePropertyChanged(nameof(ArtistText));
            this.RaisePropertyChanged(nameof(SongText));
        });

        _volume.BindTo(bassEngine.VolumeB);
        _volume.BindValueChanged(d => this.RaisePropertyChanged(nameof(Volume)));

        _isPlaying.BindTo(Player.IsPlaying);
        _isPlaying.BindValueChanged(d => this.RaisePropertyChanged(nameof(IsPlaying)));

        _isRepeating.BindTo(Player.IsRepeating);
        _isRepeating.BindValueChanged(d => { this.RaisePropertyChanged(nameof(IsRepeating)); });

        _isShuffle.BindTo(Player.IsShuffle);
        _isShuffle.BindValueChanged(d => this.RaisePropertyChanged(nameof(IsShuffle)));

        Player.CurrentSongImage.BindValueChanged(d => CurrentSongImage = d.NewValue, true);

        Activator = new ViewModelActivator();
        this.WhenActivated(disposables => { Disposable.Create(() => { }).DisposeWith(disposables); });
    }

    public double Volume
    {
        get => _volume.Value;
        set
        {
            _volume.Value = value;
            this.RaisePropertyChanged();
        }
    }

    public bool IsShuffle
    {
        get => _isShuffle.Value;
        set
        {
            _isShuffle.Value = value;
            this.RaisePropertyChanged();
        }
    }

    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set
        {
            Player.SetPlaybackSpeed(value);
            this.RaiseAndSetIfChanged(ref _playbackSpeed, value);
            this.RaisePropertyChanged(nameof(CurrentSongLength));
        }
    }

    public double SongTime
    {
        get
        {
            this.RaisePropertyChanged(nameof(CurrentSongTime));
            return _songTime.Value;
        }
        set => _songTime.Value = value;
    }

    public string CurrentSongTime
    {
        get => TimeSpan.FromSeconds(_songTime.Value * (1 - PlaybackSpeed)).FormatTime();
        set => this.RaiseAndSetIfChanged(ref _currentSongTime, value);
    }

    public double SongLength
    {
        get
        {
            this.RaisePropertyChanged(nameof(CurrentSongLength));
            return _songLength.Value;
        }
    }

    public string CurrentSongLength
    {
        get => TimeSpan.FromSeconds(_songLength.Value * (1 - PlaybackSpeed)).FormatTime();
        set => this.RaiseAndSetIfChanged(ref _currentSongLength, value);
    }

    public bool IsPlaying => _isPlaying.Value;

    public string TitleText => _currentSong.Value?.Title ?? "No song is playing";

    public RepeatMode IsRepeating
    {
        get => _isRepeating.Value;
        set
        {
            _isRepeating.Value = value;
            this.RaisePropertyChanged();
        }
    }

    public string ArtistText => _currentSong.Value?.Artist ?? "please select from song list";

    public string SongText => $"{ArtistText} - {TitleText}";

    public Bitmap? CurrentSongImage
    {
        get => _currentSongImage;
        set
        {
            _currentSongImage?.Dispose();
            this.RaiseAndSetIfChanged(ref _currentSongImage, value);
        }
    }

    public IEnumerable<Playlist> Playlists => PlaylistManager.GetAllPlaylists().Where(x => x.Songs.Count > 0);

    public string ActivePlaylist => $"Active playlist: {Player.ActivePlaylist?.Name ?? "none"}";
}