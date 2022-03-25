using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using OsuPlayer.IO.DbReader.DataModels;
using OsuPlayer.IO.Storage.Config;

namespace OsuPlayer.IO.DbReader;

/// <summary>
/// A <see cref="BinaryReader" /> to read the osu!.db to extract their beatmap data or to read from the collection.db
/// </summary>
public partial class DbReader : BinaryReader
{
    private DbReader(Stream input) : base(input)
    {
    }

    public static int OsuDbVersion;

    private byte[] _buf = new byte[512];

    /// <summary>
    /// Reads the osu!.db and skips duplicate beatmaps of one beatmap set
    /// </summary>
    /// <param name="osuPath">the osu full path</param>
    /// <returns> a <see cref="MinimalMapEntry"/> list</returns>
    public static async Task<List<MinimalMapEntry>?> ReadOsuDb(string osuPath)
    {
        var minBeatMaps = new List<MinimalMapEntry>();
        var dbLoc = Path.Combine(osuPath, "osu!.db");

        if (!File.Exists(dbLoc)) return null;

        await using var config = new Config();
        var unicode = (await config.ReadAsync()).UseSongNameUnicode;

        await using var file = File.OpenRead(dbLoc);
        using var reader = new DbReader(file);
        var ver = reader.ReadInt32();
        OsuDbVersion = ver;
        var flag = ver is >= 20160408 and < 20191107;

        reader.ReadInt32();
        reader.ReadBoolean();
        reader.ReadInt64();
        reader.ReadString();

        var mapCount = reader.ReadInt32();

        minBeatMaps.Capacity = mapCount;
        var prevId = -1;

        for (var i = 1; i < mapCount; i++)
        {
            if (flag)
                reader.ReadInt32(); //btlen

            if (prevId != -1)
            {
                var length = CalculateMapLength(reader, out var newSetId);
                if (prevId == newSetId)
                {
                    prevId = newSetId;
                    continue;
                }

                reader.BaseStream.Seek(-length, SeekOrigin.Current);
            }

            var minBeatMap = new MinimalMapEntry
            {
                DbOffset = reader.BaseStream.Position
            };

            ReadFromStreamMinimal(reader, osuPath, ref minBeatMap, out var curSetId);
            prevId = curSetId;
            minBeatMaps.Add(minBeatMap);
        }

        reader.ReadInt32(); //account rank

        await file.FlushAsync();
        reader.Dispose();
        return minBeatMaps;
    }

    /// <summary>
    /// Reads a osu!.db map entry and calculates the map length in bytes
    /// </summary>
    /// <param name="r">the current <see cref="DbReader"/> instance of the stream</param>
    /// <param name="setId">outputs a <see cref="int"/> of the beatmap set id</param>
    /// <returns>a <see cref="long"/> from the byte length of the current map</returns>
    private static long CalculateMapLength(DbReader r, out int setId)
    {
        var initOffset = r.BaseStream.Position;

        r.GetStringLen();
        if (OsuDbVersion >= 20121008)
        {
            r.GetStringLen();
        }

        r.GetStringLen();
        if (OsuDbVersion >= 20121008)
        {
            r.GetStringLen();
        }

        r.GetStringLen();
        r.GetStringLen();
        r.GetStringLen();
        r.GetStringLen();
        r.GetStringLen();
        r.BaseStream.Seek(15, SeekOrigin.Current);
        if (OsuDbVersion >= 20140609)
            r.BaseStream.Seek(16, SeekOrigin.Current);
        else
            r.BaseStream.Seek(4, SeekOrigin.Current);

        r.BaseStream.Seek(8, SeekOrigin.Current);
        if (OsuDbVersion >= 20140609)
        {
            r.ReadStarRating();
            r.ReadStarRating();
            r.ReadStarRating();
            r.ReadStarRating();
        }

        r.BaseStream.Seek(12, SeekOrigin.Current);
        var timingCnt = r.ReadInt32();
        r.BaseStream.Seek(timingCnt * 17, SeekOrigin.Current);
        r.BaseStream.Seek(4, SeekOrigin.Current);
        setId = r.ReadInt32();
        r.BaseStream.Seek(15, SeekOrigin.Current);
        r.GetStringLen();
        r.GetStringLen();
        r.BaseStream.Seek(2, SeekOrigin.Current);
        r.GetStringLen();
        r.BaseStream.Seek(10, SeekOrigin.Current);
        r.GetStringLen();
        if (OsuDbVersion < 20140609)
            r.BaseStream.Seek(20, SeekOrigin.Current);
        else
            r.BaseStream.Seek(18, SeekOrigin.Current);

        return r.BaseStream.Position - initOffset;
    }

    /// <summary>
    /// Reads the collection from the collection.db
    /// </summary>
    /// <param name="osuPath">the osu full path</param>
    /// <returns>a <see cref="Collection"/> list</returns>
    public static List<Collection>? ReadCollections(string osuPath)
    {
        var collections = new List<Collection>();
        var colLoc = Path.Combine(osuPath, "collection.db");

        if (!File.Exists(colLoc)) return null;

        using (DbReader reader = new(File.OpenRead(colLoc)))
        {
            reader.ReadInt32(); //osuVersion
            var num = reader.ReadInt32();

            for (var i = 0; i < num; i++) collections.Add(Collection.ReadFromReader(reader));
        }

        return collections;
    }

    /// <summary>
    /// Returns a ULEB128 length encoded string from the base stream
    /// </summary>
    /// <param name="ignore">the string will not be read and the base stream will skip it</param>
    /// <returns>a <see cref="string"/> containing the read string if string mark byte was 11 or an empty string if <paramref name="ignore"/> is true or the string mark byte was 0</returns>
    /// <exception cref="Exception">throws if the string mark byte is neither 0 nor 11</exception>
    public string ReadString(bool ignore = false)
    {
        switch (ReadByte())
        {
            case 0:
                return string.Empty;
            case 11:
                var strLen = Read7BitEncodedInt();
                if (!ignore)
                {
                    BaseStream.Read(_buf, 0, strLen);
                    return Encoding.UTF8.GetString(_buf, 0, strLen);
                }

                BaseStream.Seek(strLen, SeekOrigin.Current);
                return string.Empty;
            default:
                throw new Exception();
        }
    }

    /// <summary>
    /// Reads the length of a ULEB128 length encoded string
    /// </summary>
    /// <returns>an <see cref="int"/> representing the length of the string</returns>
    /// <exception cref="Exception">throws if the string mark byte is neither 0 nor 11</exception>
    private int GetStringLen()
    {
        switch (ReadByte())
        {
            case 0:
                return 0;
            case 11:
                var strLen = Read7BitEncodedInt();
                BaseStream.Seek(strLen, SeekOrigin.Current);
                return strLen;
            default:
                throw new Exception();
        }
    }

    /// <summary>
    /// Reads the star rating count and moves the base stream accordingly effectively skipping it
    /// </summary>
    public void ReadStarRating()
    {
        var count = ReadInt32();
        BaseStream.Seek(14 * count, SeekOrigin.Current);
    }

    /// <summary>
    /// Reads a <see cref="Int64"/> and converts it to UTC based time
    /// </summary>
    /// <returns>a <see cref="DateTime"/> converted from the read data</returns>
    public DateTime ReadDateTime()
    {
        return new DateTime(ReadInt64(), DateTimeKind.Utc);
    }
}