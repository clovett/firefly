using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace FireflyWindows
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            this.DispatcherUnhandledException += OnDispatcherUnhandledException;
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            UiDispatcher.RunOnUIThread(() =>
            {
                MessageBox.Show(e.ToString(), "Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            });
            e.Handled = true;
        }
    }
}
