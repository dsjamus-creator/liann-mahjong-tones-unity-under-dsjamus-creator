using LiANN.MahjongTones.Core;
using NUnit.Framework;

namespace LiANN.MahjongTones.Tests
{
    public class ProfileLinkTests
    {
        const string Base = "https://lwl-backend-mhgo.onrender.com";

        [TestCase("https://lwl-backend-mhgo.onrender.com/auth/liann/continue#pass=abc", true)]
        [TestCase("https://LWL-BACKEND-MHGO.onrender.com/student#profile", true)]
        [TestCase("http://lwl-backend-mhgo.onrender.com/auth/liann/continue", false)]   // scheme changed
        [TestCase("https://lwl-backend-mhgo.onrender.com.evil.example/x", false)]
        [TestCase("https://evil.example/lwl-backend-mhgo.onrender.com", false)]
        [TestCase("https://user@lwl-backend-mhgo.onrender.com/x", false)]
        [TestCase("https://lwl-backend-mhgo.onrender.com:8443/x", false)]
        [TestCase("javascript:alert(1)", false)]
        [TestCase("/student#profile", false)]
        [TestCase(null, false)]
        public void OpensOnlyAlongSideAddresses(string url, bool allowed) => Assert.AreEqual(allowed, ProfileLink.IsOnAlongSide(url, Base));

        [Test]
        public void FormAddressIsOnAlongSide()
        {
            Assert.AreEqual(Base + "/student#profile", ProfileLink.FormUrl(Base + "/"));
            Assert.IsTrue(ProfileLink.IsOnAlongSide(ProfileLink.FormUrl(Base), Base));
        }
    }
}
