using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace LiANN.MahjongTones.Core
{
    /// <summary>One saved word, as AlongSide's game-events endpoint takes it (lwl-backend docs/PINYIN-GAME-SETLISTS.md).</summary>
    public sealed class GameEvent
    {
        public string EventId, SourceListId, SetListName, EntryId, SenseId, Headword, OccurredAt;
    }

    /// <summary>
    /// Builds idempotent "saved" events for AlongSide: the list and event ids are name-based UUIDs (version 5), so the
    /// same word in the same list always produces the same event and retries never duplicate (spec §13, D15).
    /// </summary>
    public static class SetListEvents
    {
        public const string SourceApp = "liann-mahjong-tones";
        public const int MaxBatch = 100;

        // Fixed namespace for this game's ids (a random UUID chosen once; never change it, or every id changes).
        static readonly Guid Namespace = new Guid("6f1b3c2e-58a4-4c6b-9e1d-2a7f4d9c0b51");

        /// <summary>RFC 4122 version 5 (SHA-1, name-based) UUID in lower-case canonical form.</summary>
        public static string NameUuid(string name)
        {
            byte[] ns = Namespace.ToByteArray();
            SwapToNetworkOrder(ns);
            byte[] nameBytes = Encoding.UTF8.GetBytes(name);
            byte[] hash;
            using (var sha1 = SHA1.Create())
            {
                var input = new byte[ns.Length + nameBytes.Length];
                Buffer.BlockCopy(ns, 0, input, 0, ns.Length);
                Buffer.BlockCopy(nameBytes, 0, input, ns.Length, nameBytes.Length);
                hash = sha1.ComputeHash(input);
            }
            var b = new byte[16];
            Array.Copy(hash, b, 16);
            b[6] = (byte)((b[6] & 0x0F) | 0x50);   // version 5
            b[8] = (byte)((b[8] & 0x3F) | 0x80);   // RFC 4122 variant
            var sb = new StringBuilder(36);
            for (int i = 0; i < 16; i++)
            {
                if (i == 4 || i == 6 || i == 8 || i == 10) sb.Append('-');
                sb.Append(b[i].ToString("x2"));
            }
            return sb.ToString();
        }

        static void SwapToNetworkOrder(byte[] g)
        {
            void Swap(int a, int c) { byte t = g[a]; g[a] = g[c]; g[c] = t; }
            Swap(0, 3); Swap(1, 2); Swap(4, 5); Swap(6, 7);
        }

        public static string ListId(string localListId) => NameUuid(SourceApp + "|list|" + localListId);

        public static string EventId(string localListId, string entryId, string senseId) =>
            NameUuid(SourceApp + "|saved|" + localListId + "|" + entryId + "|" + senseId);

        /// <summary>Events for every word in a list that has a dictionary identity. Words without one are skipped.</summary>
        public static List<GameEvent> ForList(string localListId, string listName,
            IEnumerable<(string entryId, string senseId, string headword, string savedAt)> words)
        {
            var list = new List<GameEvent>();
            string sourceListId = ListId(localListId);
            foreach (var (entryId, senseId, headword, savedAt) in words)
            {
                if (string.IsNullOrEmpty(entryId) || string.IsNullOrEmpty(senseId) || string.IsNullOrEmpty(headword)) continue;
                list.Add(new GameEvent
                {
                    EventId = EventId(localListId, entryId, senseId), SourceListId = sourceListId, SetListName = listName,
                    EntryId = entryId, SenseId = senseId, Headword = headword,
                    OccurredAt = NormaliseTime(savedAt),
                });
            }
            return list;
        }

        static string NormaliseTime(string t)
        {
            if (DateTime.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d))
                return d.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
            return DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        }

        /// <summary>Splits events into request bodies of at most <see cref="MaxBatch"/> events.</summary>
        public static List<string> Bodies(IList<GameEvent> events)
        {
            var bodies = new List<string>();
            for (int i = 0; i < events.Count; i += MaxBatch)
            {
                var sb = new StringBuilder("{\"events\":[");
                for (int j = i; j < Math.Min(events.Count, i + MaxBatch); j++)
                {
                    var e = events[j];
                    if (j > i) sb.Append(',');
                    sb.Append("{\"eventId\":").Append(Q(e.EventId))
                      .Append(",\"sourceApp\":").Append(Q(SourceApp))
                      .Append(",\"sourceListId\":").Append(Q(e.SourceListId))
                      .Append(",\"activity\":\"saved\"")
                      .Append(",\"setlistName\":").Append(Q(e.SetListName))
                      .Append(",\"wordIdentity\":{\"language\":\"zh\",\"entryId\":").Append(Q(e.EntryId))
                      .Append(",\"senseId\":").Append(Q(e.SenseId))
                      .Append(",\"headword\":").Append(Q(e.Headword)).Append('}')
                      .Append(",\"occurredAt\":").Append(Q(e.OccurredAt)).Append('}');
                }
                bodies.Add(sb.Append("]}").ToString());
            }
            return bodies;
        }

        /// <summary>JSON string literal; non-ASCII stays as UTF-8 text.</summary>
        public static string Q(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
