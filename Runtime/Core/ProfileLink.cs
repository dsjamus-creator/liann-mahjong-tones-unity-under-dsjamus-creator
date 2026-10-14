using System;

namespace LiANN.MahjongTones.Core
{
    /// <summary>
    /// Finishing the learner profile (One LiANN account, build step B3). AlongSide's learner form is at /student#profile;
    /// a signed-in player asks AlongSide for a one-time pass and opens the address it returns in the browser, which signs
    /// the browser in and lands on the form. Only an address on the configured AlongSide server is ever opened.
    /// </summary>
    public static class ProfileLink
    {
        public const string FormPath = "/student#profile";
        public const string PassPath = "/learning/session-pass";

        public static string FormUrl(string alongsideBase) => (alongsideBase ?? "").TrimEnd('/') + FormPath;

        /// <summary>True only for an address with the same scheme, host and port as the AlongSide server, and no user info.</summary>
        public static bool IsOnAlongSide(string url, string alongsideBase)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !Uri.TryCreate(alongsideBase, UriKind.Absolute, out var b)) return false;
            if (!string.IsNullOrEmpty(uri.UserInfo)) return false;
            if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return false;
            return uri.Scheme == b.Scheme && string.Equals(uri.Host, b.Host, StringComparison.OrdinalIgnoreCase) && uri.Port == b.Port;
        }
    }
}
