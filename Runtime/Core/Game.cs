using System;
using System.Collections.Generic;

namespace LiANN.MahjongTones.Core
{
    public enum SelectOutcome { Blocked, Selected, Deselected, WrongPair, AwaitingTones, Ignored }
    public enum ToneOutcome { Cleared, WrongTones, NoPendingMatch }

    /// <summary>What a round produced for the SetList (spec §0, §8).</summary>
    public sealed class WordResult
    {
        public Word Word;
        public bool Matched, Hinted;
        public int WrongToneAttempts, WrongPairAttempts;
        public bool Perfect => Matched && !Hinted && WrongToneAttempts == 0;
    }

    public sealed class Tile
    {
        public int SlotId, WordIndex;
        public TileKind Kind;
        public bool OnBoard = true;
    }

    /// <summary>
    /// One round of LiANN Pinyin Mahjong Tones: select a free word tile and its free meaning tile,
    /// then enter the tones. Pure logic; the Unity layer animates the outcomes.
    /// </summary>
    public sealed class Game
    {
        public readonly Layout Layout;
        public readonly PlayMode Mode;
        public readonly string Seed;
        public readonly List<Word> Words;
        public readonly Tile[] Tiles;          // indexed by slot id
        public readonly WordResult[] Results;  // indexed by word index

        public int Score { get; private set; }
        public int Streak { get; private set; }
        public int? Selected { get; private set; }
        public int? PendingWord { get; private set; }
        public int HintsUsed { get; private set; }
        public int UndosUsed { get; private set; }
        public int ShufflesUsed { get; private set; }
        public int ShuffleCount { get; private set; }   // includes auto-reshuffles; drives the shuffle seed
        public int TilesLeft { get; private set; }
        public bool Won => TilesLeft == 0;

        readonly Stack<Snapshot> _undo = new Stack<Snapshot>();

        public Game(Layout layout, IEnumerable<Word> pool, PlayMode mode, string seed, IList<IList<string>> tonePairGroups = null)
        {
            Layout = layout; Mode = mode; Seed = seed;
            var rng = new Rng(seed);
            Words = BoardGenerator.SelectWords(pool, layout.Count / 2, rng, tonePairGroups);
            Tiles = new Tile[layout.Count];
            foreach (var p in BoardGenerator.NewBoard(layout, Words, rng))
                Tiles[p.SlotId] = new Tile { SlotId = p.SlotId, WordIndex = p.WordIndex, Kind = p.Kind };
            Results = new WordResult[Words.Count];
            for (int i = 0; i < Words.Count; i++) Results[i] = new WordResult { Word = Words[i] };
            TilesLeft = layout.Count;
        }

        bool[] Occupied()
        {
            var o = new bool[Tiles.Length];
            for (int i = 0; i < Tiles.Length; i++) o[i] = Tiles[i].OnBoard;
            return o;
        }

        public bool IsFree(int slotId) => Layout.IsFree(slotId, Occupied());

        /// <summary>Word indexes whose two tiles are both free right now, ascending.</summary>
        public List<int> OpenPairs()
        {
            var occ = Occupied();
            var freeA = new HashSet<int>(); var freeB = new HashSet<int>();
            foreach (var t in Tiles)
                if (t.OnBoard && Layout.IsFree(t.SlotId, occ)) (t.Kind == TileKind.WordTile ? freeA : freeB).Add(t.WordIndex);
            var open = new List<int>();
            for (int w = 0; w < Words.Count; w++) if (freeA.Contains(w) && freeB.Contains(w)) open.Add(w);
            return open;
        }

        void Penalise(int points) { if (Mode == PlayMode.Challenge) Score = Math.Max(0, Score - points); }

        public SelectOutcome Select(int slotId)
        {
            if (PendingWord.HasValue || slotId < 0 || slotId >= Tiles.Length || !Tiles[slotId].OnBoard) return SelectOutcome.Ignored;
            if (!IsFree(slotId)) return SelectOutcome.Blocked;
            if (Selected == slotId) { Selected = null; return SelectOutcome.Deselected; }
            if (!Selected.HasValue || Tiles[Selected.Value].Kind == Tiles[slotId].Kind)
            {
                Selected = slotId;
                return SelectOutcome.Selected;
            }
            Tile first = Tiles[Selected.Value], second = Tiles[slotId];
            if (first.WordIndex != second.WordIndex)
            {
                Results[first.WordIndex].WrongPairAttempts++;
                Penalise(Scoring.WrongPairPenalty);
                Selected = null;
                return SelectOutcome.WrongPair;
            }
            PendingWord = first.WordIndex;
            Selected = null;
            return SelectOutcome.AwaitingTones;
        }

        /// <summary>Close the tone picker without answering; the tiles stay.</summary>
        public void CancelTones() => PendingWord = null;

