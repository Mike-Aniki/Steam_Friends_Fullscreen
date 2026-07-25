using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SteamFriendsFullscreen
{
    public partial class SteamFriendsFullscreenSettingsView : UserControl
    {
        public SteamFriendsFullscreenSettingsView()
        {
            InitializeComponent();
        }

        private async void ConnectSteamWebLogin_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as SteamFriendsFullscreenSettingsViewModel;
            if (viewModel == null)
            {
                return;
            }

            await viewModel.ConnectSteamWebLoginAsync();
        }

        private void DisconnectSteamWebLogin_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as SteamFriendsFullscreenSettingsViewModel;
            viewModel?.DisconnectSteamWebLogin();
        }

        private async void TestSteamConnection_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as SteamFriendsFullscreenSettingsViewModel;
            if (viewModel == null)
            {
                return;
            }

            await viewModel.TestSteamConnectionAsync();
        }

        private void TestNotification_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                dynamic dc = DataContext;
                dc?.Settings?.DebugTestNotification?.Invoke();
            }
            catch
            {
                // ignore
            }
        }
    }
}