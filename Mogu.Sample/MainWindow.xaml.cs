using System;
using System.Text;
using System.Windows;

namespace Mogu.Sample
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static Mogu.Connection? connection;

        public MainWindow()
        {
            InitializeComponent();
            this.textBlock.Text = $"pid:{NativeFunctions.GetCurrentProcessId()}";
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (connection == null) return;

            var text = this.textBox.Text;
            var buf = Encoding.UTF8.GetBytes(text);
            if (connection.IsConnected)
            {
                connection.Pipe.WriteAsync(buf, 0, buf.Length);
                Log($"send");
            }
            else
            {
                Log($"disconnected");
            }
        }

        private void Log(string str)
        {
            this.logText.Text = $"{DateTime.Now}:{str}\n{this.logText.Text}";
        }
    }
}
