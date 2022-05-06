using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using OsuPlayer.Data.OsuPlayer.Classes;
using OsuPlayer.Data.OsuPlayer.Enums;
using OsuPlayer.IO.Storage.Config;
using OsuPlayer.Modules.Audio;
using OsuPlayer.Network;
using OsuPlayer.Network.Online;
using OsuPlayer.ViewModels;
using OsuPlayer.Windows;
using OsuPlayer.Extensions;
using ReactiveUI;

namespace OsuPlayer.Views;

public class SettingsViewModel : BaseViewModel
{
    public readonly Player Player;
    private string _osuLocation;
    private StartupSong _selectedStartupSong;
    private WindowTransparencyLevel _selectedTransparencyLevel;
    private string _settingsSearchQ;

    public MainWindow? MainWindow;
    private string _patchnotes;
    private ObservableCollection<string> _availableLanguages;
    private string _currentLanguage;

    public string CurrentLanguage
    {
        get => _currentLanguage;
        set => this.RaiseAndSetIfChanged(ref _currentLanguage, value);
    }

    public ObservableCollection<string> AvailableLanguages
    {
        get => _availableLanguages;
        set => this.RaiseAndSetIfChanged(ref _availableLanguages, value);
    }

    public string Patchnotes
    {
        get => _patchnotes;
        set => this.RaiseAndSetIfChanged(ref _patchnotes, value);
    }

    public SettingsViewModel(Player player)
    {
        var config = new Config();

        _selectedStartupSong = config.Container.StartupSong;
        _selectedTransparencyLevel = config.Container.TransparencyLevelHint;

        Player = player;

        Activator = new ViewModelActivator();
        this.WhenActivated(Block);
    }

    private async void Block(CompositeDisposable disposables)
    {
        Disposable.Create(() => { }).DisposeWith(disposables);

        Patchnotes = await GitHubUpdater.GetLatestPatchnotes(true);

        AvailableLanguages = CultureInfo.GetCultures(CultureTypes.NeutralCultures)
            .OrderBy(x => x.EnglishName)
            .Select(x => x.EnglishName)
            .ToObservableCollection();
    }

    public User? CurrentUser => ProfileManager.User;

    public string OsuLocation
    {
        get => $"osu! location: {_osuLocation}";
        set => this.RaiseAndSetIfChanged(ref _osuLocation, value);
    }

    public IEnumerable<WindowTransparencyLevel> WindowTransparencyLevels => Enum.GetValues<WindowTransparencyLevel>();

    public WindowTransparencyLevel SelectedTransparencyLevel
    {
        get => _selectedTransparencyLevel;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedTransparencyLevel, value);

            if (MainWindow == null) return;

            MainWindow.TransparencyLevelHint = value;
            using var config = new Config();
            config.Container.TransparencyLevelHint = value;
        }
    }

    public IEnumerable<StartupSong> StartupSongs => Enum.GetValues<StartupSong>();

    public StartupSong SelectedStartupSong
    {
        get => _selectedStartupSong;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedStartupSong, value);

            using var config = new Config();
            config.Container.StartupSong = value;
        }
    }

    public IEnumerable<SortingMode> SortingModes => Enum.GetValues<SortingMode>();

    public SortingMode SelectedSortingMode
    {
        get => Player.SortingModeBindable.Value;
        set
        {
            Player.SortingModeBindable.Value = value;
            this.RaisePropertyChanged();

            using var config = new Config();
            config.Container.SortingMode = value;
        }
    }

    public string SettingsSearchQ
    {
        get => _settingsSearchQ;
        set
        {
            var searchQs = value.Split(' ');

            foreach (var category in SettingsCategories)
                if (category is Grid settingsCat)
                {
                    var settingsPanel =
                        settingsCat.Children.FirstOrDefault(x => x.Name?.Contains(category.Name) ?? false);

                    if (settingsPanel is StackPanel stackPanel)
                    {
                        var settings = stackPanel.Children;

                        var categoryFound = searchQs.All(x =>
                            category.Name?.Contains(x, StringComparison.OrdinalIgnoreCase) ?? true);

                        if (categoryFound)
                        {
                            category.IsVisible = true;
                            foreach (var setting in settings) setting.IsVisible = true;

                            continue;
                        }

                        var foundAnySettings = false;
                        foreach (var setting in settings)
                        {
                            setting.IsVisible = searchQs.All(x =>
                                setting.Name?.Contains(x, StringComparison.OrdinalIgnoreCase) ?? false);
                            foundAnySettings = foundAnySettings || setting.IsVisible;
                        }

                        category.IsVisible = foundAnySettings;
                    }
                }

            this.RaiseAndSetIfChanged(ref _settingsSearchQ, value);
        }
    }

    public Avalonia.Controls.Controls SettingsCategories { get; set; }

    public ObservableCollection<AudioDevice> OutputDeviceComboboxItems { get; set; }
}