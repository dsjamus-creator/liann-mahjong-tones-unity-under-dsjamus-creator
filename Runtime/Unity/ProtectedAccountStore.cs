using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace LiANN.MahjongTones
{
    // Only encrypted or OS-protected data may survive a process restart.
    // The Editor and unsupported platforms deliberately keep the session in memory.
    internal static class ProtectedAccountStore
    {
        const string LegacyFileName = "liann-tones.account.v1.json";
        const string WindowsFileName = "liann-tones.account.v2.dpapi";

        public static bool IsPersistent
        {
            get
            {
#if (UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_WIN) && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public static bool RemoveLegacyPlaintext()
        {
            try
            {
                var path = Path.Combine(Application.persistentDataPath, LegacyFileName);
                if (File.Exists(path)) File.Delete(path);
                return !File.Exists(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Could not remove the old account cache: " + e.GetType().Name);
                return false;
            }
        }

        public static string Load()
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                using var store = new AndroidJavaClass("com.learningwithliann.mahjongtones.SecureSessionStore");
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                return store.CallStatic<string>("load", activity);
#elif UNITY_IOS && !UNITY_EDITOR
                var pointer = SessionLoad();
                if (pointer == IntPtr.Zero) return null;
                try { return Marshal.PtrToStringUTF8(pointer); }
                finally { SessionFree(pointer); }
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
                var path = Path.Combine(Application.persistentDataPath, WindowsFileName);
                return File.Exists(path) ? Encoding.UTF8.GetString(WindowsUnprotect(File.ReadAllBytes(path))) : null;
#else
                return null;
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning("Protected account state could not be loaded: " + e.GetType().Name);
                return null;
            }
        }

        public static bool Save(string json)
        {
            if (string.IsNullOrEmpty(json)) return false;
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                using var store = new AndroidJavaClass("com.learningwithliann.mahjongtones.SecureSessionStore");
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                return store.CallStatic<bool>("save", activity, json);
#elif UNITY_IOS && !UNITY_EDITOR
                return SessionSave(json);
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
                var path = Path.Combine(Application.persistentDataPath, WindowsFileName);
                var temporary = path + ".tmp";
                File.WriteAllBytes(temporary, WindowsProtect(Encoding.UTF8.GetBytes(json)));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                return true;
#else
                return false;
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning("Protected account state could not be saved: " + e.GetType().Name);
                return false;
            }
        }

        public static bool Delete()
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                using var store = new AndroidJavaClass("com.learningwithliann.mahjongtones.SecureSessionStore");
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                return store.CallStatic<bool>("delete", activity);
#elif UNITY_IOS && !UNITY_EDITOR
                return SessionDelete();
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
                var path = Path.Combine(Application.persistentDataPath, WindowsFileName);
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
                return !File.Exists(path) && !File.Exists(path + ".tmp");
#else
                return true;
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning("Protected account state could not be removed: " + e.GetType().Name);
                return false;
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal", EntryPoint = "LMT_SessionLoad")]
        static extern IntPtr SessionLoad();
        [DllImport("__Internal", EntryPoint = "LMT_SessionSave")]
        [return: MarshalAs(UnmanagedType.I1)]
        static extern bool SessionSave([MarshalAs(UnmanagedType.LPUTF8Str)] string json);
        [DllImport("__Internal", EntryPoint = "LMT_SessionDelete")]
        [return: MarshalAs(UnmanagedType.I1)]
        static extern bool SessionDelete();
        [DllImport("__Internal", EntryPoint = "LMT_SessionFree")]
        static extern void SessionFree(IntPtr pointer);
#endif

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [StructLayout(LayoutKind.Sequential)]
        struct DataBlob { public int Size; public IntPtr Data; }

        [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CryptProtectData(ref DataBlob input, string description, IntPtr entropy,
            IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);
        [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CryptUnprotectData(ref DataBlob input, IntPtr description, IntPtr entropy,
            IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);
        [DllImport("kernel32.dll")]
        static extern IntPtr LocalFree(IntPtr memory);

        const int UiForbidden = 0x1;
        static byte[] WindowsProtect(byte[] data) => WindowsTransform(data, true);
        static byte[] WindowsUnprotect(byte[] data) => WindowsTransform(data, false);

        static byte[] WindowsTransform(byte[] data, bool protect)
        {
            var input = new DataBlob { Size = data.Length, Data = Marshal.AllocHGlobal(data.Length) };
            var output = new DataBlob();
            try
            {
                Marshal.Copy(data, 0, input.Data, data.Length);
                var ok = protect
                    ? CryptProtectData(ref input, "LiANN Mahjong Tones account", IntPtr.Zero, IntPtr.Zero,
                        IntPtr.Zero, UiForbidden, out output)
                    : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                        IntPtr.Zero, UiForbidden, out output);
                if (!ok || output.Data == IntPtr.Zero) throw new InvalidOperationException("Windows account protection failed");
                var result = new byte[output.Size];
                Marshal.Copy(output.Data, result, 0, output.Size);
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(input.Data);
                if (output.Data != IntPtr.Zero) LocalFree(output.Data);
            }
        }
#endif
    }
}

