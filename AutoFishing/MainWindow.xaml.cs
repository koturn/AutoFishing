using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using NumericUpDownLib;
using Koturn.Windows.GlobalHotKeys;
using Koturn.VRChat.Log.Events;
using AutoFishing.Enums;
using AutoFishing.Events;


namespace AutoFishing
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// OSC data byte sequence of press trigger on the right hand.
        /// </summary>
        private static readonly byte[] PressData = Encoding.ASCII.GetBytes("/input/UseRight\x00,i\x00\x00\x00\x00\x00\x01");
        /// <summary>
        /// OSC data byte sequence of release trigger on the right hand.
        /// </summary>
        private static readonly byte[] ReleaseData = Encoding.ASCII.GetBytes("/input/UseRight\x00,i\x00\x00\x00\x00\x00\x00");

        /// <summary>
        /// Customized VRChat log watcher.
        /// </summary>
        private readonly FishingLogWatcher _logWatcher = new();
        /// <summary>
        /// Array of the <see cref="IdleFishingRingEventArgs"/>.
        /// </summary>
        /// <remarks>This array has two elements only.</remarks>
        private readonly IdleFishingRingEventArgs?[] _idleFishingRingEventArgsArray = new IdleFishingRingEventArgs[2];
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
        /// Minimal input interval (milliseconds).
        /// </summary>
        private int _minimalInputInterval = 33;

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
            _logWatcher.RingReserved += LogWatcher_RingReserved;
            _logWatcher.RingActivated += LogWatcher_RingActivated;
            _logWatcher.RingDeactivated += LogWatcher_RingDeactivated;
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

                int saveDetectedCount = 0;

                var mreFishBite = new ManualResetEvent(false);
                var mrePickup = new ManualResetEvent(false);
                var dataSaved = new EventHandler((_, _) =>
                {
                    mrePickup.Set();

                    var newCount = Interlocked.Increment(ref saveDetectedCount);
                    if (newCount > 0)
                    {
                        mreFishBite.Set();
                    }
                    ConsoleEx.Log($"Saved; saveDetectedCount=[{newCount}]");
                });
                var fishPickuped = new EventHandler((_, _) =>
                {
                    var newCount = Interlocked.Exchange(ref saveDetectedCount, -2);
                    ConsoleEx.Log($"Fish Pickuped; saveDetectedCount=[{newCount}]");
                });
                _logWatcher.DataSaved += dataSaved;
                _logWatcher.FishPickuped += fishPickuped;
                try
                {
                    while (true)
                    {
                        //
                        // Hold for the duration of the charge time.
                        //
                        updClient.Send(PressData, PressData.Length);
                        ConsoleEx.Log($"Charge ...; [{_chargeTime}] ms");
                        _labelStatus.Dispatcher.Invoke(() => _labelStatus.Content = "Charging");
                        Thread.Sleep(_chargeTime);

                        //
                        // Unhold and wait for the bite.
                        //
                        updClient.Send(ReleaseData, ReleaseData.Length);
                        ConsoleEx.Log($"Release; Timeout=[{_waitTimeout}] ms");
                        _labelStatus.Dispatcher.Invoke(() => _labelStatus.Content = "Wait");

                        mreFishBite.Reset();
                        if (!mreFishBite.WaitOne(_waitTimeout))
                        {
                            ConsoleEx.Log("Wait timeout");
                        }

                        //
                        // Hold it until you reel it in.
                        //
                        updClient.Send(PressData, PressData.Length);
                        ConsoleEx.Log($"Start reeling; Timeout=[{_rollTimeout}] ms");
                        _labelStatus.Dispatcher.Invoke(() => _labelStatus.Content = "Reeling");
                        mrePickup.Reset();
                        if (mrePickup.WaitOne(_rollTimeout))
                        {
                            ConsoleEx.Log("Put into bucket");
                            Thread.Sleep(_minimalInputInterval);  // Wait for put into bucket.
                        }
                        else
                        {
                            var newCount = Interlocked.Exchange(ref saveDetectedCount, 0);
                            ConsoleEx.Log($"Roll timeout; saveDetectedCount=[{newCount}]");
                        }

                        //
                        // Unhold for next hold.
                        //
                        updClient.Send(ReleaseData, ReleaseData.Length);
                        Thread.Sleep(_minimalInputInterval);
                    }
                }
                catch (ThreadInterruptedException)
                {
                    // Do nothing
                }
                finally
                {
                    _logWatcher.FishPickuped -= fishPickuped;
                    _logWatcher.DataSaved -= dataSaved;
                    mrePickup.Dispose();
                    mreFishBite.Dispose();
                    updClient.Send(ReleaseData, ReleaseData.Length);
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

                var mreFishBite = new ManualResetEvent(false);
                var reelingStarted = new EventHandler((_, _) =>
                {
                    mreFishBite.Set();
                    ConsoleEx.Log("Reeling log detected");
                });
                _logWatcher.ReelingStarted += reelingStarted;
                try
                {
                    while (true)
                    {
                        var chargeTime = _chargeTime;

                        //
                        // Hold for the duration of the charge time.
                        //
                        ConsoleEx.Log($"Charge ...; [{chargeTime}] ms");
                        _labelStatus.Dispatcher.Invoke(() => _labelStatus.Content = "Charging");
                        updClient.Send(PressData, PressData.Length);
                        Thread.Sleep(chargeTime);

                        //
                        // Unhold and wait for the bite.
                        //
                        ConsoleEx.Log($"Release; Timeout=[{_waitTimeout}] ms");
                        _labelStatus.Dispatcher.Invoke(() => _labelStatus.Content = "Wait");
                        updClient.Send(ReleaseData, ReleaseData.Length);

                        mreFishBite.Reset();
                        if (!mreFishBite.WaitOne(_waitTimeout))
                        {
                            ConsoleEx.Log("Wait timeout");
                        }

                        //
                        // Hold it until you reel it in.
                        //
                        var reelingTime = Math.Max(900, chargeTime * 3);
                        ConsoleEx.Log($"Start reeling; [{reelingTime}] ms");
                        _labelStatus.Dispatcher.Invoke(() => _labelStatus.Content = "Reeling");
                        updClient.Send(PressData, PressData.Length);
                        Thread.Sleep(reelingTime);

                        //
                        // Unhold for next hold.
                        //
                        updClient.Send(ReleaseData, ReleaseData.Length);
                        Thread.Sleep(_minimalInputInterval);

                        //
                        // Hold to collect cached fish.
                        //
                        ConsoleEx.Log($"Collect; [{1000}] ms");
                        _labelStatus.Dispatcher.Invoke(() => _labelStatus.Content = "Collect");
                        updClient.Send(PressData, PressData.Length);
                        Thread.Sleep(1000);

                        //
                        // Unhold for next hold.
                        //
                        updClient.Send(ReleaseData, ReleaseData.Length);
                        Thread.Sleep(_minimalInputInterval);
                    }
                }
                catch (ThreadInterruptedException)
                {
                    // Do nothing
                }
                finally
                {
                    _logWatcher.ReelingStarted -= reelingStarted;
                    mreFishBite.Dispose();
                    updClient.Send(ReleaseData, ReleaseData.Length);
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
            _buttonStartStop.Content = "Start";
            _textBoxHost.IsEnabled = true;
            _nudPort.IsEnabled = true;
            _labelStatus.Foreground = new SolidColorBrush(Colors.Black);
            _labelStatus.Content = "Stop";
            Topmost = false;

            var thread = _thread;
            if (thread != null)
            {
                ConsoleEx.Log("Stop");
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
        /// <para>This method is called when the value of <see cref="_nudInputInterval"/> is changed.</para>
        /// <para>Change minimal input interval.</para>
        /// </summary>
        /// <param name="sender"><see cref="_nudInputInterval"/></param>
        /// <param name="e">An object that contains the old value and new value.</param>
        private void NudInputInterval_ValueChanged(object sender, RoutedPropertyChangedEventArgs<ushort> e)
        {
            _minimalInputInterval = (int)e.NewValue;
        }

        /// <summary>
        /// <para>This method is called when the value of <see cref="_nudWatchCycle"/> is changed.</para>
        /// <para>Change watcing cycle of the log files.</para>
        /// </summary>
        /// <param name="sender"><see cref="_nudWatchCycle"/></param>
        /// <param name="e">An object that contains the old value and new value.</param>
        private void NudWatchCycle_ValueChanged(object sender, RoutedPropertyChangedEventArgs<ushort> e)
        {
            _logWatcher.WatchCycle = (int)e.NewValue;
        }

        /// <summary>
        /// <para>This method is called when the checkbox of "Picked up and Start" is checked.</para>
        /// <para>Add callback method to <see cref="FishingLogWatcher.FishPickuped"/>.</para>
        /// </summary>
        /// <param name="sender">Source checkbox.</param>
        /// <param name="e">Contains state information and event data associated with a routed event.</param>
        private void CheckBoxPickedUpAndStart_Checked(object sender, RoutedEventArgs e)
        {
            _logWatcher.RodPickedUp += LogWatcher_RodPickedUp;
        }

        /// <summary>
        /// <para>This method is called when the checkbox of "Picked up and Start" is unchecked.</para>
        /// <para>Remove callback method to <see cref="FishingLogWatcher.FishPickuped"/>.</para>
        /// </summary>
        /// <param name="sender">Source checkbox.</param>
        /// <param name="e">Contains state information and event data associated with a routed event.</param>
        private void CheckBoxPickedUpAndStart_Unchecked(object sender, RoutedEventArgs e)
        {
            _logWatcher.RodPickedUp -= LogWatcher_RodPickedUp;
        }

        /// <summary>
        /// <para>This method is called when the checkbox of "Dropped and stop" is checked.</para>
        /// <para>Add callback method to <see cref="FishingLogWatcher.FishPickuped"/>.</para>
        /// </summary>
        /// <param name="sender">Source checkbox.</param>
        /// <param name="e">Contains state information and event data associated with a routed event.</param>
        private void CheckBoxDroppedAndStop_Checked(object sender, RoutedEventArgs e)
        {
            _logWatcher.RodDropped += LogWatcher_RodDropped;
        }

        /// <summary>
        /// <para>This method is called when the checkbox of "Dropped and stop" is unchecked.</para>
        /// <para>Remove callback method to <see cref="FishingLogWatcher.FishPickuped"/>.</para>
        /// </summary>
        /// <param name="sender">Source checkbox.</param>
        /// <param name="e">Contains state information and event data associated with a routed event.</param>
        private void CheckBoxDroppedAndStop_Unchecked(object sender, RoutedEventArgs e)
        {
            _logWatcher.RodDropped -= LogWatcher_RodDropped;
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
                _statusBarItemInformation.Content = string.Empty;
                StopAutoFishing();
            });
        }

        /// <summary>
        /// <para>This method is called when a rod is picked up.</para>
        /// <para>Stop auto fishing.</para>
        /// </summary>
        /// <param name="sender"><see cref="_logWatcher"/></param>
        /// <param name="e">An object that contains the picked up object.</param>
        private void LogWatcher_RodPickedUp(object sender, ObjectPickedupEventArgs e)
        {
            Dispatcher.Invoke(StartAutoFishing);
        }

        /// <summary>
        /// <para>This method is called when a rod is dropped.</para>
        /// <para>Stop auto fishing.</para>
        /// </summary>
        /// <param name="sender"><see cref="_logWatcher"/></param>
        /// <param name="e">An object that contains the dropped object.</param>
        private void LogWatcher_RodDropped(object sender, ObjectDroppedEventArgs e)
        {
            Dispatcher.Invoke(StopAutoFishing);
        }

        /// <summary>
        /// <para>This method is called when a new ring is reserved.</para>
        /// <para>Update <see cref="_statusBarItemInformation"/>.</para>
        /// </summary>
        /// <param name="sender"><see cref="_logWatcher"/></param>
        /// <param name="e">An object that contains the ring event information.</param>
        private void LogWatcher_RingReserved(object sender, IdleFishingRingEventArgs e)
        {
            _idleFishingRingEventArgsArray[e.Index] = e;
            UpdateInformationForRingEvent();
            ConsoleEx.Log($"Ring at [{e.Position}][{(e.Kind == RingKind.Blue ? "Blue" : "Yellow")}][Reserved]");
        }

        /// <summary>
        /// <para>This method is called when a new ring is reserved.</para>
        /// <para>Update <see cref="_statusBarItemInformation"/>.</para>
        /// </summary>
        /// <param name="sender"><see cref="_logWatcher"/></param>
        /// <param name="e">An object that contains the ring event information.</param>
        private void LogWatcher_RingActivated(object sender, IdleFishingRingEventArgs e)
        {
            _idleFishingRingEventArgsArray[e.Index] = e;
            UpdateInformationForRingEvent();
            ConsoleEx.Log($"Ring at [{e.Position}][{(e.Kind == RingKind.Blue ? "Blue" : "Yellow")}][Activated]");
        }

        /// <summary>
        /// <para>This method is called when a new ring is reserved.</para>
        /// <para>Update <see cref="_statusBarItemInformation"/>.</para>
        /// </summary>
        /// <param name="sender"><see cref="_logWatcher"/></param>
        /// <param name="e">An object that contains the ring event information.</param>
        private void LogWatcher_RingDeactivated(object sender, IdleFishingRingEventArgs e)
        {
            _idleFishingRingEventArgsArray[e.Index] = null;
            UpdateInformationForRingEvent();
            ConsoleEx.Log($"Ring at [{e.Position}][{(e.Kind == RingKind.Blue ? "Blue" : "Yellow")}][Deactivated]");
        }

        /// <summary>
        /// Update content of <see cref="_statusBarItemInformation"/> for ring information.
        /// </summary>
        private void UpdateInformationForRingEvent()
        {
            var sb = new StringBuilder();
            foreach (var e in _idleFishingRingEventArgsArray)
            {
                if (e == null)
                {
                    continue;
                }
                if (sb.Length > 0)
                {
                    sb.Append("; ");
                }
                sb.AppendFormat("{0} Ring: {1}", e.Kind == RingKind.Blue ? "Blue" : "Yellow", e.Position);
                if (!e.IsActivated)
                {
                    sb.Append(" (Reserved)");
                }
            }
            if (sb.Length == 0)
            {
                Dispatcher.Invoke(() => _statusBarItemInformation.Content = string.Empty);
            }
            else
            {
                Dispatcher.Invoke(() => _statusBarItemInformation.Content = sb.ToString());
            }
        }
    }
}
