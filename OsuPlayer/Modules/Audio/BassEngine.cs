﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Avalonia.Threading;
using ManagedBass;
using ManagedBass.DirectX8;
using ManagedBass.Fx;
using OsuPlayer.Data.OsuPlayer.Classes;
using OsuPlayer.Extensions.Bindables;
using OsuPlayer.Extensions.Equalizer;
using OsuPlayer.IO.Storage.Config;

namespace OsuPlayer.Modules.Audio;

/// <summary>
/// Audioengine for the osu!player using <see cref="ManagedBass" />
/// </summary>
public sealed class BassEngine : INotifyPropertyChanged
{
    private const int KRepeatThreshold = 200;
    private readonly SyncProcedure _endTrackSyncProc;

    private readonly int[] _frq =
    {
        80, 125, 200, 300, 500, 1000, 2000, 4000, 8000, 16000
    };

    private readonly DispatcherTimer _positionTimer = new(DispatcherPriority.ApplicationIdle);
    private readonly SyncProcedure _repeatSyncProc;
    private int _activeStream;
    private bool _inChannelSet;
    private bool _inChannelTimerUpdate;
    private bool _inRepeatSet;
    private bool _isPlaying;
    private DXParamEQ _paramEq;
    private double _playbackSpeed;
    private TimeSpan _repeatStart;
    private TimeSpan _repeatStop;
    private int _repeatSyncId;
    private int _streamFx;
    public Bindable<double> ChannelLengthB = new();
    public Bindable<double> ChannelPositionB = new();
    public int SampleFrequency = 44100;
    public Bindable<double> VolumeB = new();

    public BassEngine()
    {
        Initialize();
        _endTrackSyncProc = EndTrack;
        _repeatSyncProc = RepeatCallback;
    }

    public int ActiveStreamHandle
    {
        get => _activeStream;
        private set
        {
            var oldValue = _activeStream;
            _activeStream = value;
            if (oldValue != _activeStream)
                NotifyPropertyChanged("ActiveStreamHandle");
        }
    }

