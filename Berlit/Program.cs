using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace Berlit
{
    internal static class Program
    {
        /// <summary>
        /// Главная точка входа для приложения.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // Датчики температуры и напряжения читаются через драйвер
            // LibreHardwareMonitor, которому нужны права администратора.
            // Просим повышение один раз; если пользователь отказался —
            // приложение всё равно запускается, но без этих датчиков.
            if (!HasFlag(args, "--no-elevate") && !IsAdministrator() && TryStartElevated())
                return;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Падение фонового потока не должно молча закрывать монитор.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainException;

            Application.Run(new Berlit());
        }

        private static bool HasFlag(string[] args, string flag)
        {
            if (args == null) return false;
            foreach (string arg in args)
                if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static bool IsAdministrator()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Перезапуск с повышенными правами. false — пользователь отказался.</summary>
        private static bool TryStartElevated()
        {
            try
            {
                var info = new ProcessStartInfo(Application.ExecutablePath, "--no-elevate");
                info.UseShellExecute = true;
                info.Verb = "runas";
                info.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                Process.Start(info);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            Report(e.Exception);
        }

        private static void OnDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            Report(e.ExceptionObject as Exception);
        }

        private static void Report(Exception exception)
        {
            string message = exception == null ? "Неизвестная ошибка." : exception.Message;
            MessageBox.Show("Непредвиденная ошибка:\r\n\r\n" + message,
                "Berlit", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
