#if NET6_0_OR_GREATER
using System;
#endif
using System.Diagnostics;
using System.Net.Sockets;
#if !NET6_0_OR_GREATER
using System.Text;
#endif  // !NET6_0_OR_GREATER
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using NumericUpDownLib;


namespace AutoFishing
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Thread for UDP send.
        /// </summary>
        private Thread? _thread;
        /// <summary>
        /// Charge time (milliseconds).
        /// </summary>
        private int _chargeTime = 0;
        /// <summary>
        /// Timeout for Rolling (milliseconds).
        /// </summary>
        private int _rollTimeout = 0;
        /// <summary>
        /// Timeout for waiting (milliseconds).
        /// </summary>
        private int _waitTimeout = 0;

        /// <summary>
        /// Initialize component.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Start auto fishing.
        /// </summary>
        private void StartAutoFishing()
        {
            ConsoleEx.Log("Start auto fishing");
            _labelStatus.Content = "Start";

            var client = new UdpClient(AddressFamily.InterNetwork);
            client.Connect("127.0.0.1", 9000);
            var thread = new Thread(param =>
            {
                var updClient = (UdpClient)param!;
#if NET6_0_OR_GREATER
                var pressData = "/input/UseRight\x00,i\x00\x00\x00\x00\x00\x01"u8;
                var releaseData = "/input/UseRight\x00,i\x00\x00\x00\x00\x00\x00"u8;
#else
                var pressData = Encoding.ASCII.GetBytes("/input/UseRight\x00,i\x00\x00\x00\x00\x00\x01");
                var releaseData = Encoding.ASCII.GetBytes("/input/UseRight\x00,i\x00\x00\x00\x00\x00\x00");
#endif  // NET6_0_OR_GREATER
                var sw = new Stopwatch();

                int saveDetectedCount = 0;
                using (var saveLogWatcher = new SaveLogWatcher())
                {
                    saveLogWatcher.Start();
                    saveLogWatcher.DataSaved += (_, _) => Interlocked.Increment(ref saveDetectedCount);
                    try
                    {
                        const int watchCycle = 32;

                        while (true)
                        {
                            ConsoleEx.Log($"Charge ...; [{_chargeTime}] ms");
                            _labelStatus.Dispatcher.Invoke(() => _labelStatus.Content = "Charging");
                            SendData(updClient, pressData);
                            Thread.Sleep(_chargeTime);

                            ConsoleEx.Log($"Release; Timeout=[{_waitTimeout}] ms");
                            _labelStatus.Dispatcher.Invoke(() => _labelStatus.Content = "Wait");
                            SendData(updClient, releaseData);
                            sw.Restart();
                            Interlocked.Exchange(ref saveDetectedCount, 0);
                            do
                            {
                                Thread.Sleep(watchCycle);

                                if (saveDetectedCount > 0)
                                {
                                    ConsoleEx.Log("Hit!");
                                    break;
                                }
                            }
                            while (sw.ElapsedMilliseconds < _waitTimeout);

                            if (saveDetectedCount == 0)
                            {
                                ConsoleEx.Log("Wait timeout");
                            }

                            ConsoleEx.Log($"Roll; Timeout=[{_rollTimeout}] ms");
                            _labelStatus.Dispatcher.Invoke(() => _labelStatus.Content = "Roll");
                            SendData(updClient, pressData);
                            sw.Restart();
                            do
                            {
                                Thread.Sleep(watchCycle);
                                if (saveDetectedCount > 2)
                                {
                                    ConsoleEx.Log("Put into bucket");
                                    Thread.Sleep(100);
                                    break;
                                }
                            }
                            while (sw.ElapsedMilliseconds < _rollTimeout);

                            if (saveDetectedCount <= 2)
                            {
                                ConsoleEx.Log("Roll timeout");
                            }

                            SendData(updClient, releaseData);
                            Thread.Sleep(100);
                        }
                    }
                    catch (ThreadInterruptedException)
                    {
                        // Do nothing
                    }
                    finally
                    {
                        SendData(updClient, releaseData);
                        client.Dispose();
                    }
                }
            })
            {
                IsBackground = true
            };
            thread.Start(client);
            _thread = thread;
        }

        /// <summary>
        /// Stop auto fishing.
        /// </summary>
        private void StopAutoFishing()
        {
            ConsoleEx.Log("Stop");
            _labelStatus.Content = "Stop";

            var thread = _thread;
            if (thread != null)
            {
                _thread = null;
                thread.Interrupt();
                thread.Join(1000);
            }
        }

        /// <summary>
        /// Start or stop auto fishing.
        /// </summary>
        /// <param name="sender">Start/Stop toggle button.</param>
        /// <param name="e">Contains state information and event data associated with a routed event.</param>
        private void ButtonToggle_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;

            if ((string)button.Content == "Start")
            {
                button.Content = "Stop";
                StartAutoFishing();
            }
            else
            {
                button.Content = "Start";
                StopAutoFishing();
            }
        }

        /// <summary>
        /// Update <see cref="_chargeTime"/>.
        /// </summary>
        /// <param name="sender"><see cref="UIntegerUpDown"/> that manages charge time.</param>
        /// <param name="e">Provides data about a change in value to a dependency property.</param>
        private void NudChargeTime_ValueChanged(object sender, RoutedPropertyChangedEventArgs<uint> e)
        {
            _chargeTime = (int)((UIntegerUpDown)sender).Value;
        }

        /// <summary>
        /// Update <see cref="_waitTimeout"/>.
        /// </summary>
        /// <param name="sender"><see cref="UIntegerUpDown"/> that manages waiting timeout.</param>
        /// <param name="e">Provides data about a change in value to a dependency property.</param>
        private void NudWaitTimeout_ValueChanged(object sender, RoutedPropertyChangedEventArgs<uint> e)
        {
            _waitTimeout = (int)((UIntegerUpDown)sender).Value;
        }

        /// <summary>
        /// Update <see cref="_rollTimeout"/>.
        /// </summary>
        /// <param name="sender"><see cref="UIntegerUpDown"/> that manages roll time.</param>
        /// <param name="e">Provides data about a change in value to a dependency property.</param>
        private void NudRollTimeout_ValueChanged(object sender, RoutedPropertyChangedEventArgs<uint> e)
        {
            _rollTimeout = (int)((UIntegerUpDown)sender).Value;
        }

#if NET6_0_OR_GREATER
        /// <summary>
        /// Send data to <see cref="UdpClient"/>.
        /// </summary>
        /// <param name="client">A <see cref="UdpClient"/>.</param>
        /// <param name="data">A <see cref="byte"/> data to send.</param>
        private static void SendData(UdpClient client, ReadOnlySpan<byte> data)
        {
            client.Send(data);
        }

#else
        /// <summary>
        /// Send data to <see cref="UdpClient"/>.
        /// </summary>
        /// <param name="client">A <see cref="UdpClient"/>.</param>
        /// <param name="data">A <see cref="byte"/> data to send.</param>
        private static void SendData(UdpClient client, byte[] data)
        {
            client.Send(data, data.Length);
        }
#endif  // NET6_0_OR_GREATER
    }
}
