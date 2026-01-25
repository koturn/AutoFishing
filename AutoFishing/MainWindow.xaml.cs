using System;
using System.Diagnostics;
using System.Net.Sockets;
#if !NET6_0_OR_GREATER
using System.Text;
#endif  // !NET6_0_OR_GREATER
using System.Threading;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using NumericUpDownLib;
using Koturn.Windows.GlobalHotKeys;
using Koturn.VRChat.Log.Events;


namespace AutoFishing
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Customized VRChat log watcher.
        /// </summary>
        private readonly FishingLogWatcher _logWatcher = new();
        /// <summary>
        /// Global Hot Key manager.
        /// </summary>
        private readonly GlobalHotKeyManager _globalHotKeyManager;
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
            var interopHelper = new WindowInteropHelper(this);
            var hWnd = interopHelper.EnsureHandle();
            HwndSource.FromHwnd(hWnd).AddHook(new HwndSourceHook(WndProc));
            _globalHotKeyManager = new GlobalHotKeyManager(hWnd);

            InitializeComponent();

            _logWatcher.JoinedToInstance += LogWatcher_JoinedToInstance;
            _logWatcher.Start();
            _logWatcher.LeftFromInstance += LogWatcher_LeftFromInstance;
        }


        /// <summary>
        /// Represents the method that handles Win32 window messages.
        /// </summary>
        /// <param name="hWnd">The window handle.</param>
        /// <param name="msg">The message ID.</param>
        /// <param name="wParam">The message's wParam value.</param>
        /// <param name="lParam">The message's lParam value.</param>
        /// <param name="handled">A value that indicates whether the message was handled.
        /// Set the value to true if the message was handled; otherwise, false.</param>
        /// <returns>The appropriate return value depends on the particular message.
        /// See the message documentation details for the Win32 message being handled.</returns>
        private nint WndProc(nint hWnd, int msg, nint wParam, nint lParam, ref bool handled)
        {
            if (msg == GlobalHotKeyManager.MessageId)
            {
                var hotKeyId = (int)wParam;
                foreach (var id in _globalHotKeyManager.RegisteredIds)
                {
                    if (hotKeyId == id)
                    {
                        ToggleStartStop(_buttonStartStop);
                        handled = true;
                        break;
                    }
                }
            }

            return default;
        }

        /// <summary>
        /// Toggle start/stop the thread.
        /// </summary>
        /// <param name="button"><see cref="_buttonStartStop"/></param>
        private void ToggleStartStop(Button button)
        {
            if ((string)button.Content == "Start")
            {
                StartAutoFishing();
            }
            else
            {
                StopAutoFishing();
            }
        }

        /// <summary>
        /// Start auto fishing.
        /// </summary>
        private void StartAutoFishing()
        {
            var worldId = _logWatcher?.InstanceInfo?.WorldId;
            if (worldId == null)
            {
                ConsoleEx.Log("Failed to identify current world");
                return;
            }
            if (worldId != WorldIds.SimpleFishingWorld && worldId != WorldIds.IdleFishing)
            {
                ConsoleEx.Log("Current world is not neither \"A Simple Fishing World\" nor \"Idle Fishing\"");
                return;
            }

            ConsoleEx.Log("Start auto fishing");

            _buttonStartStop.Content = "Stop";
            _textBoxHost.IsEnabled = false;
            _nudPort.IsEnabled = false;
            _labelStatus.Foreground = new SolidColorBrush(Colors.Red);
            _labelStatus.Content = "Start";
            Topmost = true;

            var client = new UdpClient(AddressFamily.InterNetwork);
            var host = _textBoxHost.Text;
            var port = (int)_nudPort.Value;
            client.Connect(host, port);

            if (worldId == WorldIds.SimpleFishingWorld)
            {
                _thread = StartSimpleFishingWorldThread(client);
            }
            else
            {
                _thread = StartIdleFishingThread(client);
            }
        }

        /// <summary>
        /// Start new auto operation <see cref="Thread"/> for "A Simple Fishing World".
        /// </summary>
        /// <param name="client"><see cref="UdpClient"/> for OSC.</param>
        /// <returns>Created and started <see cref="Thread"/>.</returns>
        private Thread StartSimpleFishingWorldThread(UdpClient client)
        {
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
                bool isPickuped = false;

                var dataSaved = new EventHandler((_, _) =>
                {
                    Interlocked.Increment(ref saveDetectedCount);
                    ConsoleEx.Log($"Saved; saveDetectedCount=[{saveDetectedCount}]");
                });
                var fishPickuped = new EventHandler((_, _) =>
                {
                    Interlocked.Exchange(ref saveDetectedCount, -2);
                    isPickuped = true;
                    ConsoleEx.Log($"Fish Pickuped; saveDetectedCount=[{saveDetectedCount}]");
                });
                _logWatcher.DataSaved += dataSaved;
                _logWatcher.FishPickuped += fishPickuped;
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
                        isPickuped = false;

                        var isTimeout = true;
                        do
                        {
                            Thread.Sleep(watchCycle);

                            if (saveDetectedCount > 0)
                            {
                                ConsoleEx.Log("Hit!");
                                isTimeout = false;
                                break;
                            }
                        }
                        while (sw.ElapsedMilliseconds < _waitTimeout);

                        if (isTimeout)
                        {
                            ConsoleEx.Log("Wait timeout");
                        }

                        ConsoleEx.Log($"Roll; Timeout=[{_rollTimeout}] ms");
                        _labelStatus.Dispatcher.Invoke(() => _labelStatus.Content = "Roll");
                        SendData(updClient, pressData);
                        sw.Restart();
                        isTimeout = true;
                        do
                        {
                            Thread.Sleep(watchCycle);
                            if (isPickuped && saveDetectedCount > -2)
                            {
                                ConsoleEx.Log("Put into bucket");
                                isTimeout = false;
                                Thread.Sleep(100);
                                break;
                            }
                        }
                        while (sw.ElapsedMilliseconds < _rollTimeout);

                        if (isTimeout)
                        {
                            ConsoleEx.Log("Roll timeout");
                            Interlocked.Exchange(ref saveDetectedCount, 0);
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
                    _logWatcher.DataSaved -= dataSaved;
                    _logWatcher.FishPickuped -= fishPickuped;
                    SendData(updClient, releaseData);
                    client.Dispose();
                }
            })
            {
                IsBackground = true
            };
            thread.Start(client);
            return thread;
        }

        /// <summary>
        /// Start new auto operation <see cref="Thread"/> for "Idle Fishing".
        /// </summary>
        /// <param name="client"><see cref="UdpClient"/> for OSC.</param>
        /// <returns>Created and started <see cref="Thread"/>.</returns>
        private Thread StartIdleFishingThread(UdpClient client)
        {
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

                var reelingStarted = new EventHandler((_, _) =>
                {
                    Interlocked.Increment(ref saveDetectedCount);
                    ConsoleEx.Log($"Start reeling; detected count=[{saveDetectedCount}]");
                });
                _logWatcher.ReelingStarted += reelingStarted;
                try
                {
                    const int watchCycle = 32;

                    while (true)
                    {
                        Interlocked.Exchange(ref saveDetectedCount, 0);

                        var chargeTime = _chargeTime;

                        ConsoleEx.Log($"Charge ...; [{chargeTime}] ms");
                        _labelStatus.Dispatcher.Invoke(() => _labelStatus.Content = "Charging");
                        SendData(updClient, pressData);
                        Thread.Sleep(chargeTime);

                        ConsoleEx.Log($"Release; Timeout=[{_waitTimeout}] ms");
                        _labelStatus.Dispatcher.Invoke(() => _labelStatus.Content = "Wait");
                        SendData(updClient, releaseData);
                        sw.Restart();

                        var isTimeout = true;
                        do
                        {
                            Thread.Sleep(watchCycle);

                            if (saveDetectedCount > 0)
                            {
                                ConsoleEx.Log("Hit!");
                                isTimeout = false;
                                break;
                            }
                        }
                        while (sw.ElapsedMilliseconds < _waitTimeout);

                        if (isTimeout)
                        {
                            ConsoleEx.Log("Wait timeout");
                        }

                        ConsoleEx.Log($"Roll; [{chargeTime * 3}] ms");
                        _labelStatus.Dispatcher.Invoke(() => _labelStatus.Content = "Roll");
                        SendData(updClient, pressData);
                        Thread.Sleep(chargeTime * 3);

                        SendData(updClient, releaseData);
                        Thread.Sleep(100);

                        ConsoleEx.Log($"Collect; [{1500}] ms");
                        _labelStatus.Dispatcher.Invoke(() => _labelStatus.Content = "Collect");
                        SendData(updClient, pressData);
                        Thread.Sleep(1000);

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
                    _logWatcher.ReelingStarted -= reelingStarted;
                    SendData(updClient, releaseData);
                    client.Dispose();
                }
            })
            {
                IsBackground = true
            };
            thread.Start(client);
            return thread;
        }

        /// <summary>
        /// Stop auto fishing.
        /// </summary>
        private void StopAutoFishing()
        {
            ConsoleEx.Log("Stop");

            _buttonStartStop.Content = "Start";
            _textBoxHost.IsEnabled = true;
            _nudPort.IsEnabled = true;
            _labelStatus.Foreground = new SolidColorBrush(Colors.Black);
            _labelStatus.Content = "Stop";
            Topmost = false;

            var thread = _thread;
            if (thread != null)
            {
                _thread = null;
                thread.Interrupt();
                thread.Join(1000);
            }
        }

        /// <summary>
        /// Update hot key.
        /// </summary>
        private void UpdateHotKey()
        {
            if ((_comboBoxHotKey.SelectedItem as ComboBoxItem)?.Content is not string text || text.Length == 0)
            {
                return;
            }

            var key = default(Keys);
            if (text.Length == 1 && char.IsDigit(text[0]))
            {
                // Digit key.
#if NETCOREAPP3_0_OR_GREATER
                key = Enum.Parse<Keys>("D" + text[0]);
#else
                key = (Keys)Enum.Parse(typeof(Keys), "D" + text[0]);
#endif  // NETCOREAPP3_0_OR_GREATER
            }
            else
            {
                // Alphabet key or Function key.
#if NETCOREAPP3_0_OR_GREATER
                key = Enum.Parse<Keys>(text);
#else
                key = (Keys)Enum.Parse(typeof(Keys), text);
#endif  // NETCOREAPP3_0_OR_GREATER
            }
            var modKey = GetModifilerKeys();

            try
            {
                _globalHotKeyManager.UnregisterAll();
                _globalHotKeyManager.Register(modKey, key);
                ConsoleEx.Log($"Re-Register Hot Key: [{modKey}][{key}]");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, ex.GetType().Name, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Get modifier key value from <see cref="_checkBoxShift"/>, <see cref="_checkBoxCtrl"/> and <see cref="_checkBoxAlt"/>.
        /// </summary>
        /// <returns>Modifier key value.</returns>
        private ModifierKeys GetModifilerKeys()
        {
            var modKey = ModifierKeys.None;
            if (_checkBoxShift.IsChecked.GetValueOrDefault())
            {
                modKey |= ModifierKeys.Shift;
            }
            if (_checkBoxCtrl.IsChecked.GetValueOrDefault())
            {
                modKey |= ModifierKeys.Control;
            }
            if (_checkBoxAlt.IsChecked.GetValueOrDefault())
            {
                modKey |= ModifierKeys.Alt;
            }
            return modKey;
        }

        /// <summary>
        /// <para>Called before main window closing.</para>
        /// <para>Ensure to stop <see cref="_thread"/>.</para>
        /// </summary>
        /// <param name="sender">`this` (Instance of the <see cref="MainWindow"/>).</param>
        /// <param name="e">Provides data for a cancelable event.</param>
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            _globalHotKeyManager.Dispose();
            StopAutoFishing();
        }

        /// <summary>
        /// Start or stop auto fishing.
        /// </summary>
        /// <param name="sender">Start/Stop toggle button.</param>
        /// <param name="e">Contains state information and event data associated with a routed event.</param>
        private void ButtonToggle_Click(object sender, RoutedEventArgs e)
        {
            ToggleStartStop((Button)sender);
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

        /// <summary>
        /// <para>Called when <see cref="_checkBoxShift"/>, <see cref="_checkBoxCtrl"/> or <see cref="_checkBoxAlt"/> is checked.</para>
        /// <para>Re-register hot key.</para>
        /// </summary>
        /// <param name="sender"><see cref="_checkBoxShift"/>, <see cref="_checkBoxCtrl"/> or <see cref="_checkBoxAlt"/>.</param>
        /// <param name="e">Contains state information and event data associated with a routed event.</param>
        private void CheckBoxModifierKey_Checked(object sender, RoutedEventArgs e)
        {
            UpdateHotKey();
        }

        /// <summary>
        /// <para>Called when <see cref="_checkBoxShift"/>, <see cref="_checkBoxCtrl"/> or <see cref="_checkBoxAlt"/> is checked.</para>
        /// <para>Re-register hot key.</para>
        /// </summary>
        /// <param name="sender"><see cref="_checkBoxShift"/>, <see cref="_checkBoxCtrl"/> or <see cref="_checkBoxAlt"/>.</param>
        /// <param name="e">Contains state information and event data associated with a routed event.</param>
        private void CheckBoxModifierKey_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateHotKey();
        }

        /// <summary>
        /// <para>Called when <see cref="_checkBoxShift"/>, <see cref="_checkBoxCtrl"/> or <see cref="_checkBoxAlt"/> is unchecked.</para>
        /// <para>Re-register hot key.</para>
        /// </summary>
        /// <param name="sender"><see cref="_comboBoxHotKey"/></param>
        /// <param name="e">Provides data for the <see cref="SelectionChangedEventHandler"/> event.</param>
        private void ComboBoxHotKey_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateHotKey();
        }

        /// <summary>
        /// <para>This method is called when a log that you joined to instance is detected.</para>
        /// <para>Update the world name label.</para>
        /// </summary>
        /// <param name="sender"><see cref="_logWatcher"/></param>
        /// <param name="e">An object that contains the instance information.</param>
        private void LogWatcher_JoinedToInstance(object sender, InstanceEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var instanceInfo = e.InstanceInfo;
                var worldId = instanceInfo.WorldId;
                _labelCurrentWorld.Foreground = new SolidColorBrush(
                    worldId == WorldIds.SimpleFishingWorld || worldId == WorldIds.IdleFishing ? Colors.Green : Colors.Black);
                _labelCurrentWorld.Content = e.InstanceInfo.WorldName;
            });
        }

        /// <summary>
        /// <para>This method is called when a log that you left from instance is detected.</para>
        /// <para>Stop auto fishing.</para>
        /// </summary>
        /// <param name="sender"><see cref="_logWatcher"/></param>
        /// <param name="e">An object that contains the instance information.</param>
        private void LogWatcher_LeftFromInstance(object sender, InstanceEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _labelCurrentWorld.Content = string.Empty;
                StopAutoFishing();
            });
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
