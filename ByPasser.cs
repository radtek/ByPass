using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ByPassProxy
{
    public interface IByPasser
    {
        int Write(Stream stream, byte[] buffer, int index, int bytesRead);
    }
    public class ByPasser : IByPasser
    {
        public static ByPasser Default { get { return new ByPasser(); } }

        public virtual int Write(Stream stream, byte[] buffer, int index, int bytesRead)
        {
            stream.Write(buffer, index, bytesRead);
            return bytesRead;
        }
    }

    public class HostReplaceBypasser : IByPasser
    {
        private readonly IByPasser _piggyback;
        private readonly string _realHost;

        public HostReplaceBypasser(string realHost, IByPasser piggyback = null)
        {
            _realHost = realHost;
            _piggyback = piggyback != null ? piggyback : ByPasser.Default;
        }

        public int Write(Stream stream, byte[] buffer, int index, int bytesRead)
        {
            ASCIIEncoding encoder = new ASCIIEncoding();
            var request = encoder.GetString(buffer, index, bytesRead);

            if (request.Contains("Host: localhost"))
            {
                request = request.Replace("Host: localhost:8080", "Host: " + _realHost+":80");
                buffer = encoder.GetBytes(request);
                index = 0;
                bytesRead = buffer.Length;
            }

            return _piggyback.Write(stream, buffer, index, bytesRead);
        }
    }

    public class LoggingBypasser : IByPasser
    {
        private readonly IByPasser _piggyback;
        private readonly Action<byte[], int, int> _logger;

        public LoggingBypasser(Action<byte[], int, int> logger, IByPasser piggyback = null)
        {
            _piggyback = piggyback != null ? piggyback : ByPasser.Default;
            _logger = logger;
        }

        public int Write(Stream stream, byte[] buffer, int index, int bytesRead)
        {
            _logger(buffer, index, bytesRead);
            return _piggyback.Write(stream, buffer, index, bytesRead);
        }
    }
}
