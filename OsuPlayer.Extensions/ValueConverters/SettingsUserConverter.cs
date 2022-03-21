﻿using System.Globalization;
using Avalonia.Data.Converters;
using OsuPlayer.Network.Online;

namespace OsuPlayer.Extensions.ValueConverters;

public class SettingsUserConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var user = (User) value;

        return user?.Name ?? "Not logged in";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}