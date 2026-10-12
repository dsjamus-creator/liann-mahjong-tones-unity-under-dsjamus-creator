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
            foreach (int band in new[] { 1, 2, 3 })
                for (int seed = 0; seed < 50; seed++)
                    foreach (var name in Layout.Names) Assert.DoesNotThrow(() => new Game(Layout.ByName(name), pool.Where(x => x.HskBand == band), PlayMode.Practice, $"real-{name}-{band}-{seed}"), $"{name} band {band} seed {seed}");
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
    }
}
