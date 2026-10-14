using LiANN.MahjongTones.Core;
using NUnit.Framework;

namespace LiANN.MahjongTones.Tests
{
    public class EmailAddressTests
    {
        [TestCase("jamusflats@gmail.com", true)]
        [TestCase("  dsjamus+lwltest2@googlemail.com ", true)]
        [TestCase("info@lwliann.com", true)]
        [TestCase("a.b-c@sub.example.co.uk", true)]
        [TestCase("jamusflats@gmail.,com", false)]   // the DSJ-PC typo
        [TestCase("jamusflats@gmail..com", false)]
        [TestCase("jamusflats@gmailcom", false)]
        [TestCase("jamusflats@gmail.c", false)]
        [TestCase("jamus flats@gmail.com", false)]
        [TestCase("jamusflats@@gmail.com", false)]
        [TestCase("@gmail.com", false)]
        [TestCase("jamusflats@", false)]
        [TestCase("jamusflats@-gmail.com", false)]
        [TestCase(".jamus@gmail.com", false)]
        [TestCase("jamus,flats@gmail.com", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void CatchesTyposBeforeSending(string email, bool valid) => Assert.AreEqual(valid, EmailAddress.LooksValid(email));
    }
}
