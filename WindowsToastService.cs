using Microsoft.Toolkit.Uwp.Notifications;
using Playnite.SDK;
using System;
using System.IO;

namespace SteamFriendsFullscreen
{
    internal class WindowsToastService
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        // AppId stable pour associer les toasts
        private const string AppId = "Playnite.SteamFriendsFullscreen";

        private bool isInitialized;

        public void EnsureInitialized()
        {
            if (isInitialized)
            {
                return;
            }

            try
            {
                // IMPORTANT (unpackaged): assure un raccourci Start Menu avec AppUserModelID
                TryInstallStartMenuShortcut();

                isInitialized = true;
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Windows toast initialization failed.");
            }
        }

        private void TryInstallStartMenuShortcut()
        {
            try
            {
                // Certaines versions du toolkit ont InstallShortcut. On utilise reflection pour être compatible.
                var t = typeof(ToastNotificationManagerCompat);
                var m =
                    t.GetMethod("InstallShortcut", new[] { typeof(string) }) ??
                    t.GetMethod("EnsureStartMenuShortcut", new[] { typeof(string) }) ??
                    t.GetMethod("CreateShortcut", new[] { typeof(string) });

                if (m != null)
                {
                    m.Invoke(null, new object[] { AppId });
                }
                else
                {
                    logger.Warn("No shortcut install method found in ToastNotificationManagerCompat. Toasts may not appear on some systems.");
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to install Start Menu shortcut for toast notifications.");
            }
        }

        public void Show(string title, string message, string imagePathOrUrl = null)
        {
            EnsureInitialized();

            try
            {
                var builder = new ToastContentBuilder()
                    .AddText(title)
                    .AddText(message);

                if (!string.IsNullOrWhiteSpace(imagePathOrUrl))
                {
                    // Support URL https + file:// + path brut
                    if (Uri.TryCreate(imagePathOrUrl, UriKind.Absolute, out var uri))
                    {
                        if (uri.IsFile)
                        {
                            var local = uri.LocalPath;
                            if (File.Exists(local))
                            {
                                // Petit avatar à gauche (discret) + crop rond
                                builder.AddAppLogoOverride(uri, ToastGenericAppLogoCrop.Circle);
                            }
                        }
                        else
                        {
                            // URL https
                            builder.AddAppLogoOverride(uri, ToastGenericAppLogoCrop.Circle);
                        }
                    }
                    else if (File.Exists(imagePathOrUrl))
                    {
                        builder.AddAppLogoOverride(new Uri(imagePathOrUrl), ToastGenericAppLogoCrop.Circle);
                    }
                }

                builder.Show();
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Windows toast show failed.");
            }
        }

    }
}
