using System;
using System.IO;
#if NET9_0_OR_GREATER
using System.Threading;
#endif  // NET9_0_OR_GREATER
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;


namespace AutoFishing
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Lock object for exception log.
        /// </summary>
#if NET9_0_OR_GREATER
        private static readonly Lock _lockLog = new();
#else
        private static readonly object _lockLog = new();
#endif  // NET9_0_OR_GREATER

        /// <summary>
        /// Register callback methods to unhandled exception handlers and start WPF application.
        /// </summary>
        [STAThread]
        public static void Main(string[] args)
        {
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

#if !DEBUG
            foreach (var arg in args)
            {
                if (arg == "--debug")
                {
#endif  // !DEBUG
                    ConsoleEx.SetupConsole();
#if !DEBUG
                    break;
                }
            }
#endif  // !DEBUG

            var app = new App();
            app.InitializeComponent();
            app.Run();
        }


        /// <summary>
        /// Setup <see cref="Application.DispatcherUnhandledException"/> to catch unhandled exception in UI thread.
        /// </summary>
        public App()
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        /// <summary>
        /// Callback method for unhandled exceptions in UI thread.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var ex = e.Exception;

            WriteExceptionLog(ex);

            var result = MessageBox.Show(
                $"An exception occured at {ex.TargetSite?.Name}.\n"
                    + "Are you sure want to continue program?\n"
                    + $"{ex.Message}",
                ex.GetType().FullName,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// Callback method for unhandled exceptions in <see cref="Task"/>.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            var ex = e.Exception.InnerException;
            if (ex == null)
            {
                return;
            }

            WriteExceptionLog(ex);

            var result = MessageBox.Show(
                $"An exception occured on background task {ex.TargetSite?.Name}."
                    + "Are you sure want to continue program?\n"
                    + $"{ex.Message}",
                ex.GetType().FullName,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                e.SetObserved();
            }
        }

        /// <summary>
        /// Callback method for unhandled exceptions.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is not Exception ex)
            {
                MessageBox.Show("Unhandled exception is not treat as System.Exception.");
                return;
            }

            WriteExceptionLog(ex);

            MessageBox.Show(
                $"An exception occured on background task {ex.TargetSite?.Name}."
                    + "Terminate this program\n"
                    + $"{ex.Message}",
                ex.GetType().FullName,
                MessageBoxButton.OK,
                MessageBoxImage.Stop);

            Environment.Exit(0);
        }

        /// <summary>
        /// Write exception log to UnhandledException.log.
        /// </summary>
        /// <param name="ex">An exception.</param>
        private static void WriteExceptionLog(Exception ex)
        {
            lock (_lockLog)
            {
                using (var fs = new FileStream("AutoFishing.log", FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                using (var sw = new StreamWriter(fs))
                {
                    sw.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff)}] {ex}");
                }
            }
        }
    }
}
