using System;
using SuperPutty.Data;
using log4net;

namespace SuperPutty.Utils
{
    /// <summary>
    /// Helper class to parse host connection strings (e.g. ssh://localhost:2222)
    /// </summary>
    public class HostConnectionString
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(HostConnectionString));

        public HostConnectionString(string hostString, bool ignorePort = false)
        {

            int idx = hostString.IndexOf("://", StringComparison.Ordinal);
            string hostPort = hostString;
            if (idx != -1)
            {
                // ssh://localhost:2020, or ssh2://localhost:2020
                Protocol = (ConnectionProtocol)Enum.Parse(typeof(ConnectionProtocol), hostString.Substring(0, idx), true);
                if (Protocol == ConnectionProtocol.SSH2)
                {
                    // ssh2 is accepted only as a compatibility feature
                    Protocol = ConnectionProtocol.SSH;
                }
                hostPort = hostString.Substring(idx + 3);
            }

            // Firefox addes a '/'...
            hostPort = hostPort.TrimEnd('/');
            int idxPort = hostPort.IndexOf(":", StringComparison.Ordinal);
            if (idxPort != -1)
            {
                // localhost:2020
                if (!ignorePort && (int.TryParse(hostPort.Substring(idxPort + 1), out var port)))
                {
                    Host = hostPort.Substring(0, idxPort);
                    Port = port;
                }
                else
                {
                    Host = hostPort;
                }

            }
            else
            {
                // localhost
                Host = hostPort;
            }


            log.InfoFormat(
                "Parsed[{0}]: proto={1}, host={2}, port={3}", 
                hostString, 
                Protocol.HasValue ? Protocol.ToString() : "", 
                Host, 
                Port.HasValue ? Port.ToString() : "");
        }

        public ConnectionProtocol? Protocol { get; set; }
        public string Host { get; set; }
        public int? Port { get; set; }
    }
}
