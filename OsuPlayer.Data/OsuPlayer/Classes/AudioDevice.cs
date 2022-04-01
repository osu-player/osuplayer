﻿using ManagedBass;

namespace OsuPlayer.Data.OsuPlayer.Classes;

/// <summary>
/// Wrapper class for <see cref="ManagedBass.DeviceInfo" />
/// </summary>
public sealed class AudioDevice
{
    public AudioDevice(DeviceInfo deviceInfo)
    {
        DeviceInfo = deviceInfo;
    }

    private DeviceInfo DeviceInfo { get; }
    public string DeviceName => DeviceInfo.Name;
    public bool IsEnabled => DeviceInfo.IsEnabled;
    public bool IsDefault => DeviceInfo.IsDefault;
    public bool IsInitialized => DeviceInfo.IsInitialized;
    public string Driver => DeviceInfo.Driver;
    public string DeviceToString => DeviceInfo.ToString();

    public override string ToString()
    {
        return DeviceName;
    }
}