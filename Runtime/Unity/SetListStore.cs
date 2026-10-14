using System;
using System.Collections.Generic;
using System.IO;
using LiANN.MahjongTones.Core;
using UnityEngine;

namespace LiANN.MahjongTones
{
    /// <summary>
    /// Local SetLists in the Pinyin Tiles word shape {py, entryId, hanzi, display, translation, gloss, savedAt} (spec §13),
    /// plus the dictionary senseId, so AccountClient can upload them to AlongSide as game events.
    /// </summary>
    public static class SetListStore
    {
        [Serializable] public class SavedWord { public string py, entryId, senseId, hanzi, display, translation, gloss, savedAt, flag, sourceApp; }
        // learner: "" for the signed-in adult, or a household child's profile id (One LiANN account C3). Each learner has
        // their own lists, so their saved words get their own event ids and never mix with an adult's.
        [Serializable] public class SetList { public string id, name, learner = ""; public List<SavedWord> words = new List<SavedWord>(); }
        [Serializable] public class Store { public int version = 1; public List<SetList> lists = new List<SetList>(); }

        static string FilePath => Path.Combine(Application.persistentDataPath, "liann-tones.setlists.v1.json");

        /// <summary>Who is playing: "" for the signed-in adult, or a child profile id. Set by AccountClient.</summary>
        public static string ActiveLearner { get; set; } = "";

        public static Store Load()
        {
            try { if (File.Exists(FilePath)) return JsonUtility.FromJson<Store>(File.ReadAllText(FilePath)) ?? new Store(); }
            catch (Exception e) { Debug.LogWarning("SetLists could not be read: " + e.Message); }
            return new Store();
        }

        /// <summary>Adds the round's words to the named list (created on first use). Returns how many were new.</summary>
        public static int Save(string listName, IEnumerable<WordResult> results)
        {
            var store = Load();
            string learner = ActiveLearner ?? "";
            var list = store.lists.Find(l => l.name == listName && (l.learner ?? "") == learner);
            if (list == null) { list = new SetList { id = Guid.NewGuid().ToString("N"), name = listName, learner = learner }; store.lists.Add(list); }
            int added = 0;
            foreach (var r in results)
            {
                var w = r.Word;
                if (list.words.Exists(x => x.py == w.ReadingKey)) continue;
                list.words.Add(new SavedWord
                {
                    py = w.ReadingKey, entryId = w.Record.entryId, senseId = w.Record.senseId, hanzi = w.Hanzi, display = w.Marked,
                    translation = w.English, gloss = w.Record.fullGloss, savedAt = DateTime.UtcNow.ToString("o"),
                    flag = r.WrongToneAttempts > 0 ? "missed-tone" : r.Hinted ? "hinted" : "new",
                    sourceApp = "liann-mahjong-tones",
                });
                added++;
            }
            File.WriteAllText(FilePath, JsonUtility.ToJson(store, true));
            return added;
        }
    }
}
