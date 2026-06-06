using System.Windows;
using System.Windows.Media;

namespace POSApp.UI.Views
{
    public partial class SyncAlertDialog : Window
    {
        private SyncAlertDialog(Window owner)
        {
            InitializeComponent();
            Owner = owner;
        }

        public static void ShowSuccess(Window owner, int pushedCount, DateTime syncTime)
        {
            var dlg = new SyncAlertDialog(owner);

            dlg.TopBanner.Background    = new SolidColorBrush(Color.FromRgb(0x38, 0x8E, 0x3C));
            dlg.IconText.Text           = "✓";
            dlg.TitleText.Text          = "Sync Successful";
            dlg.SubtitleText.Text       = "Firebase database is now up to date";

            dlg.StatsPanel.Visibility   = Visibility.Visible;
            dlg.LeftCountText.Text      = pushedCount.ToString();
            dlg.LeftCountBrush.Color    = Color.FromRgb(0x38, 0x8E, 0x3C);
            dlg.LeftCardBg.Color        = Color.FromRgb(0xE8, 0xF5, 0xE9);
            dlg.LeftLabel.Text          = "Records Pushed";
            dlg.RightValueText.Text     = syncTime.ToString("hh:mm tt");
            dlg.RightLabel.Text         = "Synced At";

            dlg.DetailText.Text = "All records have been successfully pushed to your Firebase database. " +
                                  "Your data is now available for remote viewing and backup.";

            dlg.OkBtn.Content           = "Great, Thanks!";
            dlg.OkBtn.Background        = new SolidColorBrush(Color.FromRgb(0x38, 0x8E, 0x3C));

            dlg.ShowDialog();
        }

        public static void ShowWarning(Window owner, string reason)
        {
            var dlg = new SyncAlertDialog(owner);

            dlg.TopBanner.Background    = new SolidColorBrush(Color.FromRgb(0xF5, 0x7C, 0x00));
            dlg.IconText.Text           = "!";
            dlg.TitleText.Text          = "Sync Not Available";
            dlg.SubtitleText.Text       = "Action required before syncing";

            dlg.StatsPanel.Visibility   = Visibility.Collapsed;
            dlg.DetailText.Text         = reason;

            dlg.OkBtn.Content           = "Understood";
            dlg.OkBtn.Background        = new SolidColorBrush(Color.FromRgb(0xF5, 0x7C, 0x00));

            dlg.ShowDialog();
        }

        public static void ShowError(Window owner, int pushed, int failed, string errorDetail)
        {
            var dlg = new SyncAlertDialog(owner);

            dlg.TopBanner.Background    = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
            dlg.IconText.Text           = "✕";
            dlg.TitleText.Text          = "Sync Partially Failed";
            dlg.SubtitleText.Text       = $"{pushed} pushed  ·  {failed} failed";

            dlg.StatsPanel.Visibility   = Visibility.Visible;
            dlg.LeftCountText.Text      = pushed.ToString();
            dlg.LeftCountBrush.Color    = Color.FromRgb(0xC6, 0x28, 0x28);
            dlg.LeftCardBg.Color        = Color.FromRgb(0xFF, 0xEB, 0xEE);
            dlg.LeftLabel.Text          = "Pushed";
            dlg.RightValueText.Text     = failed.ToString();
            dlg.RightValueText.Foreground = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));
            dlg.RightLabel.Text         = "Failed";

            dlg.DetailText.Text = $"Some records could not be pushed to Firebase.\n\n" +
                                  $"Error: {errorDetail}\n\n" +
                                  $"Please check your internet connection and try again.";

            dlg.OkBtn.Content           = "Close";
            dlg.OkBtn.Background        = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));

            dlg.ShowDialog();
        }

        private void OkBtn_Click(object sender, RoutedEventArgs e) => Close();
    }
}
