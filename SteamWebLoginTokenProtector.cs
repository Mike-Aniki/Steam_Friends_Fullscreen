using System;
using System.Security.Cryptography;
using System.Text;

namespace SteamFriendsFullscreen
{
    internal static class SteamWebLoginTokenProtector
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes(
            "SteamFriendsFullscreen.WebLoginTokens.v1");

        public static string Protect(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var clearBytes = Encoding.UTF8.GetBytes(value);
            var encrypted = ProtectedData.Protect(
                clearBytes,
                Entropy,
                DataProtectionScope.CurrentUser);

            return Convert.ToBase64String(encrypted);
        }

        public static bool TryUnprotect(string encryptedValue, out string clearValue)
        {
            clearValue = string.Empty;
            if (string.IsNullOrWhiteSpace(encryptedValue))
            {
                return true;
            }

            try
            {
                var encryptedBytes = Convert.FromBase64String(encryptedValue);
                var clearBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    Entropy,
                    DataProtectionScope.CurrentUser);

                clearValue = Encoding.UTF8.GetString(clearBytes);
                return true;
            }
            catch
            {
                clearValue = string.Empty;
                return false;
            }
        }
    }
}
