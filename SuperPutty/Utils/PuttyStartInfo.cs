using System;
using System.Linq;
using SuperPutty.Data;
using log4net;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace SuperPutty.Utils
{
    public class PuttyStartInfo
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(PuttyStartInfo));

        private static readonly Regex regExEnvVars = new Regex(@"(%\w+%)");

        public static String GetExecutable(SessionData session)
        {
            switch (session.Proto)
            {
                case ConnectionProtocol.Mintty:
                    return TryParseEnvVars(SuperPuTTY.Settings.MinttyExe);

                case ConnectionProtocol.VNC:
                    return TryParseEnvVars(SuperPuTTY.Settings.VNCExe);

                default:
                    return TryParseEnvVars(SuperPuTTY.Settings.PuttyExe);
            }
        }

        public PuttyStartInfo(SessionData session, SessionData session1)
        {
            Session = session1;
            string argsToLog = null;

            Executable = GetExecutable(session);

            if (session.Proto == ConnectionProtocol.Cygterm)
            {
                CygtermStartInfo cyg = new CygtermStartInfo(session);
                Args = cyg.Args;
                WorkingDir = cyg.StartingDir;
            }
            else if (session.Proto == ConnectionProtocol.Mintty)
            {
                MinttyStartInfo mintty = new MinttyStartInfo(session);
                Args = mintty.Args;
                WorkingDir = mintty.StartingDir;
            }
            else if (session.Proto == ConnectionProtocol.VNC)
            {
                VNCStartInfo vnc = new VNCStartInfo(session);
                Args = vnc.Args;
                WorkingDir = vnc.StartingDir;
            }
            else
            {
                Args = MakeArgs(session, true);
                argsToLog = MakeArgs(session, false);
            }

            // attempt to parse env vars
            Args = Args.Contains('%') ? TryParseEnvVars(Args) : Args;

            Log.InfoFormat("Putty Args: '{0}'", argsToLog ?? Args);
        }

        static string MakeArgs(SessionData session, bool includePassword)
        {
            if (!String.IsNullOrEmpty(session.Password) && includePassword && !SuperPuTTY.Settings.AllowPlainTextPuttyPasswordArg)
                Log.Warn("SuperPuTTY is set to NOT allow the use of the -pw <password> argument, this can be overriden in Tools -> Options -> GUI");

            string args = "-" + session.Proto.ToString().ToLower() + " ";
            args += !String.IsNullOrEmpty(session.Password) && SuperPuTTY.Settings.AllowPlainTextPuttyPasswordArg 
                ? "-pw " + (includePassword ? session.Password : "XXXXX") + " " 
                : "";
            args += "-P " + session.Port + " ";
            args += !String.IsNullOrEmpty(session.PuttySession) ? "-load \"" + session.PuttySession + "\" " : "";

            args += !String.IsNullOrEmpty(SuperPuTTY.Settings.PuttyDefaultParameters) ? SuperPuTTY.Settings.PuttyDefaultParameters + " " : "";

            //If extra args contains the password, delete it (it's in session.password)
            string extraArgs = CommandLineOptions.replacePassword(session.ExtraArgs,"");            
            args += !String.IsNullOrEmpty(extraArgs) ? extraArgs + " " : "";
            args += !String.IsNullOrEmpty(session.Username) ? " -l " + session.Username + " " : "";
            args += session.Host;

            return args;
        }

        static string TryParseEnvVars(string args)
        {
            string result = args;
            try
            {
                result = regExEnvVars.Replace(
                    args,
                    m =>
                    {
                        string name = m.Value.Trim('%');
                        return Environment.GetEnvironmentVariable(name) ?? m.Value;
                    });
            }
            catch(Exception ex)
            {
                Log.Warn("Could not parse env vars in args", ex);
            }
            return result;
        }

        /// <summary>
        /// Start of standalone putty process
        /// </summary>
        public void StartStandalone()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = Executable,
                Arguments = Args
            };
            if (WorkingDir != null && Directory.Exists(WorkingDir))
            {
                startInfo.WorkingDirectory = WorkingDir;
            }
            Process.Start(startInfo);
        }

        public SessionData Session { get; }

        public string Args { get; }
        public string WorkingDir { get; }
        public string Executable { get; }

    }
}
