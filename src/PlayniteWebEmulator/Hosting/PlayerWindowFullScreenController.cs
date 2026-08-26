using System;
using System.Windows;
using System.Windows.Input;

namespace PlayniteWebEmulator.Hosting
{
    internal sealed class PlayerWindowFullScreenController : IDisposable
    {
        private readonly Window window;
        private readonly WindowStyle originalWindowStyle;
        private readonly ResizeMode originalResizeMode;
        private readonly WindowState originalWindowState;
        private bool isFullScreen;
        private bool disposed;

        public PlayerWindowFullScreenController(Window window)
        {
            this.window = window ?? throw new ArgumentNullException(nameof(window));
            originalWindowStyle = window.WindowStyle;
            originalResizeMode = window.ResizeMode;
            originalWindowState = window.WindowState;
            window.PreviewKeyDown += Window_PreviewKeyDown;
        }

        public void Handle(PlayerDiagnostic diagnostic)
        {
            if (diagnostic == null) throw new ArgumentNullException(nameof(diagnostic));
            if (!string.Equals(diagnostic.EventName, "fullscreen", StringComparison.OrdinalIgnoreCase)) return;
            SetFullScreen(string.Equals(diagnostic.Detail, "enter", StringComparison.OrdinalIgnoreCase));
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            window.PreviewKeyDown -= Window_PreviewKeyDown;
            if (isFullScreen) ApplyFullScreen(false);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs eventArgs)
        {
            if (eventArgs.Key == Key.F11)
            {
                SetFullScreen(!isFullScreen);
                eventArgs.Handled = true;
            }
            else if (eventArgs.Key == Key.Escape && isFullScreen)
            {
                SetFullScreen(false);
                eventArgs.Handled = true;
            }
        }

        private void SetFullScreen(bool enabled)
        {
            if (disposed || enabled == isFullScreen) return;
            if (!window.Dispatcher.CheckAccess())
            {
                window.Dispatcher.BeginInvoke(new Action(() => SetFullScreen(enabled)));
                return;
            }

            ApplyFullScreen(enabled);
        }

        private void ApplyFullScreen(bool enabled)
        {
            if (enabled)
            {
                window.WindowState = WindowState.Normal;
                window.WindowStyle = WindowStyle.None;
                window.ResizeMode = ResizeMode.NoResize;
                window.WindowState = WindowState.Maximized;
            }
            else
            {
                window.WindowState = WindowState.Normal;
                window.WindowStyle = originalWindowStyle;
                window.ResizeMode = originalResizeMode;
                window.WindowState = originalWindowState;
            }

            isFullScreen = enabled;
        }
    }
}
