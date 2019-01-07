using System.Windows;
using System.Windows.Controls;

using AsmDude.Tools;

namespace AsmDude.QuickInfo
{
    public partial class BugWindow : UserControl
    {
        public BugWindow()
        {
            this.InitializeComponent();
            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:constructor", this.ToString()));

            this.MainWindow.MouseLeftButtonDown += (o, i) => {
                AsmDudeToolsStatic.Output_INFO(string.Format("{0}:MouseLeftButtonDown Event", this.ToString()));
                //i.Handled = true; // dont let the mouse event from inside this window bubble up to VS
            }; 

            this.MainWindow.PreviewMouseLeftButtonDown += (o, i) =>
            {
                AsmDudeToolsStatic.Output_INFO(string.Format("{0}:PreviewMouseLeftButtonDown Event", this.ToString()));
                //i.Handled = true; // if true then no event is able to bubble to the gui
            };
        }

        private void GotMouseCapture_Click(object sender, RoutedEventArgs e)
        {
            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:GotMouseCapture_Click", this.ToString()));
            e.Handled = true;
        }
    }
}
