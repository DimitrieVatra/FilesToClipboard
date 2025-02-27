using System;
using System.IO;
using System.Windows;

namespace FilesToClipboard
{
    public partial class App : Application
    {
        // A constant default directory
        private const string DefaultStartupDirectory = @"C:\MyDefaultPath";

        // We'll store the actual directory to use here. 
        // It can be changed if an argument is passed.
        public static string StartupDirectory { get; private set; } = DefaultStartupDirectory;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Check if an argument is provided and if it’s a valid directory
            if (e.Args.Length > 0 && Directory.Exists(e.Args[0]))
            {
                StartupDirectory = e.Args[0];
            }

            // Show the main window, passing the chosen directory
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}
