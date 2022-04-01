﻿using OsuPlayer.IO.DbReader.DataModels;
using OsuPlayer.IO.Storage.LazerModels.Beatmaps;
using Realms;

namespace OsuPlayer.IO.DbReader;

public class RealmReader
{
    public static async Task<List<IMapEntryBase>?> ReadRealm(string path)
    {
        var realmLoc = Path.Combine(path, "client.realm");

        var realmConfig = new RealmConfiguration(realmLoc)
        {
            SchemaVersion = 14,
            IsReadOnly = true
        };

        var minBeatMaps = new List<IMapEntryBase>();

        var realm = await Realm.GetInstanceAsync(realmConfig);
        var beatmaps = realm.DynamicApi.All("BeatmapSet").ToList().OfType<BeatmapSetInfo>();

        foreach (var beatmap in beatmaps)
        {
            minBeatMaps.Add(new RealmMapEntryBase
            {
                Artist = beatmap.Metadata.Artist,
                BeatmapChecksum = beatmap.Hash,
                BeatmapSetId = beatmap.OnlineID,
                Title = beatmap.Metadata.Title,
                TotalTime = (int)beatmap.MaxLength,
                Id = beatmap.ID
            });
        }

        realm.Dispose();

        return minBeatMaps;
    }
}