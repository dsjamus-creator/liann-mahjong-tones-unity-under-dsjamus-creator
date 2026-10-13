using System;
using System.Collections.Generic;

namespace LiANN.MahjongTones.Core
{
    public enum TileKind { WordTile = 0, MeaningTile = 1 }   // A = hanzi + toneless pinyin, B = English (+ picture)

    public readonly struct Placement
    {
        public readonly int SlotId, WordIndex;
        public readonly TileKind Kind;
        public Placement(int slotId, int wordIndex, TileKind kind) { SlotId = slotId; WordIndex = wordIndex; Kind = kind; }
    }

    /// <summary>Word selection and backwards-removal placement, exactly as spec §4.1.</summary>
    public static class BoardGenerator
    {
        public const int MaxAttempts = 100;

        /// <summary>
        /// Label key for the one-label-per-board rule: lower case, punctuation removed, a leading "to " and a
        /// plural "s" dropped, so "sport"/"sports" or "how"/"how?" can't share a board (spec §4.1).
        /// </summary>
        public static string LabelKey(string label)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in (label ?? "").ToLowerInvariant())
                if (char.IsLetterOrDigit(c) || c == ' ') sb.Append(c);
            string k = string.Join(" ", sb.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            if (k.StartsWith("to ")) k = k.Substring(3);
            if (k.Length > 3 && k.EndsWith("s") && !k.EndsWith("ss")) k = k.Substring(0, k.Length - 1);
            return k;
        }

        /// <summary>
        /// Choose <paramref name="pairs"/> words. Pool is sorted by reading key, Fisher–Yates shuffled,
        /// tone-pair groups (if any) go first and whole; duplicates of hanzi or English label are skipped.
        /// </summary>
        public static List<Word> SelectWords(IEnumerable<Word> pool, int pairs, Rng rng, IList<IList<string>> tonePairGroups = null)
        {
            var sorted = new List<Word>(pool);
            sorted.Sort((a, b) => string.CompareOrdinal(a.ReadingKey, b.ReadingKey));
            for (int i = sorted.Count - 1; i >= 1; i--)
            {
                int j = rng.RandInt(i + 1);
                (sorted[i], sorted[j]) = (sorted[j], sorted[i]);
            }

            var chosen = new List<Word>();
            var hanzi = new HashSet<string>();
            var english = new HashSet<string>(StringComparer.Ordinal);
            bool Fits(Word w) => !hanzi.Contains(w.Hanzi) && !english.Contains(LabelKey(w.English));
            void Take(Word w) { chosen.Add(w); hanzi.Add(w.Hanzi); english.Add(LabelKey(w.English)); }

            if (tonePairGroups != null)
            {
                var byKey = new Dictionary<string, Word>();
                foreach (var w in sorted) byKey[w.ReadingKey] = w;
                // Groups in seeded random order, so tone-pair boards vary from game to game.
                var groups = new List<IList<string>>(tonePairGroups);
                for (int i = groups.Count - 1; i >= 1; i--)
                {
                    int j = rng.RandInt(i + 1);
                    (groups[i], groups[j]) = (groups[j], groups[i]);
                }
                foreach (var group in groups)
                {
                    var members = new List<Word>();
                    foreach (var key in group) if (byKey.TryGetValue(key, out var w)) members.Add(w);
                    if (members.Count != group.Count || chosen.Count + members.Count > pairs) continue;
                    bool ok = true;
                    var gh = new HashSet<string>(); var ge = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var w in members) if (!Fits(w) || !gh.Add(w.Hanzi) || !ge.Add(LabelKey(w.English))) ok = false;
                    if (ok) foreach (var w in members) Take(w);
                }
            }
            foreach (var w in sorted)
            {
                if (chosen.Count == pairs) break;
                if (!chosen.Contains(w) && Fits(w)) Take(w);
            }
            if (chosen.Count < pairs) throw new InvalidOperationException($"Only {chosen.Count} distinct words for {pairs} pairs.");
            return chosen;
        }

        /// <summary>
        /// Place word k (k = 0…) on the k-th pair removed from a full set of <paramref name="slotIds"/>.
        /// Every pair is free when removed, so playing the removals forward always clears the board.
        /// </summary>
        public static List<Placement> Place(Layout layout, IList<int> slotIds, IList<int> wordIndexes, Rng rng)
        {
            if (slotIds.Count != wordIndexes.Count * 2) throw new ArgumentException("Need two slots per word.");
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var occupied = new bool[layout.Count];
                foreach (int id in slotIds) occupied[id] = true;
                var result = new List<Placement>();
                bool stuck = false;
                for (int k = 0; k < wordIndexes.Count; k++)
                {
                    List<int> free = layout.FreeSlots(occupied);
                    if (free.Count < 2) { stuck = true; break; }
                    int n = free.Count;
                    int i = rng.RandInt(n);
                    int j = rng.RandInt(n - 1);
                    if (j >= i) j++;
                    int first = free[i], second = free[j];
                    occupied[first] = occupied[second] = false;
                    bool aFirst = rng.RandInt(2) == 0;
                    result.Add(new Placement(aFirst ? first : second, wordIndexes[k], TileKind.WordTile));
                    result.Add(new Placement(aFirst ? second : first, wordIndexes[k], TileKind.MeaningTile));
                }
                if (!stuck)
                {
                    result.Sort((a, b) => a.SlotId.CompareTo(b.SlotId));
                    return result;
                }
            }
            throw new InvalidOperationException("Could not place the board in " + MaxAttempts + " attempts.");
        }

        /// <summary>Full new board: select words then place them on every slot of the layout.</summary>
        public static List<Placement> NewBoard(Layout layout, List<Word> words, Rng rng)
        {
            var slots = new List<int>();
            for (int i = 0; i < layout.Count; i++) slots.Add(i);
            var indexes = new List<int>();
            for (int i = 0; i < words.Count; i++) indexes.Add(i);
            return Place(layout, slots, indexes, rng);
        }
    }
}
