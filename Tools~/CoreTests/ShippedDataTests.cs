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
            var pool = list.words.Select(r => new Word(r)).ToList();
            Assert.IsTrue(pool.All(w => w.Syllables.Length >= 2 && !string.IsNullOrWhiteSpace(w.English)));
            foreach (int band in new[] { 1, 2, 3 })
                for (int seed = 0; seed < 50; seed++)
                    Assert.DoesNotThrow(() => new Game(Layout.Small24(), pool.Where(x => x.HskBand == band), PlayMode.Practice, $"real-{band}-{seed}"));
        }
    }
}
