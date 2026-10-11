using System;
using System.Collections.Generic;

namespace LiANN.MahjongTones.Core
{
    /// <summary>A board position in half-tile units; a tile covers a 2×2 footprint (spec §4.1).</summary>
    public readonly struct Slot
    {
        public readonly int Id, X, Y, Z;
        public Slot(int id, int x, int y, int z) { Id = id; X = x; Y = y; Z = z; }
        public override string ToString() => $"#{Id}({X},{Y},{Z})";
    }

    public sealed class Layout
    {
        public readonly string Name;
        public readonly Slot[] Slots;

        public Layout(string name, IList<(int x, int y, int z)> positions)
        {
            Name = name;
            Slots = new Slot[positions.Count];
            for (int i = 0; i < positions.Count; i++) Slots[i] = new Slot(i, positions[i].x, positions[i].y, positions[i].z);
            if (Slots.Length % 2 != 0) throw new ArgumentException("A layout needs an even number of slots.");
        }

        public int Count => Slots.Length;

        /// <summary>Free = not covered and at least one horizontal side open, given which slots are occupied.</summary>
        public bool IsFree(int id, bool[] occupied)
        {
            if (!occupied[id]) return false;
            Slot s = Slots[id];
            bool left = false, right = false;
            foreach (Slot t in Slots)
            {
                if (t.Id == id || !occupied[t.Id]) continue;
                bool rowsOverlap = Math.Abs(t.Y - s.Y) < 2;
                if (t.Z > s.Z && Math.Abs(t.X - s.X) < 2 && rowsOverlap) return false;
                if (t.Z == s.Z && rowsOverlap)
                {
                    if (t.X == s.X - 2) left = true;
                    else if (t.X == s.X + 2) right = true;
                }
            }
            return !(left && right);
        }

        /// <summary>Ids of free occupied slots, ascending.</summary>
        public List<int> FreeSlots(bool[] occupied)
        {
            var free = new List<int>();
            for (int i = 0; i < Slots.Length; i++) if (IsFree(i, occupied)) free.Add(i);
            return free;
        }

        /// <summary>Starter 16: 4 × 4, one layer.</summary>
        public static Layout Starter16()
        {
            var p = new List<(int, int, int)>();
            for (int y = 0; y <= 6; y += 2) for (int x = 0; x <= 6; x += 2) p.Add((x, y, 0));
            return new Layout("Starter", p);
        }

        /// <summary>Small 24: 6 × 3 base plus a 3 × 2 second layer offset by half a tile.</summary>
        public static Layout Small24()
        {
            var p = new List<(int, int, int)>();
            for (int y = 0; y <= 4; y += 2) for (int x = 0; x <= 10; x += 2) p.Add((x, y, 0));
            for (int y = 1; y <= 3; y += 2) for (int x = 3; x <= 7; x += 2) p.Add((x, y, 1));
            return new Layout("Small", p);
        }

        public static Layout ByName(string name) => name switch
        {
            "Starter" => Starter16(),
            "Small" => Small24(),
            _ => throw new ArgumentException("Unknown layout " + name),
        };
    }
}
