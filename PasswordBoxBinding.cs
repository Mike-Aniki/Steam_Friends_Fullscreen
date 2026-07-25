using System.Windows;
using System.Windows.Controls;

namespace SteamFriendsFullscreen
{
    /// <summary>
    /// Allows a PasswordBox to use a standard two-way WPF binding without
    /// exposing the API key visually in the settings page.
    /// </summary>
    public static class PasswordBoxBinding
    {
        public static readonly DependencyProperty BindPasswordProperty =
            DependencyProperty.RegisterAttached(
                "BindPassword",
                typeof(bool),
                typeof(PasswordBoxBinding),
                new PropertyMetadata(false, OnBindPasswordChanged));

        public static readonly DependencyProperty BoundPasswordProperty =
            DependencyProperty.RegisterAttached(
                "BoundPassword",
                typeof(string),
                typeof(PasswordBoxBinding),
                new FrameworkPropertyMetadata(
                    string.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnBoundPasswordChanged));

        private static readonly DependencyProperty IsUpdatingProperty =
            DependencyProperty.RegisterAttached(
                "IsUpdating",
                typeof(bool),
                typeof(PasswordBoxBinding),
                new PropertyMetadata(false));

        public static void SetBindPassword(DependencyObject element, bool value)
        {
            element.SetValue(BindPasswordProperty, value);
        }

        public static bool GetBindPassword(DependencyObject element)
        {
            return (bool)element.GetValue(BindPasswordProperty);
        }

        public static void SetBoundPassword(DependencyObject element, string value)
        {
            element.SetValue(BoundPasswordProperty, value ?? string.Empty);
        }

        public static string GetBoundPassword(DependencyObject element)
        {
            return (string)element.GetValue(BoundPasswordProperty);
        }

        private static void SetIsUpdating(DependencyObject element, bool value)
        {
            element.SetValue(IsUpdatingProperty, value);
        }

        private static bool GetIsUpdating(DependencyObject element)
        {
            return (bool)element.GetValue(IsUpdatingProperty);
        }

        private static void OnBindPasswordChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs args)
        {
            var passwordBox = dependencyObject as PasswordBox;
            if (passwordBox == null)
            {
                return;
            }

            passwordBox.PasswordChanged -= PasswordBox_PasswordChanged;

            if ((bool)args.NewValue)
            {
                passwordBox.PasswordChanged += PasswordBox_PasswordChanged;
            }
        }

        private static void OnBoundPasswordChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs args)
        {
            var passwordBox = dependencyObject as PasswordBox;
            if (passwordBox == null || GetIsUpdating(passwordBox))
            {
                return;
            }

            passwordBox.PasswordChanged -= PasswordBox_PasswordChanged;
            passwordBox.Password = args.NewValue as string ?? string.Empty;
            passwordBox.PasswordChanged += PasswordBox_PasswordChanged;
        }

        private static void PasswordBox_PasswordChanged(object sender, RoutedEventArgs args)
        {
            var passwordBox = sender as PasswordBox;
            if (passwordBox == null)
            {
                return;
            }

            SetIsUpdating(passwordBox, true);
            SetBoundPassword(passwordBox, passwordBox.Password);
            SetIsUpdating(passwordBox, false);
        }
    }
}
