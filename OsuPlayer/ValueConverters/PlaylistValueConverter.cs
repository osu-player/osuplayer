﻿using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace OsuPlayer.ValueConverters;

//This ValueConverter is in the OsuPlayer Project, because we need access to the Audio class
//to convert the checksum to a MapEntry
public class PlaylistValueConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == default) return default;
        
        return Core.Instance.Player.GetMapEntriesFromChecksums((ICollection<string>) value);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}