﻿using System.Globalization;
using Avalonia.Media;
using OsuPlayer.Data.API.Enums;
using OsuPlayer.Data.API.Models.User;

namespace OsuPlayer.Network.Online;

public sealed class User : UserModel
{
    public User()
    {
        Role = UserRole.Unknown;
    }

    public string SongsPlayedString => $"{SongsPlayed.ToString("##,###", CultureInfo.InvariantCulture)} songs played";

    public string LevelAndTotalXpString =>
        $"Level {Level.ToString("##,###", CultureInfo.InvariantCulture)} | Total XP: {TotalXp.ToString("##,###", CultureInfo.InvariantCulture)}";

    public string LevelProgressString =>
        $"{Xp.ToString("##,###", CultureInfo.InvariantCulture)} XP / {GetXpNeededForNextLevel().ToString("##,###", CultureInfo.InvariantCulture)} XP";

    public Brush RoleColor => GetRoleColorBrush();
    public string RoleString => GetRoleString();

    public string DescriptionTitleString => $"{Name}'s Description";
    public string LevelString => $"Level {Level}";
    public string JoinDateString => $"joined {JoinDate.ToString("D", new CultureInfo("en-us"))}";

    public string TotalXpString =>
        TotalXp == 0 ? "0 XP" : $"{TotalXp.ToString("##,###", CultureInfo.InvariantCulture)} XP";

    public override string ToString()
    {
        return Name;
    }

    public int GetXpNeededForNextLevel()
    {
        return (int)Math.Round(0.04 * Math.Pow(Level, 3) + 0.8 * Math.Pow(Level, 2) + 2 * Level);
    }

    public static int GetXpNeededForNextLevel(int level)
    {
        return (int)Math.Round(0.04 * Math.Pow(level, 3) + 0.8 * Math.Pow(level, 2) + 2 * level);
    }

    public Brush GetRoleColorBrush()
    {
        switch (Role)
        {
            case UserRole.Developer:
                return UserColors.Developer;
            case UserRole.Tester:
                return UserColors.Tester;
            case UserRole.Translator:
                return UserColors.Translator;
            case UserRole.LegacyUser:
                return UserColors.LegacyUser;
            case UserRole.Donator:
                return UserColors.Donator;
            default:
                return UserColors.User;
        }
    }

    public string GetRoleString()
    {
        switch (Role)
        {
            case UserRole.Developer:
                return "Developer";
            case UserRole.Tester:
                return "Tester";
            case UserRole.Translator:
                return "Translator";
            case UserRole.Donator:
                return "Donator";
            default:
                return "User";
        }
    }
}