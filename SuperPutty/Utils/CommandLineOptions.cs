using System;
using System.Collections.Generic;
using System.Text;
using SuperPutty.Data;
using log4net;
using System.Web;
using System.Text.RegularExpressions;

namespace SuperPutty.Utils
{
    /// <summary>
    /// Use:
    /// --------------------------------------------------------------------------
    /// SuperPutty.exe -layout LAYOUT_NAME
    /// OR
    /// SuperPutty.exe -session SESSION_NAME
    /// OR 
    /// SuperPutty.exe -[PROTOCOL] -P PORT -l USER -pw PASSWORD -load SETTINGS HOSTNAME
    /// OR 
    /// SuperPutty.exe -l USER -pw PASSWORD -load SETTINGS PROTOCOL://HOSTNAME:port
    /// ------------
    /// Options:
    /// -ssh|-serial|-telnet|-scp|-raw|-rlogin|-cygterm|-vnc  -Choose Protocol (default: ssh)
    /// -P                                                    -Port            (default: 22)
    /// -l                                                    -Login Name
    /// -pw                                                   -Login Password
    /// -load                                                 -Session to load (default: Default Session)
    /// ------------------------------------------------------------------------------
    /// SuperPutty.exe -layout LAYOUT_NAME
    /// SuperPutty.exe -session SESSION_ID
    /// SuperPutty.exe -ssh -P 22 -l homer -pw springfield -load pp1 prod-reactor
    /// SuperPutty.exe -l peter -pw donut foobar
    /// </summary>
    public class CommandLineOptions
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CommandLineOptions));

        public CommandLineOptions(string[] args)
        {
            try
            {
                if (args.Length == 1 && args[0].EndsWith("/") && args[0].Contains("://") && args[0].Contains("%20"))
                {
                    // special case for ssh links in browser
                    // ssh://localhost:22%20-l%20beau/
                    string cmdLine = HttpUtility.UrlDecode(args[0].TrimEnd('/'));
                    args = cmdLine.Split(' ');
                }
                if (args.Length > 0)
                {
                    Parse(args);
                    IsValid = true;
                }
                else
                {
                    // no args to consider
                    IsValid = false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("Error parsing args [{0}]", String.Join(" ", args)), ex);
                IsValid = false;
            }
        }

        void Parse(string[] args)
        {
            Log.InfoFormat("CommandLine: [{0}]", String.Join(" ", args));
            Queue<string> queue = new Queue<string>(args);
            while(queue.Count > 0)
            {
                var arg = queue.Dequeue();
                switch (arg)
                {
                    case "-layout":
                        Layout = queue.Dequeue();
                        break;
                    case "-session":
                        SessionId = queue.Dequeue();
                        break;
                    case "-ssh":
                    case "-ssh2":
                        Protocol = ConnectionProtocol.SSH;
                        break;
                    case "-telnet":
                        Protocol = ConnectionProtocol.Telnet;
                        break;
                    case "-rlogin":
                        Protocol = ConnectionProtocol.Rlogin;
                        break;
                    case "-raw":
                        Protocol = ConnectionProtocol.Raw;
                        break;
                    case "-serial":
                        Protocol = ConnectionProtocol.Serial;
                        break;
                    case "-cygterm":
                        Protocol = ConnectionProtocol.Cygterm;
                        break;
                    case "-vnc":
                        Protocol = ConnectionProtocol.VNC;
                        break;
                    case "-scp":
                        UseScp = true;
                        break;
                    case "-P":
                        Port = int.Parse(queue.Dequeue());
                        break;
                    case "-l":
                        UserName = queue.Dequeue();
                        break;
                    case "-pw":
                        Password = queue.Dequeue();
                        break;
                    case "-load":
                        PuttySession = queue.Dequeue();
                        break;
                    case "--help":
                        Help = true;
                        return;
                    default:
                        // unflagged arg must be the host...
                        Host = arg;
                        break;
                }
            }
        }



        /// <summary>
        /// Return the command value in allCommands (without quotes)              
        /// </summary>
        /// <param name="allCommands">String contains all commands</param>
        /// <param name="command">command to search: ej "-pw"</param>
        /// <returns>the value of command  )</returns>
        public static String getcommand(String allCommands, String command)
        {

            if (String.IsNullOrEmpty(allCommands))
            {
                return "";
            }
            string strRegex = command + @"[=:\s](?:""([^""]*)""|([^""\s]+))";
            Regex myRegex = new Regex(strRegex, RegexOptions.Compiled);
            foreach (Match myMatch in myRegex.Matches(allCommands))
            {
                if (myMatch.Success)
                {
                    return String.IsNullOrEmpty(myMatch.Groups[1].Value) ? myMatch.Groups[2].Value : myMatch.Groups[1].Value;
                }
            }
            return "";
        }


        /// <summary>
        /// replace the value of "-pw" command included in allcomands with text
        /// </summary>
        /// <param name="allCommands"></param>
        /// <param name="text"></param>
        /// <returns></returns>
        public static String replacePassword(String allCommands, String text)
        {            
            if (String.IsNullOrEmpty(allCommands))
            {
                return "";
            }
            string strRegex = @"(-pw[=:\s])(""[^""]*""|[^""\s]+)";
            Regex myRegex = new Regex(strRegex, RegexOptions.Compiled);

            foreach (Match myMatch in myRegex.Matches(allCommands))
            {
                if (myMatch.Success)
                {
                    allCommands = Regex.Replace(allCommands, strRegex, String.IsNullOrEmpty(text) ? "" : "${1}" + text);                                       
                }
            }
            return allCommands;
        }

        public SessionDataStartInfo ToSessionStartInfo()
        {
            SessionDataStartInfo ssi = null;
            if (SessionId != null)
            {
                // first try to resolve by sessionId
                SessionData session = SuperPuTTY.GetSessionById(SessionId);
                if (session == null)
                {
                    Log.WarnFormat("Session from command line not found, id={0}", SessionId);
                }
                else
                {
                    ssi = new SessionDataStartInfo 
                    { 
                        Session = session, 
                        UseScp = UseScp 
                    };
                }
            }
            else if (Host != null ||  PuttySession != null)
            {
                // Host or puttySession provided
                string sessionName;
                if (Host != null)
                {
                    // Decode URL type host spec, if provided (e.g. ssh://localhost:2020)
                    HostConnectionString connStr = new HostConnectionString(Host);
                    Host = connStr.Host;
                    Protocol = connStr.Protocol.GetValueOrDefault(Protocol.GetValueOrDefault(ConnectionProtocol.SSH));
                    Port = connStr.Port.GetValueOrDefault(Port.GetValueOrDefault(dlgEditSession.GetDefaultPort(Protocol.GetValueOrDefault())));
                    sessionName = Host;
                }
                else
                {
                    // no host provided so assume sss
                    sessionName = PuttySession;
                }

                ssi = new SessionDataStartInfo
                {
                    Session = new SessionData
                    {
                        Host = Host,
                        SessionName = sessionName,
                        SessionId = SuperPuTTY.MakeUniqueSessionId(SessionData.CombineSessionIds("CLI", Host)),
                        Port = Port.GetValueOrDefault(22),
                        Proto = Protocol.GetValueOrDefault(ConnectionProtocol.SSH),
                        Username = UserName,
                        Password = Password,
                        PuttySession = PuttySession
                    },
                    UseScp = UseScp
                };
            }

            if (ssi == null)
            {
                Log.WarnFormat("Could not determine session or host to connect.  SessionId or Host or PuttySession must be provided");
            }

            return ssi;
        }

        /// <summary>
        /// Return usage string
        /// </summary>
        /// <returns></returns>
        public static string Usage()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Usage:");
            sb.AppendLine("");
            sb.AppendLine("  SuperPutty.exe -session SESSION");
            sb.AppendLine("  SuperPutty.exe -layout LAYOUT");
            sb.AppendLine("  SuperPutty.exe -load SETTINGS");
            sb.AppendLine("  SuperPutty.exe -PROTO -P PORT -l USER -pw PASSWORD -load SETTINGS HOST");
            sb.AppendLine("  SuperPutty.exe -l USER -pw PASSWORD -load SETTINGS PROTO://HOST:PORT");
            sb.AppendLine();
            sb.AppendLine("Options:");
            sb.AppendLine();
            sb.AppendLine("  SESSION\t\t - Session id");
            sb.AppendLine("  LAYOUT\t\t - Layout name");
            sb.AppendLine("  SETTINGS\t - Putty Saved Session Profile");
            sb.AppendLine("  PROTO\t\t - Protocol - (ssh|ssh2|telnet|serial|raw|scp|cygterm|rlogin|mintty|vnc)");
            sb.AppendLine("  USER\t\t - User name");
            sb.AppendLine("  PASSWORD\t - Login Password");
            sb.AppendLine("  HOST\t\t - Hostname");
            sb.AppendLine();
            sb.AppendLine("Examples:");
            sb.AppendLine();
            sb.AppendLine("  SuperPutty.exe -session nyc-qa-1");
            sb.AppendLine("  SuperPutty.exe -layout prod");
            sb.AppendLine("  SuperPutty.exe -ssh -P 22 -l homer -pw springfield -load pp1 prod-reactor");
            sb.AppendLine("  SuperPutty.exe -vnc stewie01");
            sb.AppendLine("  SuperPutty.exe -l peter -pw griffin stewie01");
            sb.AppendLine("  SuperPutty.exe -load localhost");
            
            return sb.ToString();
        }

        public string ExePath { get; private set; }
        public string Layout { get; private set; }
        public string SessionId { get; private set; }
        public bool IsValid { get; private set; }

        public bool UseScp { get; private set; }
        public ConnectionProtocol? Protocol { get; private set; }
        public int? Port { get; private set; }
        public string UserName { get; private set; }
        public string Password { get; private set; }
        public string PuttySession { get; private set; }

        public string Host { get; private set; }
        public bool Help { get; private set; }

    }
}
