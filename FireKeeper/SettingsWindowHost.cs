// SettingsWindowHost.cs - Hosts SettingsWindow (WPF) on its own dedicated STA thread/Dispatcher.
//
// The rest of FireKeeper runs on a classic WinForms Application.Run() message loop
// (BackupTrayContext : ApplicationContext). WPF windows need a System.Windows.Threading.Dispatcher
// pumping their own message loop, and the two don't share one safely - mixing them on a single
// thread leads to timers/animations/async continuations that never get pumped. The standard,
// well-established pattern for "WinForms tray app opens a WPF window" is to run that window on
// its own STA thread with Dispatcher.Run(), which is what this class does.
using System;
using System.Threading;
using System.Windows.Threading;

namespace FireKeeper
{
    public class SettingsWindowHost
    {
        private readonly Config config;
        private readonly BackupTrayContext context;
        private SettingsWindow window;
        private Dispatcher dispatcher;

        /// <summary>Raised (on the caller's thread, via ThreadPool) after the window closes.</summary>
        public event Action Closed;

        public bool IsOpen => window != null;

        public SettingsWindowHost(Config cfg, BackupTrayContext ctx)
        {
            config = cfg;
            context = ctx;
        }

        /// <summary>Opens the settings window, or brings it to the front if already open.</summary>
        public void ShowOrActivate()
        {
            if (IsOpen)
            {
                dispatcher.Invoke(() =>
                {
                    if (window.WindowState == System.Windows.WindowState.Minimized)
                        window.WindowState = System.Windows.WindowState.Normal;
                    window.Activate();
                });
                return;
            }

            var ready = new ManualResetEventSlim(false);

            var uiThread = new Thread(() =>
            {
                dispatcher = Dispatcher.CurrentDispatcher;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));

                window = new SettingsWindow(config, context);
                window.Closed += (s, e) =>
                {
                    window = null;
                    Closed?.Invoke();
                    dispatcher.InvokeShutdown();
                };

                ready.Set();
                window.Show();
                Dispatcher.Run();
            });
            uiThread.SetApartmentState(ApartmentState.STA);
            uiThread.IsBackground = true;
            uiThread.Name = "FireKeeper-SettingsWindow";
            uiThread.Start();

            ready.Wait();
        }

        /// <summary>Thread-safe: marshals the progress update onto the settings window's own Dispatcher.</summary>
        public void UpdateProgress(int percent, string status)
        {
            var d = dispatcher;
            var w = window;
            if (d == null || w == null) return;

            d.BeginInvoke(new Action(() => w.UpdateProgress(percent, status)));
        }

        /// <summary>Closes the window (if open) and waits for it to finish closing.</summary>
        public void Close()
        {
            var d = dispatcher;
            var w = window;
            if (d == null || w == null) return;

            d.Invoke(() => w.Close());
        }
    }
}
