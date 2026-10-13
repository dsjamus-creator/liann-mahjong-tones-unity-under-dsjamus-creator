using System.Text;

namespace LiANN.MahjongTones.Core
{
    /// <summary>
    /// mulberry32 seeded with the FNV-1a hash of a seed string (spec §4.1).
    /// Must stay bit-for-bit identical to the web edition, so every step is unchecked uint maths.
    /// </summary>
    public sealed class Rng
    {
        uint _state;

        public Rng(string seed) { _state = Fnv1a(seed); }
        public Rng(uint state) { _state = state; }

        public static uint Fnv1a(string text)
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (byte b in Encoding.UTF8.GetBytes(text ?? ""))
                {
                    h ^= b;
                    h *= 16777619u;
                }
                return h;
            }
        }

        /// <summary>Next raw uint32 output.</summary>
        public uint NextUInt()
        {
            unchecked
            {
                _state += 0x6D2B79F5u;
                uint a = _state;
                uint t = (a ^ (a >> 15)) * (a | 1u);
                t = (t + (t ^ (t >> 7)) * (t | 61u)) ^ t;
                return t ^ (t >> 14);
            }
        }

        /// <summary>Uniform double in [0, 1).</summary>
        public double Next() => NextUInt() / 4294967296.0;

        /// <summary>floor(next() × n).</summary>
        public int RandInt(int n) => (int)(Next() * n);
    }
}
