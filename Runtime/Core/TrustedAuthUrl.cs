using System;

namespace LiANN.MahjongTones.Core
{
    /// <summary>
    /// The Supabase address comes from the Pinyin API's remote config, and the email, code and refresh token are sent there.
    /// Only an https address on the expected host is used: the exact host from accounts.json when it is set, otherwise
    /// a *.supabase.co project host.
    /// </summary>
    public static class TrustedAuthUrl
    {
        public static bool IsAllowed(string url, string allowedHost)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
            if (uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort) return false;
            string host = uri.Host.ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(allowedHost)) return host == allowedHost.Trim().ToLowerInvariant();
            return host.EndsWith(".supabase.co", StringComparison.Ordinal) && host.Length > ".supabase.co".Length
                && host.IndexOf('.') == host.Length - ".supabase.co".Length;
        }
    }
}
