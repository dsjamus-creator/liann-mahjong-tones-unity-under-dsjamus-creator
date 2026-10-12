using System;

namespace LiANN.MahjongTones.Core
{
    public enum PlayMode { Practice, Challenge, Zen }

    /// <summary>Scoring v1, spec §6.1. Both editions use these exact numbers.</summary>
    public static class Scoring
    {
        public const int ToneBonusPerSyllable = 10;
        public const int StreakStep = 5, StreakCap = 25;
        public const int WrongPairPenalty = 5, WrongTonePenalty = 10, HintPenalty = 15, ShufflePenalty = 20, AutoReshufflePenalty = 20;
        public const int ChallengeHints = 3, ChallengeUndos = 3, ChallengeShuffles = 2;

        public static int BandPoints(int hskBand) => Math.Max(1, hskBand) + 1;   // 1→2, 2→3, 3→4 …

        public static int MatchPoints(int hskBand, int tilesOnBoardBeforeMatch) => BandPoints(hskBand) * tilesOnBoardBeforeMatch;

        public static int StreakBonus(int streak) => Math.Min(StreakCap, StreakStep * Math.Max(0, streak - 1));

        public static int CountdownSeconds(string layoutName) => layoutName switch
        {
            "Small" => 300,
            "Medium" => 480,
            "Large" => 900,
            _ => 180,
        };
    }
}