    public int FxStream
    {
        get => _streamFx;
        private set
        {
            var oldValue = _streamFx;
            _streamFx = value;
            if (oldValue != _streamFx)
                NotifyPropertyChanged("FXStream");
        }
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            var oldValue = _isPlaying;
            _isPlaying = value;
            if (oldValue != _isPlaying)
                NotifyPropertyChanged("PlayState");
        }
    }

    private void PositionTimer_Tick(object sender, EventArgs e)
    {
        if (!IsPlaying) return;
        if (FxStream == 0)
        {
            ChannelPosition = 0;
        }
        else
        {
            _inChannelTimerUpdate = true;
            ChannelPosition = Bass.ChannelBytes2Seconds(FxStream, Bass.ChannelGetPosition(FxStream));
            _inChannelTimerUpdate = false;
        }
    }

    private int GetIndex(int frq)
    {
        switch (frq)
        {
            case 80:
                return 0;
            case 125:
                return 1;
            case 200:
                return 2;
            case 300:
                return 3;
            case 500:
                return 4;
            case 1000:
                return 5;
            case 2000:
                return 6;
            case 4000:
                return 7;
            case 8000:
                return 8;
            case 16000:
                return 9;
        }

        return -1;
    }

    private void SetValue(int index, double gain)
    {
        _paramEq.UpdateBand(index, gain);
        switch (index)
        {
            case 0:
                EqPresetStorage.F80 = gain;
                return;
            case 1:
                EqPresetStorage.F125 = gain;
                return;
            case 2:
                EqPresetStorage.F200 = gain;
                return;
            case 3:
                EqPresetStorage.F300 = gain;
                return;
            case 4:
                EqPresetStorage.F500 = gain;
                return;
            case 5:
                EqPresetStorage.F1000 = gain;
                return;
            case 6:
                EqPresetStorage.F2000 = gain;
                return;
            case 7:
                EqPresetStorage.F4000 = gain;
                return;
            case 8:
                EqPresetStorage.F8000 = gain;
                return;
            case 9:
                EqPresetStorage.F16000 = gain;
                return;
        }
    }

    public void SetSpeed(double speed)
    {
        Bass.ChannelSetAttribute(FxStream, ChannelAttribute.TempoFrequency,
            SampleFrequency * (1 + speed));
        _playbackSpeed = speed;
    }


    #region Player

    public TimeSpan SelectionBegin
    {
        get => _repeatStart;
        set
        {
            if (!_inRepeatSet)
            {
                _inRepeatSet = true;
                var oldValue = _repeatStart;
                _repeatStart = value;
                if (oldValue != _repeatStart)
                    NotifyPropertyChanged("SelectionBegin");
                SetRepeatRange(value, SelectionEnd);
                _inRepeatSet = false;
            }
        }
    }

    public TimeSpan SelectionEnd
    {
        get => _repeatStop;
        set
        {
            if (!_inChannelSet)
            {
                _inRepeatSet = true;
                var oldValue = _repeatStop;
                _repeatStop = value;
                if (oldValue != _repeatStop)
                    NotifyPropertyChanged("SelectionEnd");
                SetRepeatRange(SelectionBegin, value);
                _inRepeatSet = false;
            }
        }
    }

    private double ChannelLength
    {
        get => ChannelLengthB.Value;
        set => ChannelLengthB.Value = value;
    }

    private double ChannelPosition
    {
        get => ChannelPositionB.Value;
        set
        {
            if (_inChannelSet) return;

            _inChannelSet = true; // Avoid recursion
            //Math.Max(0, Math.Min(value, ChannelLength));
            if (!_inChannelTimerUpdate)
                Bass.ChannelSetPosition(FxStream, Bass.ChannelSeconds2Bytes(FxStream, value));
            if (Math.Abs(ChannelPositionB.Value - value) > 0.05)
            {
                ChannelPositionB.Value = value;
                NotifyPropertyChanged("ChannelPosition");
            }

            _inChannelSet = false;
        }
    }

    #endregion

    #region Callbacks

    private void EndTrack(int handle, int channel, int data, IntPtr user)
    {
        NotifyPropertyChanged("SongEnd");
        //Dispatcher.UIThread.Post(Locator.Current.GetService<Player>().NextSong);
    }

    private void RepeatCallback(int handle, int channel, int data, IntPtr user)
    {
        Dispatcher.UIThread.Post(() => ChannelPosition = SelectionBegin.TotalSeconds);
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler PropertyChanged;

    private void NotifyPropertyChanged(string info)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
    }

    #endregion

    #region Public Methods

    public void Stop()
    {
        ChannelPosition = (long) SelectionBegin.TotalSeconds;
        if (FxStream != 0)
        {
            Bass.ChannelStop(FxStream);
            Bass.ChannelSetPosition(FxStream, (long) ChannelPositionB.Value);
            IsPlaying = false;
        }
    }

    public void Pause()
    {
        if (Bass.ChannelPause(FxStream))
            IsPlaying = false;
    }

    public void Play()
    {
        IsPlaying = PlayCurrentStream();
    }

    public void PlayPause()
    {
        if (IsPlaying)
        {
            if (Bass.ChannelPause(FxStream))
                IsPlaying = false;
        }
        else
        {
            IsPlaying = PlayCurrentStream();
        }
    }

    public double Volume
    {
        get => VolumeB.Value;
        set
        {
            VolumeB.Value = value;
            try
            {
                if (FxStream != 0)
                    Bass.ChannelSetAttribute(FxStream, ChannelAttribute.Volume, (float) value / 100);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }
    }

    public bool OpenFile(string path)
    {
        Stop();

        if (FxStream != 0)
        {
            ClearRepeatRange();
            ChannelPosition = 0;
            Bass.StreamFree(FxStream);
        }

        if (File.Exists(path))
        {
            // Create Stream
            ActiveStreamHandle = Bass.CreateStream(path, 0, 0, BassFlags.Decode | BassFlags.Float | BassFlags.Prescan);
            FxStream = BassFx.TempoCreate(ActiveStreamHandle,
                BassFlags.FxFreeSource | BassFlags.Float | BassFlags.Loop);
            ChannelLength = Bass.ChannelBytes2Seconds(FxStream, Bass.ChannelGetLength(FxStream));
            if (FxStream != 0)
            {
                // Obtain the sample rate of the stream
                var info = Bass.ChannelGetInfo(FxStream);
                SampleFrequency = info.Frequency;
                //SetEqBands();
                var speed = SampleFrequency * (1 + _playbackSpeed);
                Bass.ChannelSetAttribute(FxStream, ChannelAttribute.TempoFrequency, speed);
                Bass.ChannelSetAttribute(FxStream, ChannelAttribute.Volume, (float) Volume / 100);

                using var config = new Config();
                SetDeviceInfo(config.Read().SelectedOutputDevice);


                // Set the stream to call Stop() when it ends.
                var syncHandle = Bass.ChannelSetSync(FxStream,
                    SyncFlags.End,
                    0,
                    _endTrackSyncProc,
                    IntPtr.Zero);

                if (syncHandle == 0)
                    throw new ArgumentException(@"Error establishing End Sync on file stream.", nameof(path));

                return true;
            }

            ActiveStreamHandle = 0;
            FxStream = 0;
        }

        return false;
    }

    public void ToggleEq(bool on)
    {
        if (on && _paramEq == null)
        {
            SetEqBands();
        }
        else if (!on && _paramEq != null)
        {
            _paramEq.Dispose();
            _paramEq = null;
        }
    }

    // /// <summary>
    // ///     Set the gain of one specific frequency band (from 80 to 16000 Hz)
    // /// </summary>
    // /// <param name="center">Frequency to set</param>
    // /// <param name="gain">Gain to set</param>
    // /// <returns></returns>
    // public void SetEQ(int center, double gain, EqualizerWindow window)
    // {
    //     if (window.PresetBox.SelectedIndex == 0) EqPreset.Custom.Gain[GetIndex(center)] = gain;
    //     if (!OsuPlayer.Config.ConfigStorage.IsEQEnabled || ParamEq == null) return;
    //     SetValue(GetIndex(center), gain);
    //     //return Bass.FXSetParameters(EQStream[GetIndex(center)], ParamEq);
    // }

    /// <summary>
    /// Sets all bands according to the parameter
    /// </summary>
    /// <param name="gain">10 double values from -15 to +15</param>
    /// <returns></returns>
    public void SetAllEq(double[] gain)
    {
        using var config = new Config();
        if (!config.Read().IsEqEnabled || _paramEq == null) return;
        for (var i = 0; i < gain.Length; i++)
            SetValue(i, gain[i]);
        //Bass.BASS_FXSetParameters(EQStream[i], ParamEq);
    }

    public List<DeviceInfo> GetDeviceInfos()
    {
        var list = new List<DeviceInfo>();
        for (var i = 0; i < Bass.DeviceCount; i++) list.Add(Bass.GetDeviceInfo(i));
        list.RemoveAt(0);
        return list;
    }

    public void SetDeviceInfo(int index)
    {
        if (index == -1)
        {
            var counter = 0;
            foreach (var deviceInfo in GetAudioDevices())
            {
                if (deviceInfo.IsDefault)
                {
                    Bass.CurrentDevice = counter + 1;
                    index = counter;
                    break;
                }

                counter++;
            }
        }

        var result = Bass.ChannelSetDevice(FxStream, index + 1);

        using var config = new Config();
        config.Read().SelectedOutputDevice = index;

        Console.WriteLine($"SET: {index} | {result} | {Bass.LastError}");
    }

    public IEnumerable<AudioDevice> GetAudioDevices()
    {
        foreach (var info in GetDeviceInfos()) yield return new AudioDevice(info);
    }

    public Collection<AudioDevice> AvailableAudioDevices;

    #endregion

    #region Init

    private void Initialize()
    {
        _positionTimer.Interval = TimeSpan.FromMilliseconds((double) 1000 / 60);
        _positionTimer.Tick += PositionTimer_Tick;
        _positionTimer.Start();

        ChannelPositionB.BindValueChanged(d => ChannelPosition = d.NewValue);
        ChannelLengthB.BindValueChanged(d => ChannelLength = d.NewValue);
        VolumeB.BindValueChanged(d => Volume = d.NewValue);

        AvailableAudioDevices = new Collection<AudioDevice>();

        var deviceInfos = GetDeviceInfos();

        var counter = 1;

        foreach (var deviceInfo in deviceInfos)
        {
            var audioDevice = new AudioDevice(deviceInfo);

            var a = Bass.Init(counter);

            if (a)
            {
                Console.WriteLine($"INIT: {audioDevice} | {a} | {Bass.LastError}");
                AvailableAudioDevices.Add(audioDevice);
            }
            else
            {
                Console.WriteLine($"INIT: {audioDevice} | {a} | {Bass.LastError}");
            }

            counter++;
        }

        //_mainWindow.ViewModel!.SettingsView.OutputDeviceComboboxItems = new ObservableCollection<AudioDevice>(GetAudioDevices());

        //SetDeviceInfo(OsuPlayer.Config.ConfigStorage.SelectedOutputDevice);
    }

    private void SetEqBands()
    {
        if (FxStream == 0) return;
        _paramEq = new DXParamEQ(FxStream);
        _paramEq.AddBand(80);
        _paramEq.AddBand(125);
        _paramEq.AddBand(200);
        _paramEq.AddBand(300);
        _paramEq.AddBand(500);
        _paramEq.AddBand(1000);
        _paramEq.AddBand(2000);
        _paramEq.AddBand(4000);
        _paramEq.AddBand(8000);
        _paramEq.AddBand(16000);
    }

    private void SetRepeatRange(TimeSpan startTime, TimeSpan endTime)
    {
        if (_repeatSyncId != 0)
            Bass.ChannelRemoveSync(FxStream, _repeatSyncId);

        if (endTime - startTime > TimeSpan.FromMilliseconds(KRepeatThreshold))
        {
            var channelLength = Bass.ChannelGetLength(FxStream);
            var repeatPos = endTime.TotalSeconds - 0.2;
            var endPosition = (long) (repeatPos / ChannelLengthB.Value * channelLength);
            _repeatSyncId = Bass.ChannelSetSync(FxStream,
                SyncFlags.Position,
                endPosition,
                _repeatSyncProc,
                IntPtr.Zero);
            ChannelPosition = (long) SelectionBegin.TotalSeconds;
        }
        else
        {
            ClearRepeatRange();
        }
    }

    private void ClearRepeatRange()
    {
        if (_repeatSyncId == 0) return;

        Bass.ChannelRemoveSync(FxStream, _repeatSyncId);
        _repeatSyncId = 0;
    }

    private bool PlayCurrentStream()
    {
        // Play Stream
        if (FxStream != 0 && Bass.ChannelPlay(FxStream)) return true;

        return false;
    }

    #endregion
}