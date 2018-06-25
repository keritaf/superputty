using System;
using System.Windows.Forms;
using log4net.Appender;
using log4net;
using log4net.Repository.Hierarchy;
using log4net.Core;
using log4net.Layout;
using System.IO;

namespace SuperPutty
{
    public partial class Log4netLogViewer : ToolWindow
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Log4netLogViewer));

        private MemoryAppender memoryAppender;
        private Hierarchy repository;
        private StringWriter msgWriter;

        public Log4netLogViewer()
        {
            InitializeComponent();

            InitLogger();

            // start pulling log messages
            timerLogPull.Start();

            Log.Info("Viewer Ready (" + AssemblyInfo.Version + ")");
        }

        /// <summary>
        /// Init log4net memoryAppender 
        /// http://dhvik.blogspot.com/2008/08/adding-appender-to-log4net-in-runtime.html
        /// </summary>
        void InitLogger()
        {
            //First create and configure the appender  
            memoryAppender = new MemoryAppender {Name = GetType().Name + "MemoryAppender"};

            PatternLayout layout = new PatternLayout
            {
                ConversionPattern = "%date %-5level %20.20logger{1} - %message%newline"
            };
            layout.ActivateOptions();
            memoryAppender.Layout = layout;

            //Notify the appender on the configuration changes  
            memoryAppender.ActivateOptions();

            //Get the logger repository hierarchy.  
            repository = (Hierarchy)LogManager.GetRepository();

            //and add the appender to the root level  
            //of the logging hierarchy  
            repository.Root.AddAppender(memoryAppender);

            //configure the logging at the root.  
            //this.repository.Root.Level = Level.Info;

            //mark repository as configured and notify that is has changed.  
            repository.Configured = true;
            repository.RaiseConfigurationChanged(EventArgs.Empty);

            msgWriter = new StringWriter();
        }

        void DisposeLogger()
        {
            // remove appender and drain events
            repository.Root.RemoveAppender(memoryAppender);
            memoryAppender.Clear();

            //mark repository as configured and notify that is has changed.  
            repository.Configured = true;
            repository.RaiseConfigurationChanged(EventArgs.Empty);
        }

        private void Log4netLogViewer_FormClosing(object sender, FormClosingEventArgs e)
        {
            Log.Info("Shutting down logViewer");
            timerLogPull.Stop();
            DisposeLogger();
        }

        private void timerLogPull_Tick(object sender, EventArgs e)
        {
            timerLogPull.Stop();
            try
            {
                LoggingEvent[] events = memoryAppender.GetEvents();
                memoryAppender.Clear();
                foreach (LoggingEvent logEvent in events)
                {
                    memoryAppender.Layout.Format(msgWriter, logEvent);
                    richTextBoxLogMessages.AppendText(msgWriter.GetStringBuilder().ToString());
                    richTextBoxLogMessages.ScrollToCaret();
                    msgWriter.GetStringBuilder().Remove(0, msgWriter.GetStringBuilder().Length);
                    //this.richTextBoxLogMessages.AppendText(Environment.NewLine);
                }
            }
            finally
            {
                timerLogPull.Start();
            }
        }

    }
}
