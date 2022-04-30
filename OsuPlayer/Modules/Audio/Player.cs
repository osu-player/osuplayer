﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using DynamicData;
using LiveChartsCore.Defaults;
using OsuPlayer.Data.OsuPlayer.Classes;
using OsuPlayer.Data.OsuPlayer.Enums;
using OsuPlayer.Extensions;
using OsuPlayer.Extensions.Bindables;
using OsuPlayer.IO;
using OsuPlayer.IO.DbReader;
using OsuPlayer.IO.DbReader.DataModels;
using OsuPlayer.IO.Storage.Config;
using OsuPlayer.IO.Storage.Playlists;
using OsuPlayer.Network.API.ApiEndpoints;
using OsuPlayer.Network.Online;
using OsuPlayer.UI_Extensions;
using OsuPlayer.Windows;
using Splat;

namespace OsuPlayer.Modules.Audio;

/// <summary>
/// This class is a wrapper for our <see cref="BassEngine" />.
/// You can play, pause, stop and etc. from this class. Custom logic should also be implemented here
/// </summary>
public class Player
{
    private readonly BassEngine _bassEngine;
    private readonly Stopwatch _currentSongTimer;
    private readonly int?[] _shuffleHistory = new int?[10];

    public readonly Bindable<IMapEntry?> CurrentSongBinding = new();

    public readonly Bindable<Bitmap?> CurrentSongImage = new();

    public readonly Bindable<List<ObservableValue>?> GraphValues = new();

    public readonly Bindable<bool> IsPlaying = new();

    public readonly Bindable<RepeatMode> IsRepeating = new();

    public readonly Bindable<bool> IsShuffle = new();

    public readonly Bindable<Playlist?> SelectedPlaylist = new();

    public readonly Bindable<bool> SongsLoading = new();
    public readonly Bindable<SourceList<IMapEntryBase>> SongSource = new();

    public readonly Bindable<SortingMode> SortingModeBindable = new();

    private bool _isMuted;
    private double _oldVolume;

    private PlayState _playState;
    private int _shuffleHistoryIndex;

    // private int _shuffleHistoryIndex;

