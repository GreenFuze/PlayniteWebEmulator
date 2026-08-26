using System;
using System.Windows.Forms;

namespace PlayniteWebEmulator.Launcher
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                var command = LaunchCommandLine.Parse(args);
                var response = new LaunchPipeClient().Run(command);
                if (!response.Succeeded)
                {
                    throw new InvalidOperationException(response.Error ?? "The Web Emulator session failed.");
                }

                return 0;
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    exception.GetBaseException().Message,
                    "Web Emulator",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}

