using LiANN.MahjongTones.Core;
using NUnit.Framework;

namespace LiANN.MahjongTones.Tests
{
    public class TrustedAuthUrlTests
    {
        [TestCase("https://abcdefgh.supabase.co", null, true)]
        [TestCase("https://abcdefgh.supabase.co/", "", true)]
        [TestCase("https://auth.lwliann.com", "auth.lwliann.com", true)]
        [TestCase("http://abcdefgh.supabase.co", null, false)]            // not https
        [TestCase("https://supabase.co", null, false)]                     // no project
        [TestCase("https://a.b.supabase.co", null, false)]                 // nested subdomain
        [TestCase("https://abcdefgh.supabase.co.evil.example", null, false)]
        [TestCase("https://evil.example/abcdefgh.supabase.co", null, false)]
        [TestCase("https://user@abcdefgh.supabase.co", null, false)]
        [TestCase("https://abcdefgh.supabase.co:8443", null, false)]
        [TestCase("https://abcdefgh.supabase.co", "other.supabase.co", false)] // pinned host wins
        [TestCase("not a url", null, false)]
        public void OnlyHttpsOnTheExpectedHost(string url, string pinned, bool allowed) =>
            Assert.AreEqual(allowed, TrustedAuthUrl.IsAllowed(url, pinned));
    }
}
