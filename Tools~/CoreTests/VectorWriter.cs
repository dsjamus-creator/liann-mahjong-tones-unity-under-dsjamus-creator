using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using LiANN.MahjongTones.Core;

namespace LiANN.MahjongTones.Tests
{
    /// <summary>
    /// Writes / checks Tests/Vectors/rules-test-vectors.json: seeded boards and a scripted play-through over the
    /// shipped HSK 1 words. The web edition must reproduce these exactly (spec §4.1, §6.1).
    /// Regenerate with: LIANN_WRITE_VECTORS=1 dotnet test
    /// </summary>
    public class VectorWriter
    {
        static object Build()
        {
            var json = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "words.json"));
            var list = JsonSerializer.Deserialize<WordList>(json, new JsonSerializerOptions { IncludeFields = true });
            var cases = new List<object>();
            var groups = list.tonePairs.Select(t => (IList<string>)t.keys).ToList();
            var runs = new List<(string layout, PlayMode mode, string seed, bool tonePairs)>();
            foreach (var layoutName in Layout.Names)
            {
                foreach (var mode in new[] { PlayMode.Practice, PlayMode.Challenge })
                foreach (var seed in new[] { "vector-1", "vector-2", "daily:2026-10-12" })
                    runs.Add((layoutName, mode, seed, false));
                foreach (var seed in new[] { "tones-1", "tones-2" }) runs.Add((layoutName, PlayMode.Practice, seed, true));
            }
            foreach (var (layoutName, mode, seed, tonePairs) in runs)
            {
                var pool = list.words.Where(w => tonePairs ? w.hskBand <= 3 : w.hskBand == 1).Select(w => new Word(w)).ToList();
                var g = new Game(Layout.ByName(layoutName), pool, mode, seed, tonePairs ? groups : null);
                var board = g.Tiles.Select(t => new { slot = t.SlotId, word = g.Words[t.WordIndex].ReadingKey, kind = t.Kind == TileKind.WordTile ? "A" : "B" }).ToList();
                var moves = new List<object>();
                int step = 0;
                while (!g.Won)
                {
                    int w = g.OpenPairs()[0];
                    int a = g.Tiles.First(t => t.OnBoard && t.WordIndex == w && t.Kind == TileKind.WordTile).SlotId;
                    int b = g.Tiles.First(t => t.OnBoard && t.WordIndex == w && t.Kind == TileKind.MeaningTile).SlotId;
                    g.Select(a); g.Select(b);
                    // Every third match first gets one wrong tone attempt, to cover penalties and streak resets.
                    bool wrongFirst = step % 3 == 2;
                    if (wrongFirst)
                    {
                        g.SubmitTones(g.Words[w].Tones.Select(t => t == 1 ? 2 : 1).ToArray());
                        g.Select(a); g.Select(b);
                    }
                    g.SubmitTones(g.Words[w].Tones);
                    moves.Add(new { word = g.Words[w].ReadingKey, wordSlot = a, meaningSlot = b, wrongToneFirst = wrongFirst, score = g.Score, streak = g.Streak, tilesLeft = g.TilesLeft, autoReshuffle = g.LastAutoReshuffle });
                    step++;
                }
                cases.Add(new { layout = layoutName, mode = mode.ToString(), seed, pool = tonePairs ? "words.json, hskBand <= 3, with tonePairs groups" : "words.json, hskBand == 1", words = g.Words.Select(x => x.ReadingKey), board, moves, finalScore = g.Score });
            }
            return new { schemaVersion = 1, spec = "PINYIN-MAHJONG-DESIGN-SPEC §4.1 + §6.1", script = "always take the lowest-numbered open pair (word order); every third match (0-based index % 3 == 2) first answers each tone wrong (1 -> 2, else -> 1)", cases };
        }

        [Test]
        public void VectorsMatchShippedFile()
        {
            string text = JsonSerializer.Serialize(Build(), new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            string target = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../Tests/Vectors/rules-test-vectors.json"));
            if (Environment.GetEnvironmentVariable("LIANN_WRITE_VECTORS") == "1") File.WriteAllText(target, text + "\n");
            Assert.AreEqual(File.ReadAllText(target).TrimEnd(), text.TrimEnd());
        }
    }
}
