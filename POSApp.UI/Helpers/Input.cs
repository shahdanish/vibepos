using System.Windows;
using System.Windows.Controls;

namespace POSApp.UI.Helpers
{
    /// <summary>
    /// Attached properties used by the design-system input templates.
    ///
    ///   &lt;TextBox helpers:Input.Placeholder="e.g. Ahmed Traders"/&gt;
    ///   &lt;PasswordBox helpers:Input.Placeholder="At least 6 characters"/&gt;
    ///
    /// The placeholder is shown while the field is empty and hidden as soon as the user types.
    /// </summary>
    public static class Input
    {
        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.RegisterAttached(
                "Placeholder", typeof(string), typeof(Input),
                new PropertyMetadata(string.Empty, OnPlaceholderChanged));

        public static string GetPlaceholder(DependencyObject d) => (string)d.GetValue(PlaceholderProperty);
        public static void SetPlaceholder(DependencyObject d, string value) => d.SetValue(PlaceholderProperty, value);

        /// <summary>
        /// True while a PasswordBox holds any characters. PasswordBox has no bindable text, so the
        /// template reads this instead to decide whether to show the placeholder.
        /// </summary>
        public static readonly DependencyProperty HasPasswordProperty =
            DependencyProperty.RegisterAttached(
                "HasPassword", typeof(bool), typeof(Input), new PropertyMetadata(false));

        public static bool GetHasPassword(DependencyObject d) => (bool)d.GetValue(HasPasswordProperty);
        public static void SetHasPassword(DependencyObject d, bool value) => d.SetValue(HasPasswordProperty, value);

        private static void OnPlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not PasswordBox box) return;

            box.PasswordChanged -= OnPasswordChanged;
            box.PasswordChanged += OnPasswordChanged;
            SetHasPassword(box, box.SecurePassword.Length > 0);
        }

        private static void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox box)
                SetHasPassword(box, box.SecurePassword.Length > 0);
        }
    }
}
