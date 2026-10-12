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
        [Serializable] public class SetList { public string id, name; public List<SavedWord> words = new List<SavedWord>(); }
        [Serializable] public class Store { public int version = 1; public List<SetList> lists = new List<SetList>(); }

        static string FilePath => Path.Combine(Application.persistentDataPath, "liann-tones.setlists.v1.json");

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
            var list = store.lists.Find(l => l.name == listName);
            if (list == null) { list = new SetList { id = Guid.NewGuid().ToString("N"), name = listName }; store.lists.Add(list); }
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
