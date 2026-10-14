using System;
using System.Linq;

namespace LiANN.MahjongTones.Core
{
    /// <summary>
    /// A plain check of a typed email address before a sign-in code is requested, so typos like "gmail.,com" are caught
    /// on the device with a clear message instead of a failed send (DSJ-PC test, 13 Oct). It is deliberately simple:
    /// it rejects what cannot be a real address and leaves the rest to the sign-in service.
    /// </summary>
    public static class EmailAddress
    {
        const string LocalExtra = ".!#$%&'*+/=?^_`{|}~-";

        public static bool LooksValid(string email)
        {
            email = (email ?? "").Trim();
            if (email.Length < 6 || email.Length > 254) return false;
            int at = email.IndexOf('@');
            if (at <= 0 || at != email.LastIndexOf('@') || at == email.Length - 1) return false;
            string local = email.Substring(0, at), domain = email.Substring(at + 1);
            if (local.Length > 64 || local.StartsWith(".") || local.EndsWith(".") || local.Contains(".."))
                return false;
            if (!local.All(c => c < 128 && (char.IsLetterOrDigit(c) || LocalExtra.IndexOf(c) >= 0))) return false;
            string[] labels = domain.Split('.');
            if (labels.Length < 2) return false;
            foreach (var label in labels)
            {
                if (label.Length == 0 || label.Length > 63 || label.StartsWith("-") || label.EndsWith("-")) return false;
                if (!label.All(c => c < 128 && (char.IsLetterOrDigit(c) || c == '-'))) return false;
            }
            string tld = labels[labels.Length - 1];
            return tld.Length >= 2 && tld.All(char.IsLetter);
        }
    }
}
