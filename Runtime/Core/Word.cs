using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace LiANN.MahjongTones.Core
{
    /// <summary>One vocabulary record as stored in words.json (public fields so Unity's JsonUtility can read it).</summary>
    [Serializable]
    public class WordRecord
    {
        public string readingKey;   // e.g. "yi1ban1"; digits are the lexical tones, 5 = neutral, v = ü
        public string entryId;      // dictionary entry id, e.g. "ude:..."
        public string senseId;      // dictionary sense id, e.g. "uds:..." (AlongSide game-events upload)
        public string hanzi;
        public string english;      // short tile label (proofread)
        public string fullGloss;
        public int hskBand;
        public string audio;        // audio file name without extension
        public string picture;      // icon id or empty
    }

    [Serializable]
    public class WordList
    {
        public int schemaVersion;
        public string source;
        public WordRecord[] words;
        public TonePairGroup[] tonePairs;   // same toneless pinyin, different hanzi (e.g. 一般 / 一半)
    }

    [Serializable]
    public class TonePairGroup
    {
        public string[] keys;
    }

    /// <summary>A word ready for play: syllables and tones parsed from the reading key.</summary>
    public sealed class Word
    {
        static readonly Regex Syllable = new Regex("([a-z]+)([1-5])", RegexOptions.CultureInvariant);

        public readonly WordRecord Record;
        public readonly string[] Syllables;   // toneless, with v kept as v
        public readonly int[] Tones;          // 1–4, 5 = neutral

        public Word(WordRecord record)
        {
            Record = record ?? throw new ArgumentNullException(nameof(record));
            var syl = new List<string>();
            var tones = new List<int>();
            int consumed = 0;
            foreach (Match m in Syllable.Matches(record.readingKey ?? ""))
            {
                if (m.Index != consumed) break;
                syl.Add(m.Groups[1].Value);
                tones.Add(m.Groups[2].Value[0] - '0');
                consumed = m.Index + m.Length;
            }
            if (syl.Count == 0 || consumed != (record.readingKey ?? "").Length)
                throw new FormatException("Bad reading key: " + record.readingKey);
            Syllables = syl.ToArray();
            Tones = tones.ToArray();
        }

        public string ReadingKey => Record.readingKey;
        public string Hanzi => Record.hanzi;
        public string English => Record.english;
        /// <summary>Lucide icon name for the meaning tile (spec D13), or empty when no icon fits the word.</summary>
        public string Picture => Record.picture ?? "";
        public int HskBand => Record.hskBand;

        /// <summary>Display name of an HSK band: "HSK 1" … "HSK 6", and "HSK 7–9" for band 7 (the pack keeps 7–9 as one band).</summary>
        public static string BandName(int hskBand) => hskBand >= 7 ? "HSK 7–9" : "HSK " + hskBand;

        /// <summary>Toneless pinyin for Tile A, e.g. "yiban", "lüxing".</summary>
        public string Toneless => string.Concat(Syllables).Replace('v', 'ü');

        /// <summary>Tone-marked pinyin, e.g. "yībān", "māma".</summary>
        public string Marked
        {
            get
            {
                var sb = new StringBuilder();
                for (int i = 0; i < Syllables.Length; i++) sb.Append(Pinyin.Mark(Syllables[i], Tones[i]));
                return sb.ToString();
            }
        }
    }

    public static class Pinyin
    {
        static readonly string[] A = { "a", "ā", "á", "ǎ", "à" }, E = { "e", "ē", "é", "ě", "è" },
            I = { "i", "ī", "í", "ǐ", "ì" }, O = { "o", "ō", "ó", "ǒ", "ò" }, U = { "u", "ū", "ú", "ǔ", "ù" },
            V = { "ü", "ǖ", "ǘ", "ǚ", "ǜ" };

        /// <summary>Place the tone mark by the standard rule (a/e first, then o in "ou", else the last vowel).</summary>
        public static string Mark(string syllable, int tone)
        {
            string s = syllable.Replace('v', 'ü');
            if (tone < 1 || tone > 4) return s;
            int at = s.IndexOf('a');
            if (at < 0) at = s.IndexOf('e');
            if (at < 0) at = s.IndexOf("ou", StringComparison.Ordinal);
            if (at < 0)
                for (int k = s.Length - 1; k >= 0; k--)
                    if ("iouü".IndexOf(s[k]) >= 0) { at = k; break; }
            if (at < 0) return s;
            string[] set = s[at] switch { 'a' => A, 'e' => E, 'i' => I, 'o' => O, 'u' => U, _ => V };
            return s.Substring(0, at) + set[tone] + s.Substring(at + 1);
        }
    }
}
