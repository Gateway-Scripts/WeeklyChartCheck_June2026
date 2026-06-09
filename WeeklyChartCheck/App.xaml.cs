using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;

namespace WeeklyChartCheck
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            string patientId = e.Args.First();
            var mainView = new MainWindow();
            var mainViewModel = new MainViewModel(patientId);
            mainView.DataContext = mainViewModel;
            mainView.ShowDialog();
        }
    }

}
