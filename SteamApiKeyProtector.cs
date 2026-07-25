using System;
using System.Security.Cryptography;
using System.Text;

namespace SteamFriendsFullscreen
{
    internal static class SteamApiKeyProtector
    {
        // Additional entropy ties the protected value to this plugin as well as
        // to the current Windows user account used by DPAPI.
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes(
            "SteamFriendsFullscreen.SteamApiKey.v1");

        public static string Protect(string plainText)
        {
            if (string.IsNullOrWhiteSpace(plainText))
            {
                return string.Empty;
            }

            var plainBytes = Encoding.UTF8.GetBytes(plainText.Trim());
            var protectedBytes = ProtectedData.Protect(
                plainBytes,
                Entropy,
                DataProtectionScope.CurrentUser);

            return Convert.ToBase64String(protectedBytes);
        }

        public static bool TryUnprotect(string protectedText, out string plainText)
        {
            plainText = string.Empty;

            if (string.IsNullOrWhiteSpace(protectedText))
            {
                return true;
            }

            try
            {
                var protectedBytes = Convert.FromBase64String(protectedText);
                var plainBytes = ProtectedData.Unprotect(
                    protectedBytes,
                    Entropy,
                    DataProtectionScope.CurrentUser);

                plainText = Encoding.UTF8.GetString(plainBytes);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (CryptographicException)
            {
                return false;
            }
        }
    }
}
