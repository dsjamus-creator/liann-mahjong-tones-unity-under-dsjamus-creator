using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using LiANN.MahjongTones.Core;

namespace LiANN.MahjongTones.Tests
{
    public class CoreTests
    {
        static Word W(string key, string hanzi, string english, int band = 1) =>
            new Word(new WordRecord { readingKey = key, hanzi = hanzi, english = english, hskBand = band });

        /// <summary>16 distinct test words incl. the 一般/一半 tone pair.</summary>
        static List<Word> Pool(int n = 20)
        {
            var list = new List<Word> { W("yi1ban1", "一般", "ordinary", 3), W("yi1ban4", "一半", "half"), W("ma1ma5", "妈妈", "mum") };
            for (int i = list.Count; i < n; i++) list.Add(W("ce" + (i % 4 + 1) + "shi" + (i / 4 % 4 + 1) + "zi" + (i / 16 + 1), "字" + i, "word " + i));
            return list;
        }

        // ---------- RNG must match the web edition (values from the JS reference implementation) ----------

        [TestCase("test", 2949673445u, new uint[] { 3079946387, 1488242721, 1149230787, 1078269322, 3765337915 })]
        [TestCase("daily:2026-10-12", 847902752u, new uint[] { 1515610117, 2565050785, 4289392419, 2212605493, 3304715385 })]
        [TestCase("一般", 2881981365u, new uint[] { 48100386, 1379143228, 14427463, 2868284378, 2964958010 })]
        public void RngMatchesJavaScriptReference(string seed, uint fnv, uint[] outputs)
        {
            Assert.AreEqual(fnv, Rng.Fnv1a(seed));
            var rng = new Rng(seed);
            foreach (uint expected in outputs) Assert.AreEqual(expected, rng.NextUInt());
        }

        // ---------- words and pinyin ----------

        [Test]
        public void ParsesReadingKeys()
        {
            var w = W("ma1ma5", "妈妈", "mum");
            CollectionAssert.AreEqual(new[] { "ma", "ma" }, w.Syllables);
            CollectionAssert.AreEqual(new[] { 1, 5 }, w.Tones);
            Assert.AreEqual("mama", w.Toneless);
            Assert.AreEqual("māma", w.Marked);
            Assert.AreEqual("lüxing", W("lv3xing2", "旅行", "to travel").Toneless);
            Assert.Throws<FormatException>(() => W("mama", "妈妈", "mum"));
        }

        [TestCase("lv", 3, "lǚ")]
        [TestCase("you", 2, "yóu")]
        [TestCase("gou", 3, "gǒu")]
        [TestCase("hui", 4, "huì")]
        [TestCase("liu", 2, "liú")]
        [TestCase("xue", 2, "xué")]
        [TestCase("ma", 5, "ma")]
        public void MarksTones(string syllable, int tone, string expected) => Assert.AreEqual(expected, Pinyin.Mark(syllable, tone));

        // ---------- layout geometry ----------

        [Test]
        public void FreeTileRule()
        {
            var starter = Layout.Starter16();
            var all = Enumerable.Repeat(true, 16).ToArray();
            Assert.IsTrue(starter.IsFree(0, all), "row end is free");
            Assert.IsFalse(starter.IsFree(1, all), "both sides blocked");
            all[0] = false;
            Assert.IsTrue(starter.IsFree(1, all), "left side opened");

            var small = Layout.Small24();
            var full = Enumerable.Repeat(true, 24).ToArray();
            Assert.IsFalse(small.IsFree(0, full) && small.IsFree(1, full) && small.IsFree(2, full) && small.IsFree(8, full), "middle base tiles start blocked");
            Assert.IsFalse(small.IsFree(1, full), "#1 (2,0) is under the top layer tile at (3,1)");
            Assert.IsTrue(small.IsFree(18, full), "top-layer end tile is free");
            Assert.IsFalse(small.IsFree(19, full), "top-layer middle tile is blocked both sides");
        }

        // ---------- generation ----------

        static bool Solvable(Layout layout, Game g)
        {
            var failed = new HashSet<long>();
            var occupied = g.Tiles.Select(t => t.OnBoard).ToArray();
            bool Solve(long mask)
            {
                if (mask == 0) return true;
                if (failed.Contains(mask)) return false;
                var free = layout.FreeSlots(occupied);
                var byWord = free.GroupBy(id => g.Tiles[id].WordIndex).Where(x => x.Count() == 2);
                foreach (var pair in byWord)
                {
                    var ids = pair.ToArray();
                    occupied[ids[0]] = occupied[ids[1]] = false;
                    bool ok = Solve(mask & ~(1L << ids[0]) & ~(1L << ids[1]));
                    occupied[ids[0]] = occupied[ids[1]] = true;
                    if (ok) return true;
                }
                failed.Add(mask);
                return false;
            }
            long start = 0;
            for (int i = 0; i < occupied.Length; i++) if (occupied[i]) start |= 1L << i;
            return Solve(start);
        }

