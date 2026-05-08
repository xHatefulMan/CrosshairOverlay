using System.Runtime.InteropServices;

namespace CrosshairApp
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        static readonly Mutex mutex = new Mutex(false, "CrosshairApp_SingleInstance_v1");

        [STAThread]
        static void Main()
        {
            bool hasHandle = false;
            try
            {
                hasHandle = mutex.WaitOne(1000, false);
            }
            catch (AbandonedMutexException)
            {
                hasHandle = true;
            }

            if (!hasHandle)
            {
                MessageBox.Show(
                    "Crosshair Overlay est déjà lancé.\nVérifiez l'icône en bas à droite.",
                    "Déjà lancé",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                SetProcessDPIAware();
                Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
                ApplicationConfiguration.Initialize();

                bool startedWithWindows = Environment.GetCommandLineArgs().Contains("--startup");

                var form = new Form1();
                form.ShowInTaskbar = false;

                if (startedWithWindows)
                {
                    // Démarrage Windows → fenêtre cachée
                    form.Opacity = 0;
                    form.Show();
                    form.Hide();
                    form.Opacity = 1;
                    Application.Run();
                }
                else
                {
                    // Lancement manuel → fenêtre visible
                    Application.Run(form);
                }
            }
            finally
            {
                if (hasHandle) mutex.ReleaseMutex();
            }
        }
    }
}