        /// <summary>Tones per syllable: 1–4, 5 = neutral (lexical tone, no sandhi).</summary>
        public ToneOutcome SubmitTones(IList<int> tones)
        {
            if (!PendingWord.HasValue) return ToneOutcome.NoPendingMatch;
            int w = PendingWord.Value;
            Word word = Words[w];
            bool right = tones != null && tones.Count == word.Tones.Length;
            for (int i = 0; right && i < word.Tones.Length; i++) right = tones[i] == word.Tones[i];
            PendingWord = null;
            if (!right)
            {
                Results[w].WrongToneAttempts++;
                Streak = 0;
                Penalise(Scoring.WrongTonePenalty);
                return ToneOutcome.WrongTones;
            }

            _undo.Push(new Snapshot(this));
            var r = Results[w];
            bool perfect = !r.Hinted && r.WrongToneAttempts == 0;
            int points = Scoring.MatchPoints(word.HskBand, TilesLeft);
            if (perfect)
            {
                Streak++;
                points += Scoring.ToneBonusPerSyllable * word.Tones.Length + Scoring.StreakBonus(Streak);
            }
            else Streak = 0;
            if (Mode != PlayMode.Zen) Score += points;
            r.Matched = true;
            foreach (var t in Tiles) if (t.OnBoard && t.WordIndex == w) { t.OnBoard = false; TilesLeft--; }
            if (TilesLeft > 0 && OpenPairs().Count == 0)
            {
                Reshuffle();
                Penalise(Scoring.AutoReshufflePenalty);
                LastAutoReshuffle = true;
            }
            else LastAutoReshuffle = false;
            return ToneOutcome.Cleared;
        }

        /// <summary>True when the last clear left no open pair and the board was reshuffled automatically.</summary>
        public bool LastAutoReshuffle { get; private set; }

        public bool CanHint => Mode != PlayMode.Challenge || HintsUsed < Scoring.ChallengeHints;
        public bool CanUndo => _undo.Count > 0 && (Mode != PlayMode.Challenge || UndosUsed < Scoring.ChallengeUndos);
        public bool CanShuffle => TilesLeft > 0 && (Mode != PlayMode.Challenge || ShufflesUsed < Scoring.ChallengeShuffles);

        /// <summary>Lowest open pair: (word index, word-tile slot, meaning-tile slot), or null.</summary>
        public (int word, int wordSlot, int meaningSlot)? Hint()
        {
            if (!CanHint) return null;
            var open = OpenPairs();
            if (open.Count == 0) return null;
            int w = open[0];
            int a = -1, b = -1;
            foreach (var t in Tiles)
                if (t.OnBoard && t.WordIndex == w) { if (t.Kind == TileKind.WordTile) a = t.SlotId; else b = t.SlotId; }
            HintsUsed++;
            Results[w].Hinted = true;
            Penalise(Scoring.HintPenalty);
            return (w, a, b);
        }

        public bool Undo()
        {
            if (!CanUndo) return false;
            int used = UndosUsed;
            _undo.Pop().Restore(this);
            UndosUsed = used + 1;
            return true;
        }

        public bool Shuffle()
        {
            if (!CanShuffle) return false;
            _undo.Push(new Snapshot(this));
            ShufflesUsed++;
            Reshuffle();
            Penalise(Scoring.ShufflePenalty);
            return true;
        }

        void Reshuffle()
        {
            ShuffleCount++;
            var slots = new List<int>();
            var words = new SortedSet<int>();
            foreach (var t in Tiles) if (t.OnBoard) { slots.Add(t.SlotId); words.Add(t.WordIndex); }
            var rng = new Rng(Seed + "#shuffle" + ShuffleCount);
            foreach (var p in BoardGenerator.Place(Layout, slots, new List<int>(words), rng))
            {
                Tiles[p.SlotId].WordIndex = p.WordIndex;
                Tiles[p.SlotId].Kind = p.Kind;
            }
            Selected = null;
        }

        sealed class Snapshot
        {
            readonly (int word, TileKind kind, bool on)[] _tiles;
            readonly (bool matched, bool hinted, int tone, int pair)[] _results;
            readonly int _score, _streak, _tilesLeft, _shufflesUsed, _shuffleCount;

            public Snapshot(Game g)
            {
                _tiles = new (int, TileKind, bool)[g.Tiles.Length];
                for (int i = 0; i < g.Tiles.Length; i++) _tiles[i] = (g.Tiles[i].WordIndex, g.Tiles[i].Kind, g.Tiles[i].OnBoard);
                _results = new (bool, bool, int, int)[g.Results.Length];
                for (int i = 0; i < g.Results.Length; i++)
                    _results[i] = (g.Results[i].Matched, g.Results[i].Hinted, g.Results[i].WrongToneAttempts, g.Results[i].WrongPairAttempts);
                _score = g.Score; _streak = g.Streak; _tilesLeft = g.TilesLeft;
                _shufflesUsed = g.ShufflesUsed; _shuffleCount = g.ShuffleCount;
            }

            public void Restore(Game g)
            {
                for (int i = 0; i < _tiles.Length; i++)
                {
                    g.Tiles[i].WordIndex = _tiles[i].word; g.Tiles[i].Kind = _tiles[i].kind; g.Tiles[i].OnBoard = _tiles[i].on;
                }
                // Learning flags are kept: undoing a match does not erase that a hint or a wrong tone happened.
                for (int i = 0; i < _results.Length; i++) g.Results[i].Matched = _results[i].matched;
                g.Score = _score; g.Streak = _streak; g.TilesLeft = _tilesLeft;
                g.ShufflesUsed = _shufflesUsed; g.ShuffleCount = _shuffleCount;   // hints used stay spent
                g.Selected = null; g.PendingWord = null; g.LastAutoReshuffle = false;
            }
        }
    }
}