        [Test]
        public void EveryBoardIsSolvable([Values("Starter", "Small")] string layoutName)
        {
            var pool = Pool(30);
            for (int seed = 0; seed < 1000; seed++)
            {
                var layout = Layout.ByName(layoutName);
                var g = new Game(layout, pool, PlayMode.Practice, "seed-" + seed);
                Assert.IsTrue(Solvable(layout, g), $"{layoutName} seed-{seed} unsolvable");
                foreach (int w in Enumerable.Range(0, g.Words.Count))
                {
                    Assert.AreEqual(1, g.Tiles.Count(t => t.WordIndex == w && t.Kind == TileKind.WordTile));
                    Assert.AreEqual(1, g.Tiles.Count(t => t.WordIndex == w && t.Kind == TileKind.MeaningTile));
                }
            }
        }

        [Test]
        public void BoardsAreDeterministicAndUnique()
        {
            var a = new Game(Layout.Small24(), Pool(30), PlayMode.Practice, "daily:2026-10-12");
            var b = new Game(Layout.Small24(), Pool(30), PlayMode.Practice, "daily:2026-10-12");
            CollectionAssert.AreEqual(a.Tiles.Select(t => (t.WordIndex, t.Kind)), b.Tiles.Select(t => (t.WordIndex, t.Kind)));
            CollectionAssert.AreEqual(a.Words.Select(w => w.ReadingKey), b.Words.Select(w => w.ReadingKey));
            Assert.AreEqual(a.Words.Count, a.Words.Select(w => w.Hanzi).Distinct().Count());
            Assert.AreEqual(a.Words.Count, a.Words.Select(w => w.English.ToLowerInvariant()).Distinct().Count());
        }

        [Test]
        public void TonePairGroupsComeFirstAndWhole()
        {
            var groups = new List<IList<string>> { new[] { "yi1ban1", "yi1ban4" } };
            var words = BoardGenerator.SelectWords(Pool(30), 8, new Rng("x"), groups);
            CollectionAssert.AreEqual(new[] { "yi1ban1", "yi1ban4" }, words.Take(2).Select(w => w.ReadingKey));
        }

        // ---------- play ----------

        static (int a, int b) SlotsOf(Game g, int word) =>
            (g.Tiles.First(t => t.OnBoard && t.WordIndex == word && t.Kind == TileKind.WordTile).SlotId,
             g.Tiles.First(t => t.OnBoard && t.WordIndex == word && t.Kind == TileKind.MeaningTile).SlotId);

        [Test]
        public void PerfectMatchScoresBandTimesTilesPlusToneBonus()
        {
            var g = new Game(Layout.Starter16(), Pool(), PlayMode.Practice, "score");
            int w = g.OpenPairs()[0];
            var (a, b) = SlotsOf(g, w);
            Assert.AreEqual(SelectOutcome.Selected, g.Select(a));
            Assert.AreEqual(SelectOutcome.AwaitingTones, g.Select(b));
            Assert.AreEqual(ToneOutcome.Cleared, g.SubmitTones(g.Words[w].Tones));
            int expected = Scoring.BandPoints(g.Words[w].HskBand) * 16 + 10 * g.Words[w].Tones.Length;
            Assert.AreEqual(expected, g.Score);
            Assert.AreEqual(14, g.TilesLeft);
            Assert.AreEqual(1, g.Streak);
        }

        [Test]
        public void WrongTonesKeepTilesAndPenaliseOnlyInChallenge([Values] PlayMode mode)
        {
            var g = new Game(Layout.Starter16(), Pool(), mode, "tone");
            int w = g.OpenPairs()[0];
            var (a, b) = SlotsOf(g, w);
            g.Select(a); g.Select(b);
            var wrong = g.Words[w].Tones.Select(t => t == 1 ? 2 : 1).ToArray();
            Assert.AreEqual(ToneOutcome.WrongTones, g.SubmitTones(wrong));
            Assert.AreEqual(16, g.TilesLeft);
            Assert.AreEqual(1, g.Results[w].WrongToneAttempts);
            Assert.AreEqual(0, g.Score, "score never below 0");
            g.Select(a); g.Select(b);
            g.SubmitTones(g.Words[w].Tones);
            Assert.IsFalse(g.Results[w].Perfect, "second-try matches are not perfect");
            int expected = mode == PlayMode.Zen ? 0 : Scoring.BandPoints(g.Words[w].HskBand) * 16;
            Assert.AreEqual(expected, g.Score);
        }

        [Test]
        public void WrongPairAndBlockedTiles()
        {
            var g = new Game(Layout.Starter16(), Pool(), PlayMode.Challenge, "pair");
            Assert.AreEqual(SelectOutcome.Blocked, g.Select(5));
            var open = g.OpenPairs();
            int w = open[0];
            var (a, _) = SlotsOf(g, w);
            int otherMeaning = g.Tiles.First(t => t.OnBoard && t.Kind == TileKind.MeaningTile && t.WordIndex != w && g.IsFree(t.SlotId)).SlotId;
            g.Select(a);
            Assert.AreEqual(SelectOutcome.WrongPair, g.Select(otherMeaning));
            Assert.AreEqual(1, g.Results[w].WrongPairAttempts);
        }

