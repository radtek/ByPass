using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Sockets;

namespace ByPassProxy
{
    public class ConnectionCanceller : IDisposable
    {
        private readonly CancellationTokenSource _token;
        private readonly TcpClient _client;
        private readonly TcpClient _target;

        public ConnectionCanceller(CancellationTokenSource token, TcpClient client, TcpClient target)
        {
            _token = token;
            _client = client;
            _target = target;
        }

        public bool IsOwnerForAny(params TcpClient[] args)
        {
            if (args == null) return false;
            return args.Any(a => _client == a || _target == a);
        }

        public void Dispose()
        {
            try
            {
                if (_token != null) _token.Cancel();
                if (_client != null) _client.Close();
                if (_target != null) _target.Close();
            }
            catch { }
        }
    }
}
