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
using System.Windows.Shapes;
using System.Windows.Navigation;
using System.Diagnostics;

namespace RoundedTB
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            WPFUI.Background.Manager.Apply(WPFUI.Background.BackgroundType.Mica, this);
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(e.Uri.ToString());
        }

        private void configButton_Click(object sender, RoutedEventArgs e)
        {
            OpenWithDefaultApp(((MainWindow)Application.Current.MainWindow).configPath);
        }

        private void logButton_Click(object sender, RoutedEventArgs e)
        {
            OpenWithDefaultApp(((MainWindow)Application.Current.MainWindow).logPath);
        }

        /// <summary>用系统默认程序打开指定文件;失败时提示而不是崩溃(Process.Start 在文件不存在/
        /// 无关联程序时会抛异常导致整个程序闪退)。</summary>
        private void OpenWithDefaultApp(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not open: " + path + "\n\n" + ex.Message +
                    "\n\n无法打开:" + path + "\n" + ex.Message,
                    "RoundedTB Revived", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
