using System.Windows;

namespace HieUserEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void ExitClick(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void AboutClick(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "HIE Benutzer-Editor\nVersion 1.0\n\nEin WPF-Tool zur Verwaltung von Active Directory Benutzern.",
                "Über HIE Benutzer-Editor",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
