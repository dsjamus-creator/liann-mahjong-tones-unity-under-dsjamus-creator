using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using LiANN.MahjongTones.Core;

namespace LiANN.MahjongTones.Tests
{
    /// <summary>Checks the shipped words.json outside Unity (Unity reads it with JsonUtility at runtime).</summary>
    public class ShippedDataTests
    {
        [Test]
        public void ShippedWordListLoadsAndBuildsBoards()
        {
            var json = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "words.json"));
            var list = JsonSerializer.Deserialize<WordList>(json, new JsonSerializerOptions { IncludeFields = true });
            Assert.Greater(list.words.Length, 500);
            Assert.IsTrue(list.words.All(r => r.entryId.StartsWith("ude:") && r.senseId != null && r.senseId.StartsWith("uds:")), "every word has its dictionary entry and sense ids");
            var pool = list.words.Select(r => new Word(r)).ToList();
            Assert.IsTrue(pool.All(w => w.Syllables.Length >= 2 && !string.IsNullOrWhiteSpace(w.English)));
            var bands = pool.Select(w => w.HskBand).Distinct().OrderBy(b => b).ToArray();
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6, 7 }, bands, "HSK 1-6 plus 7-9 (stored as band 7)");
            foreach (int band in bands)
                for (int seed = 0; seed < 50; seed++)
                    foreach (var name in Layout.Names) Assert.DoesNotThrow(() => new Game(Layout.ByName(name), pool.Where(x => x.HskBand == band), PlayMode.Practice, $"real-{name}-{band}-{seed}"), $"{name} band {band} seed {seed}");
        }

        [Test]
        public void BandNamesCoverSevenToNineAsOneLevel()
        {
            Assert.AreEqual("HSK 1", Word.BandName(1));
            Assert.AreEqual("HSK 6", Word.BandName(6));
            Assert.AreEqual("HSK 7–9", Word.BandName(7));
        }

        [Test]
        public void TonePairsStayWithinHskOneToThree()
        {
            // The Tone pairs board draws from HSK 1-3, and the shared rule vectors (web edition) depend on these groups.
            var json = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "words.json"));
            var list = JsonSerializer.Deserialize<WordList>(json, new JsonSerializerOptions { IncludeFields = true });
            var band = list.words.ToDictionary(w => w.readingKey, w => w.hskBand);
            foreach (var g in list.tonePairs) foreach (var k in g.keys) Assert.LessOrEqual(band[k], 3, k);
        }

        [Test]
        public void HomophoneDenylistIsExcluded()
        {
            var json = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "words.json"));
            var keys = JsonSerializer.Deserialize<WordList>(json, new JsonSerializerOptions { IncludeFields = true }).words.Select(w => w.readingKey).ToHashSet();
            var deny = JsonDocument.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../data/homophone-denylist.json")));
            foreach (var p in deny.RootElement.EnumerateObject())
                if (!p.Name.StartsWith("_")) Assert.IsFalse(keys.Contains(p.Name), p.Name + " must not ship: its hanzi and label belong to different words");
        }

        [Test]
        public void FormerHomophonesShipWithTheirHskHeadword()
        {
            // Restored once the reading set carried the HSK word (pinyin-tiles-playtest PR #6). A regression to the
            // homophone (一生 for yi1sheng1, ...) would teach the wrong hanzi for the label.
            var json = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "words.json"));
            var words = JsonSerializer.Deserialize<WordList>(json, new JsonSerializerOptions { IncludeFields = true }).words.ToDictionary(w => w.readingKey);
            var expected = new Dictionary<string, string> {
                ["yi1sheng1"] = "医生", ["chu1zhong1"] = "初中", ["gong1yuan2"] = "公园", ["hui4yi4"] = "会议",
                ["jin4lai2"] = "进来", ["zhong1yu2"] = "终于", ["jian3cha2"] = "检查", ["bi4xu1"] = "必须" };
            foreach (var kv in expected)
            {
                Assert.IsTrue(words.ContainsKey(kv.Key), kv.Key + " should ship");
                Assert.AreEqual(kv.Value, words[kv.Key].hanzi, kv.Key);
                Assert.IsFalse(string.IsNullOrEmpty(words[kv.Key].senseId), kv.Key + " needs a senseId for AlongSide uploads");
            }
        }

        [Test]
        public void EveryMeaningLabelKeepsItsLongestWordOnOneLine()
        {
            var json = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "words.json"));
            var words = JsonSerializer.Deserialize<WordList>(json, new JsonSerializerOptions { IncludeFields = true }).words;
            foreach (var w in words)
            {
                float scale = MeaningLabel.FontScale(w.english);
                Assert.LessOrEqual(scale * MeaningLabel.RenderFactor * MeaningLabel.LongestWordEm(w.english), MeaningLabel.Available + 1e-4f, w.english);
                Assert.GreaterOrEqual(scale, 0.10f, w.english + " would be too small to read; shorten its label");
            }
        }
    
        [Test]
        public void PicturesAreConcreteOnlyAndEveryOneShips()
        {
            // Spec D13: pictures only where a fitting icon exists (no stand-ins), rendered PNG + committed .meta for each.
            string dir = TestContext.CurrentContext.TestDirectory;
            var words = JsonSerializer.Deserialize<WordList>(File.ReadAllText(Path.Combine(dir, "words.json")), new JsonSerializerOptions { IncludeFields = true }).words;
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(dir, "../../../../data/picture-map.json")));
            string pictures = Path.Combine(dir, "../../../../../Runtime/Resources/LiANNMahjongTones/Pictures");
            var pictured = words.Where(w => !string.IsNullOrEmpty(w.picture)).ToList();
            Assert.That(pictured.Count, Is.InRange(100, words.Length / 2), "concrete words only, so well under half the list");
            foreach (var w in words)
                Assert.AreEqual(map.TryGetValue(w.readingKey, out var icon) ? icon : "", w.picture ?? "", w.readingKey + ": words.json and picture-map.json disagree; run apply_pictures.py");
            foreach (var icon in pictured.Select(w => w.picture).Distinct())
            {
                Assert.IsTrue(File.Exists(Path.Combine(pictures, icon + ".png")), icon + ".png");
                Assert.IsTrue(File.Exists(Path.Combine(pictures, icon + ".png.meta")), icon + ".png.meta");
                Assert.IsTrue(File.Exists(Path.Combine(dir, "../../../../data/lucide", icon + ".svg")), icon + ".svg source");
            }
            var used = pictured.Select(w => w.picture).ToHashSet();
            foreach (var png in Directory.GetFiles(pictures, "*.png"))
                Assert.IsTrue(used.Contains(Path.GetFileNameWithoutExtension(png)), Path.GetFileName(png) + " is not used by any word");
            Assert.IsTrue(File.Exists(Path.Combine(dir, "../../../../data/lucide/LICENSE")), "Lucide licence ships with the sources");
        }
    }
}