        [Test]
        public void UndoRestoresBoardAndScore()
        {
            var g = new Game(Layout.Small24(), Pool(30), PlayMode.Practice, "undo");
            int w = g.OpenPairs()[0];
            var (a, b) = SlotsOf(g, w);
            var before = g.Tiles.Select(t => (t.WordIndex, t.Kind, t.OnBoard)).ToArray();
            g.Select(a); g.Select(b); g.SubmitTones(g.Words[w].Tones);
            Assert.Greater(g.Score, 0);
            Assert.IsTrue(g.Undo());
            Assert.AreEqual(0, g.Score);
            Assert.AreEqual(24, g.TilesLeft);
            CollectionAssert.AreEqual(before, g.Tiles.Select(t => (t.WordIndex, t.Kind, t.OnBoard)).ToArray());
        }

        [Test]
        public void ChallengeLimitsHintsAndShuffles()
        {
            var g = new Game(Layout.Small24(), Pool(30), PlayMode.Challenge, "limits");
            for (int i = 0; i < 3; i++) Assert.IsNotNull(g.Hint());
            Assert.IsNull(g.Hint());
            Assert.IsTrue(g.Shuffle()); Assert.IsTrue(g.Shuffle()); Assert.IsFalse(g.Shuffle());
            Assert.IsTrue(Solvable(g.Layout, g), "shuffled boards stay solvable");
        }

        [Test]
        public void PlayingToTheEndWinsAndNeverDeadEnds([Values("Starter", "Small")] string layoutName)
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var g = new Game(Layout.ByName(layoutName), Pool(30), PlayMode.Practice, "play-" + seed);
                int guard = 0;
                while (!g.Won && guard++ < 100)
                {
                    var open = g.OpenPairs();
                    Assert.IsNotEmpty(open, "auto-reshuffle guarantees an open pair");
                    int w = open[open.Count - 1];   // pick greedily from the end to exercise reshuffles
                    var (a, b) = SlotsOf(g, w);
                    g.Select(a); g.Select(b);
                    Assert.AreEqual(ToneOutcome.Cleared, g.SubmitTones(g.Words[w].Tones));
                }
                Assert.IsTrue(g.Won);
                Assert.IsTrue(g.Results.All(r => r.Matched && r.Perfect));
            }
        }

        [Test]
        public void ChallengeUndoDoesNotRefundShufflesOrPenalties()
        {
            var g = new Game(Layout.Small24(), Pool(30), PlayMode.Challenge, "undo-refund");
            int w = g.OpenPairs()[0];
            var (a, b) = SlotsOf(g, w);
            g.Select(a); g.Select(b); g.SubmitTones(g.Words[w].Tones);
            int afterMatch = g.Score;
            Assert.IsTrue(g.Shuffle());
            Assert.IsNotNull(g.Hint());
            Assert.AreEqual(afterMatch - 20 - 15, g.Score);
            Assert.IsTrue(g.Undo());   // undoes the shuffle
            Assert.AreEqual(1, g.ShufflesUsed, "shuffles stay spent");
            Assert.AreEqual(afterMatch - 35, g.Score, "penalties stay deducted");
            Assert.IsTrue(g.Undo());   // undoes the match
            Assert.AreEqual(0, g.Score, "match points removed, never below 0");
            Assert.IsTrue(g.Shuffle()); Assert.IsFalse(g.Shuffle(), "only 2 shuffles in Challenge, even after undo");
        }

        [TestCase("sports", "sport")]
        [TestCase("how?", "how")]
        [TestCase("To Receive", "receive")]
        [TestCase("glass", "glass")]
        [TestCase("bus", "bus")]
        public void LabelKeysNormalise(string label, string key) => Assert.AreEqual(key, BoardGenerator.LabelKey(label));

        [Test]
        public void TonePairBoardsVary()
        {
            var pool = new List<Word> { W("yi1ban1", "一般", "ordinary"), W("yi1ban4", "一半", "half"), W("si1ji1", "司机", "driver"), W("si4ji4", "四季", "four seasons"),
                W("na3ge5", "哪个", "which"), W("na4ge5", "那个", "that one"), W("you2yong3", "游泳", "to swim"), W("you3yong4", "有用", "useful") };
            pool.AddRange(Pool(30));
            var groups = new List<IList<string>> { new[] { "yi1ban1", "yi1ban4" }, new[] { "si1ji1", "si4ji4" }, new[] { "na3ge5", "na4ge5" }, new[] { "you2yong3", "you3yong4" } };
            var firsts = new HashSet<string>();
            for (int s = 0; s < 20; s++) firsts.Add(BoardGenerator.SelectWords(pool, 4, new Rng("tp" + s), groups)[0].ReadingKey);
            Assert.Greater(firsts.Count, 1, "group order must depend on the seed");
        }
    }
}
