using System;
using System.Reflection;
using System.Windows.Forms;
using log4net;
using System.Threading;

namespace SuperPuttyUnitTests
{
    class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        private static bool _initialized;

        public static void InitLoggingForUnitTests()
        {
            lock (Log)
            {
                if (!_initialized)
                {
                    log4net.Config.BasicConfigurator.Configure();
                    _initialized = true;
                }
            }
        }

        [STAThread]
        static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();
            Log.Info("Starting...");

            AppDomain.CurrentDomain.UnhandledException += (CurrentDomain_UnhandledException);
            Application.ThreadException += (Application_ThreadException);
            Application.EnableVisualStyles();
            Application.Run(new TestAppRunner());
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            string msg = string.Format(LocalizedText.Program_CurrentDomain_UnhandledException_isTerminating, e.IsTerminating, e.ExceptionObject);
            Log.Error(msg);
            MessageBox.Show(msg, LocalizedText.Program_CurrentDomain_UnhandledException_CurrentDomain_UnhandledException);
        }

        static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            string msg = String.Format(LocalizedText.Program_Application_ThreadException_, e.Exception);
            Log.Error(msg);
            MessageBox.Show(msg, LocalizedText.Program_Application_ThreadException_Application_ThreadException);
        }


        static void RunConsole()
        {
            string[] my_args = { Assembly.GetExecutingAssembly().Location };

            int returnCode = NUnit.ConsoleRunner.Runner.Main(my_args);

            if (returnCode != 0)
                Console.Beep();

            Console.WriteLine(LocalizedText.Program_RunConsole_Complete___Any_key_to_kill);
            Console.ReadLine();
        }

    }


}