    public Player(BassEngine bassEngine)
    {
        _bassEngine = bassEngine;

        _bassEngine.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == "SongEnd")
                Dispatcher.UIThread.Post(NextSong);
        };

        SortingModeBindable.Value = new Config().Container.SortingMode;
        SortingModeBindable.BindValueChanged(d => UpdateSorting(d.NewValue), true);

        SongSource.Value = new SourceList<IMapEntryBase>();

        _currentSongTimer = new Stopwatch();
    }

    private IMapEntry? CurrentSong
    {
        get => CurrentSongBinding.Value;
        set
        {
            CurrentSongBinding.Value = value;

            CurrentIndex = SongSourceList!.FindIndex(x => x.BeatmapChecksum == value!.BeatmapChecksum);

            using var config = new Config();

            config.Container.LastPlayedSong = new LastPlayedSongModel(value!.BeatmapSetId, value.Title, value.Artist);

            // _mainWindow.ViewModel!.PlayerControl.CurrentSongImage = Task.Run(value!.FindBackground).Result;
        }
    }

    public List<IMapEntryBase>? SongSourceList => SongSource.Value.Items.ToList();

    public BindableArray<double> EqGains => _bassEngine.EqGains;

    private PlayState PlayState
    {
        get => _playState;
        set
        {
            IsPlaying.Value = value == PlayState.Playing;
            _playState = value;
        }
    }

    private int CurrentIndex { get; set; }

    public RepeatMode Repeat
    {
        get => IsRepeating.Value;
        set => IsRepeating.Value = value;
    }

    public Playlist? ActivePlaylist => ActivePlaylistId != default
        ? PlaylistManager.GetAllPlaylists().First(x => x.Id == ActivePlaylistId)
        : default;

    public Guid? ActivePlaylistId { get; set; }

    /// <summary>
    /// Imports the songs from either the osu!.db or client.realm using the <see cref="SongImporter" />. <br />
    /// Imported songs are stored in <see cref="SongSource" />. <br />
    /// Also plays the first song depending on the <see cref="StartupSong" /> config.
    /// <seealso cref="SongImporter.ImportSongsAsync" />
    /// </summary>
    public async Task ImportSongsAsync()
    {
        SongsLoading.Value = true;

        ConfigContainer configContainer;

        await using (var config = new Config())
        {
            var songEntries = await SongImporter.ImportSongsAsync((await config.ReadAsync()).OsuPath!);

            if (songEntries == null) return;

            SongSource.Value = songEntries.OrderBy(x => CustomSorter(x, config.Container.SortingMode)).ThenBy(x => x.Title).ToSourceList();

            SongsLoading.Value = false;

            if (SongSourceList == null || !SongSourceList.Any()) return;

            configContainer = config.Container;
            _bassEngine.Volume = config.Container.Volume;
        }

        switch (configContainer.StartupSong)
        {
            case StartupSong.FirstSong:
                await PlayAsync(SongSourceList[0]);
                break;
            case StartupSong.LastPlayed:
                await PlayLastPlayedSongAsync(configContainer);
                break;
            case StartupSong.RandomSong:
                await PlayAsync(SongSourceList[new Random().Next(SongSourceList.Count)]);
                break;
        }
    }

    /// <summary>
    /// Imports the collections found in the osu! collection.db and adds them as playlists
    /// </summary>
    public async Task ImportCollectionsAsync()
    {
        var config = new Config();
        var collections = await OsuCollectionReader.Read(config.Container.OsuPath!);

        if (collections != default && collections.Any())
        {
            RealmReader realmReader = null;
            Dictionary<string, int> beatmapHashes = null;

            if (SongSourceList?[0] is RealmMapEntryBase)
                realmReader = new RealmReader(config);
            else if (SongSourceList?[0] is DbMapEntryBase)
                beatmapHashes = await OsuDbReader.ReadAllDiffs(config.Container.OsuPath);

            foreach (var collection in collections)
            foreach (var hash in collection.BeatmapHashes)
                if (SongSourceList?[0] is RealmMapEntryBase)
                {
                    var setId = realmReader?.QueryBeatmap(x => x.MD5Hash == hash)?.BeatmapSet?.OnlineID ?? -1;
                    await PlaylistManager.AddSongToPlaylistAsync(collection.Name, setId);
                }
                else if (SongSourceList?[0] is DbMapEntryBase)
                {
                    var setId = beatmapHashes?.GetValueOrDefault(hash) ?? -1;
                    await PlaylistManager.AddSongToPlaylistAsync(collection.Name, setId);
                }

            Dispatcher.UIThread.Post(() => MessageBox.Show(Locator.Current.GetService<MainWindow>(), "Import successful. Have fun!", "Import complete!"));
            return;
        }

        Dispatcher.UIThread.Post(() => MessageBox.Show(Locator.Current.GetService<MainWindow>(), "There are no collections in osu!", "Import complete!"));
    }

    /// <summary>
    /// Plays the last played song read from the <see cref="ConfigContainer" /> and defaults to the
    /// first song in the <see cref="SongSourceList" /> if null
    /// </summary>
    /// <param name="config">optional parameter defaults to null. Used to avoid duplications of config instances</param>
    private async Task PlayLastPlayedSongAsync(ConfigContainer? config = null)
    {
        config ??= new Config().Container;

        if (config.LastPlayedSong == null)
        {
            await PlayAsync(null);
            return;
        }

        if (config.LastPlayedSong.SetId != -1)
        {
            await PlayAsync(GetMapEntryFromSetId(config.LastPlayedSong.SetId));
            return;
        }

        await PlayAsync(SongSourceList?.FirstOrDefault(x => x.Title == config.LastPlayedSong.Title && x.Artist == config.LastPlayedSong.Artist));
    }

    /// <summary>
    /// Updates the <see cref="SongSource" /> according to the <paramref name="sortingMode" />
    /// </summary>
    /// <param name="sortingMode">the <see cref="SortingMode" /> of the song list</param>
    private void UpdateSorting(SortingMode sortingMode = SortingMode.Title)
    {
        SongSource.Value = SongSource.Value.Items.OrderBy(x => CustomSorter(x, sortingMode)).ThenBy(x => x.Title).ToSourceList();
    }

    /// <summary>
    /// Picks the <see cref="IMapEntryBase" /> property to sort maps on
    /// </summary>
    /// <param name="map">the <see cref="IMapEntryBase" /> to be sorted</param>
    /// <param name="sortingMode">the <see cref="SortingMode" /> to decide how to sort</param>
    /// <returns>an <see cref="IComparable" /> containing the property to compare on</returns>
    public IComparable CustomSorter(IMapEntryBase map, SortingMode sortingMode)
    {
        switch (sortingMode)
        {
            case SortingMode.Title:
                return map.Title;
            case SortingMode.Artist:
                return map.Artist;
            case SortingMode.SetId:
                return map.BeatmapSetId;
            default:
                return null!;
        }
    }

    /// <summary>
    /// Sets the playback speed globally (including pitch)
    /// </summary>
    /// <param name="speed">The speed to set</param>
    public void SetPlaybackSpeed(double speed)
    {
        _bassEngine.SetSpeed(speed);
    }

    public void ToggleEq(bool on)
    {
        _bassEngine.IsEqEnabled = on;
    }

    /// <summary>
    /// Starts playing a song
    /// </summary>
    /// <param name="song">The song to play</param>
    /// <param name="playDirection">The direction we went in the playlist. Mostly used by the Next and Prev method</param>
    public async Task PlayAsync(IMapEntryBase? song, PlayDirection playDirection = PlayDirection.Forward)
    {
        if (SongSourceList == default || !SongSourceList.Any())
            return;

        if (song == default)
        {
            if ((await TryEnqueueSongAsync(SongSourceList[^1])).IsFaulted)
                await TryEnqueueSongAsync(SongSourceList![0]);
            return;
        }

        if (CurrentSongBinding.Value != null && Repeat != RepeatMode.SingleSong
                                             && (await new Config().ReadAsync()).IgnoreSongsWithSameNameCheckBox
                                             && CurrentSongBinding.Value.BeatmapChecksum == song.BeatmapChecksum)
            if ((await EnqueueSongFromDirectionAsync(playDirection)).IsFaulted)
                await TryEnqueueSongAsync(SongSourceList![^1]);

        if ((await TryEnqueueSongAsync(song)).IsFaulted)
            await TryEnqueueSongAsync(SongSourceList![^1]);
    }

    /// <summary>
    /// Enqueues a song with a given <paramref name="direction" />
    /// </summary>
    /// <param name="direction">a <see cref="PlayDirection" /> to indicate in which direction the next song should be</param>
    /// <returns>a <see cref="Task" /> from the enqueue try <seealso cref="TryEnqueueSongAsync" /></returns>
    private async Task<Task> EnqueueSongFromDirectionAsync(PlayDirection direction)
    {
        switch (direction)
        {
            case PlayDirection.Backwards:
            {
                for (var i = CurrentIndex - 1; i < SongSourceList?.Count; i--)
                {
                    if (SongSourceList[i].BeatmapChecksum == CurrentSongBinding.Value!.BeatmapChecksum) continue;

                    return await TryEnqueueSongAsync(SongSourceList[i]);
                }

                break;
            }
            case PlayDirection.Forward:
            {
                for (var i = CurrentIndex + 1; i < SongSourceList?.Count; i++)
                {
                    if (SongSourceList[i].BeatmapChecksum == CurrentSongBinding.Value!.BeatmapChecksum) continue;

                    return await TryEnqueueSongAsync(SongSourceList[i]);
                }

                break;
            }
        }

        return Task.FromException(new InvalidOperationException($"Direction {direction} is not valid"));
    }

    /// <summary>
    /// Enqueues a specific song to play
    /// </summary>
    /// <param name="song">a <see cref="IMapEntryBase" /> to play next</param>
    /// <returns>a <see cref="Task" /> with the resulting state</returns>
    private async Task<Task> TryEnqueueSongAsync(IMapEntryBase song)
    {
        if (SongSourceList == null || !SongSourceList.Any())
            return Task.FromException(new NullReferenceException($"{nameof(SongSourceList)} can't be null or empty"));

        var config = new Config();

        await config.ReadAsync();

        var fullMapEntry = await song.ReadFullEntry(config.Container.OsuPath!);

        if (fullMapEntry == default)
            return Task.FromException(new NullReferenceException());

        fullMapEntry.UseUnicode = config.Container.UseSongNameUnicode;

        //We put the XP update to an own try catch because if the API fails or is not available,
        //that the whole TryEnqueue does not fail
        try
        {
            if (CurrentSongBinding.Value != default)
                await UpdateXp();
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Could not update XP error => {e}");
        }

        try
        {
            _bassEngine.OpenFile(fullMapEntry.FullPath!);
            //_bassEngine.SetAllEq(Core.Instance.Config.Eq);
            _bassEngine.Play();
            PlayState = PlayState.Playing;

            _currentSongTimer.Restart();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.ToString());
            return Task.FromException(ex);
        }

        CurrentSong = fullMapEntry;

        //Same as update XP mentioned Above
        try
        {
            if (CurrentSongBinding.Value != default)
                await UpdateSongsPlayed(fullMapEntry.BeatmapSetId);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Could not update Songs Played error => {e}");
        }

        CurrentSongImage.Value = await fullMapEntry.FindBackground();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Updates the user xp on the api
    /// </summary>
    private async Task UpdateXp()
    {
        if (ProfileManager.User == default) return;

        var currentTotalXp = ProfileManager.User.TotalXp;

        _currentSongTimer.Stop();

        var time = (double) _currentSongTimer.ElapsedMilliseconds / 1000;

        var response = await ApiAsync.UpdateXpFromCurrentUserAsync(
            CurrentSongBinding.Value?.BeatmapChecksum ?? string.Empty,
            time,
            _bassEngine.ChannelLengthB.Value);

        if (response == default) return;

        ProfileManager.User = response;

        var xpEarned = response.TotalXp - currentTotalXp;

        var values = GraphValues.Value?.ToList() ?? new List<ObservableValue>();

        values.Add(new ObservableValue(xpEarned));

        GraphValues.Value = values;
    }

    public event PropertyChangedEventHandler UserChanged;

    /// <summary>
    /// Updates the songs played count of the user
    /// </summary>
    /// <param name="beatmapSetId">the beatmap set id of the map that was played</param>
    private async Task UpdateSongsPlayed(int beatmapSetId)
    {
        if (ProfileManager.User == default) return;

        var response = await ApiAsync.UpdateSongsPlayedForCurrentUserAsync(1, beatmapSetId);

        if (response == default) return;

        ProfileManager.User = response;

        UserChanged.Invoke(this, new PropertyChangedEventArgs("SongsPlayed"));
    }

    /// <summary>
    /// Toggles mute of the volume
    /// </summary>
    public void ToggleMute()
    {
        if (!_isMuted)
        {
            _oldVolume = _bassEngine.Volume;
            _bassEngine.Volume = 0;
            _isMuted = true;
        }
        else
        {
            _bassEngine.Volume = _oldVolume;
            _isMuted = false;
        }
    }

    /// <summary>
    /// Pauses the current song if playing or plays again if paused
    /// </summary>
    public void PlayPause()
    {
        if (PlayState == PlayState.Paused)
        {
            _bassEngine.Play();
            _currentSongTimer.Start();
            PlayState = PlayState.Playing;
        }
        else
        {
            _bassEngine.Pause();
            _currentSongTimer.Stop();
            PlayState = PlayState.Paused;
        }
    }

    /// <summary>
    /// Sets the playing state to Playing
    /// </summary>
    public void Play()
    {
        _bassEngine.Play();
        PlayState = PlayState.Playing;
    }

    /// <summary>
    /// Sets the playing state to Pause
    /// </summary>
    public void Pause()
    {
        _bassEngine.Pause();
        PlayState = PlayState.Paused;
    }

    /// <summary>
    /// Plays the next song in the list. Or the first if we are at the end
    /// </summary>
    public async void NextSong()
    {
        if (SongSourceList == null || !SongSourceList.Any())
            return;

        if (Repeat == RepeatMode.SingleSong)
        {
            await PlayAsync(SongSourceList[CurrentIndex]);
            return;
        }

        if (IsShuffle.Value)
        {
            await PlayAsync(await DoShuffle(ShuffleDirection.Forward));

            return;
        }

        // if (CurrentIndex + 1 == SongSourceList.Count)
        // {
        //     // if (OsuPlayer.Blacklist.IsSongInBlacklist(Songs[0]))
        //     // {
        //     //     CurrentIndex++;
        //     //     await NextSong();
        //     //     return;
        //     // }
        // }

        if (Repeat == RepeatMode.Playlist)
        {
            if (ActivePlaylist == default || ActivePlaylist.Songs.Count == 0)
            {
                // OsuPlayerMessageBox.Show(
                //    OsuPlayer.LanguageService.LoadControlLanguageWithKey("message.noPlaylistSelected"));
                Repeat = RepeatMode.NoRepeat;

                await PlayAsync(CurrentIndex == SongSourceList.Count - 1 ? SongSourceList[0] : SongSourceList[CurrentIndex + 1],
                    PlayDirection.Forward);

                return;
            }

            var currentPlaylistIndex = ActivePlaylist.Songs.IndexOf(CurrentSong!.BeatmapSetId);

            if (currentPlaylistIndex == ActivePlaylist.Songs.Count - 1)
                await PlayAsync(GetMapEntryFromSetId(ActivePlaylist.Songs[0]));
            else
                await PlayAsync(GetMapEntryFromSetId(ActivePlaylist.Songs[currentPlaylistIndex + 1]));

            return;
        }

        await PlayAsync(CurrentIndex == SongSourceList.Count - 1
            ? SongSourceList[0]
            : SongSourceList[CurrentIndex + 1]);
    }

    public event PropertyChangedEventHandler? PlaylistChanged;

    /// <summary>
    /// Plays the previous song or the last song if we are the beginning
    /// </summary>
    public async void PreviousSong()
    {
        if (SongSourceList == null || !SongSourceList.Any())
            return;

        if (_bassEngine.ChannelPositionB.Value > 3)
        {
            await TryEnqueueSongAsync(SongSourceList[CurrentIndex]);
            return;
        }

        if (IsShuffle.Value)
        {
            await PlayAsync(await DoShuffle(ShuffleDirection.Backwards), PlayDirection.Backwards);
            return;
        }

        // if (CurrentIndex - 1 == -1)
        // {
        //     // if (false) //OsuPlayer.Blacklist.IsSongInBlacklist(Songs[Songs.Count - 1]))
        //     // {
        //     //     CurrentIndex--;
        //     //     PreviousSong();
        //     //     return;
        //     // }
        // }

        if (Repeat == RepeatMode.Playlist)
        {
            if (ActivePlaylist == default || ActivePlaylist.Songs.Count == 0)
            {
                // OsuPlayerMessageBox.Show(
                //    OsuPlayer.LanguageService.LoadControlLanguageWithKey("message.noPlaylistSelected"));
                Repeat = RepeatMode.NoRepeat;

                await PlayAsync(CurrentIndex <= 0 ? SongSourceList[^1] : SongSourceList[CurrentIndex - 1], PlayDirection.Forward);

                return;
            }

            var currentPlaylistIndex = ActivePlaylist.Songs.IndexOf(CurrentSong!.BeatmapSetId);

            if (currentPlaylistIndex <= 0)
                await PlayAsync(GetMapEntryFromSetId(ActivePlaylist.Songs[^1]));
            else
                await PlayAsync(GetMapEntryFromSetId(ActivePlaylist.Songs[currentPlaylistIndex - 1]));

            return;
        }

        if (SongSourceList == null) return;
        await PlayAsync(CurrentIndex == 0 ? SongSourceList.Last() : SongSourceList[CurrentIndex - 1],
            PlayDirection.Backwards);
    }

    /// <summary>
    /// Gets the map entry from the beatmap set id
    /// </summary>
    /// <param name="setId">the beatmap set id to get the map from</param>
    /// <returns>an <see cref="IMapEntryBase" /> of the found map or null if it doesn't exist</returns>
    private IMapEntryBase? GetMapEntryFromSetId(int setId)
    {
        return SongSourceList!.FirstOrDefault(x => x.BeatmapSetId == setId);
    }

    /// <summary>
    /// Gets all Songs from a specific beatmapset ID
    /// </summary>
    /// <param name="setId">The beatmapset ID</param>
    /// <returns>A list of <see cref="IMapEntryBase" /></returns>
    public List<IMapEntryBase> GetMapEntriesFromSetId(ICollection<int> setId)
    {
        return SongSourceList!.Where(x => setId.Contains(x.BeatmapSetId)).ToList();
    }

    /// <summary>
    /// Triggers if the playlist got changed
    /// </summary>
    /// <param name="e"></param>
    public void TriggerPlaylistChanged(PropertyChangedEventArgs e)
    {
        PlaylistChanged?.Invoke(this, e);
    }

    #region Shuffle

    /// <summary>
    /// Implements the shuffle logic <seealso cref="GetNextShuffledIndex" />
    /// </summary>
    /// <param name="direction">the <see cref="ShuffleDirection" /> to shuffle to</param>
    /// <returns>a random/shuffled <see cref="IMapEntryBase" /></returns>
    private Task<IMapEntryBase> DoShuffle(ShuffleDirection direction)
    {
        if ((Repeat == RepeatMode.Playlist && ActivePlaylist == default) || CurrentSong == default || SongSourceList == default)
            return Task.FromException<IMapEntryBase>(new NullReferenceException());

        switch (direction)
        {
            case ShuffleDirection.Forward:
            {
                // Next index if not at array end
                if (_shuffleHistoryIndex < _shuffleHistory.Length - 1)
                {
                    GetNextShuffledIndex();
                }
                // Move array one down if at the top of the array
                else
                {
                    Array.Copy(_shuffleHistory, 1, _shuffleHistory, 0, _shuffleHistory.Length - 1);

                    _shuffleHistory[_shuffleHistoryIndex] = GenerateShuffledIndex();
                }

                break;
            }
            case ShuffleDirection.Backwards:
            {
                // Prev index if index greater than zero
                if (_shuffleHistoryIndex > 0)
                {
                    GetPreviousShuffledIndex();
                }
                // Move array one up if at the start of the array
                else
                {
                    Array.Copy(_shuffleHistory, 0, _shuffleHistory, 1, _shuffleHistory.Length - 1);

                    _shuffleHistory[_shuffleHistoryIndex] = GenerateShuffledIndex();
                }

                break;
            }
        }

        Debug.WriteLine("ShuffleHistory: " + _shuffleHistoryIndex);

        // ReSharper disable once PossibleInvalidOperationException
        var shuffleIndex = (int) _shuffleHistory[_shuffleHistoryIndex];

        return Task.FromResult(Repeat == RepeatMode.Playlist
            ? GetMapEntryFromSetId(ActivePlaylist!.Songs[shuffleIndex])
            : SongSourceList![shuffleIndex]);
    }

    /// <summary>
    /// Generates the next shuffled index in <see cref="_shuffleHistory" />
    /// <seealso cref="GenerateShuffledIndex" />
    /// </summary>
    private void GetNextShuffledIndex()
    {
        // If there is no "next" song generate new shuffled index
        if (_shuffleHistory[_shuffleHistoryIndex + 1] == null)
        {
            _shuffleHistory[_shuffleHistoryIndex] = Repeat == RepeatMode.Playlist
                ? ActivePlaylist?.Songs.IndexOf(CurrentSong!.BeatmapSetId)
                : CurrentIndex;
            _shuffleHistory[++_shuffleHistoryIndex] = GenerateShuffledIndex();
        }
        // There is a "next" song in the history
        else
        {
            // Check if next song index is in allowed boundary
            if (_shuffleHistory[_shuffleHistoryIndex + 1] < (Repeat == RepeatMode.Playlist
                    ? ActivePlaylist?.Songs.Count
                    : SongSourceList!.Count))
                _shuffleHistoryIndex++;
            // Generate new shuffled index when not
            else
                _shuffleHistory[++_shuffleHistoryIndex] = GenerateShuffledIndex();
        }
    }

    /// <summary>
    /// Generates the previous shuffled index in <see cref="_shuffleHistory" />
    /// <seealso cref="GenerateShuffledIndex" />
    /// </summary>
    private void GetPreviousShuffledIndex()
    {
        // If there is no "prev" song generate new shuffled index
        if (_shuffleHistory[_shuffleHistoryIndex - 1] == null)
        {
            _shuffleHistory[_shuffleHistoryIndex] = Repeat == RepeatMode.Playlist
                ? ActivePlaylist?.Songs.IndexOf(CurrentSong!.BeatmapSetId)
                : CurrentIndex;
            _shuffleHistory[--_shuffleHistoryIndex] = GenerateShuffledIndex();
        }
        // There is a "prev" song in history
        else
        {
            // Check if next song index is in allowed boundary
            if (_shuffleHistory[_shuffleHistoryIndex - 1] < (Repeat == RepeatMode.Playlist
                    ? ActivePlaylist?.Songs.Count
                    : SongSourceList!.Count))
                _shuffleHistoryIndex--;
            // Generate new shuffled index when not
            else
                _shuffleHistory[--_shuffleHistoryIndex] = GenerateShuffledIndex();
        }
    }

    /// <summary>
    /// Generates a new random/shuffled index of available songs in either the <see cref="SongSourceList" /> or
    /// <see cref="ActivePlaylist" /> songs
    /// </summary>
    /// <returns>the index of the new shuffled index</returns>
    private int GenerateShuffledIndex()
    {
        var rdm = new Random();
        var shuffleIndex = rdm.Next(0, Repeat == RepeatMode.Playlist
            ? ActivePlaylist!.Songs.Count
            : SongSourceList!.Count);

        while (shuffleIndex == (Repeat == RepeatMode.Playlist
                   ? ActivePlaylist?.Songs.IndexOf(CurrentSong!.BeatmapSetId)
                   : CurrentIndex)) // || OsuPlayer.Blacklist.IsSongInBlacklist(Songs[shuffleIndex]))
            shuffleIndex = rdm.Next(0, Repeat == RepeatMode.Playlist
                ? ActivePlaylist!.Songs.Count
                : SongSourceList!.Count);

        return shuffleIndex;
    }

    #endregion
}