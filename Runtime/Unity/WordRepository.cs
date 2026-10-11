using System.Collections.Generic;
using System.Linq;
using LiANN.MahjongTones.Core;
using UnityEngine;

namespace LiANN.MahjongTones
{
    /// <summary>Loads the shipped word list and pronunciation clips from Resources/LiANNMahjongTones.</summary>
    public static class WordRepository
    {
        const string Root = "LiANNMahjongTones/";
        static List<Word> _words;
        static List<IList<string>> _tonePairs;
        static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();

        public static IReadOnlyList<Word> All { get { Load(); return _words; } }
        public static IList<IList<string>> TonePairs { get { Load(); return _tonePairs; } }

        static void Load()
        {
            if (_words != null) return;
            var text = Resources.Load<TextAsset>(Root + "words");
            if (text == null) { Debug.LogError("LiANN Mahjong Tones: Resources/" + Root + "words.json is missing."); _words = new List<Word>(); _tonePairs = new List<IList<string>>(); return; }
            var list = JsonUtility.FromJson<WordList>(text.text);
            _words = list.words.Select(r => new Word(r)).ToList();
            _tonePairs = (list.tonePairs ?? new TonePairGroup[0]).Select(g => (IList<string>)g.keys).ToList();
        }

        public static IEnumerable<Word> Band(int hskBand) => All.Where(w => w.HskBand == hskBand);

        /// <summary>Words for the tone-pair board: every word in a tone-pair group plus HSK 1–3 fillers.</summary>
        public static IEnumerable<Word> TonePairPool() => All.Where(w => w.HskBand <= 3);

        public static AudioClip Clip(Word word)
        {
            string key = word.Record.audio;
            if (string.IsNullOrEmpty(key)) return null;
            if (!Clips.TryGetValue(key, out var clip))
            {
                clip = Resources.Load<AudioClip>(Root + "Audio/" + key);
                Clips[key] = clip;
            }
            return clip;
        }
    }
}
