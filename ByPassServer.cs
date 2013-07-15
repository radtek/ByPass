using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using System.Threading.Tasks;

namespace ByPassProxy
{
    public class ProxyService
    {
        #region Fileds
        //================================================================================
        private readonly object _locker = new object();
        private ILogger _logger;
        private int _listenPort;
        private string _toHost;
        private int _toPort;

        private TcpListener _tcpListener;
        CancellationTokenSource _listenerToken;

        List<ConnectionCanceller> _sessionCancelTokens;

        private bool _isRunning;
        private int _latency = 0;
        private bool _isSingleConnection = false;

        private IByPasser _clientLogger = null;
        private IByPasser _targetLogger = null;
        //================================================================================
        #endregion

        #region Constructor
        //================================================================================
        public ProxyService(int listenPort, string toHost, int toPort, ILogger logger,
            IByPasser clientLogger = null, IByPasser targetLogger = null)
        {
            _logger = logger;
            _listenPort = listenPort;
            _toHost = toHost;
            _toPort = toPort;

            _clientLogger = clientLogger != null ? clientLogger : ByPasser.Default;
            _targetLogger = targetLogger != null ? targetLogger : ByPasser.Default;
        }
        //================================================================================
        #endregion

        #region Properties and Events
        //================================================================================
        public class ByPassStateEventArgs : EventArgs { public int ActiveSessions { get; set; } public bool IsRunning { get; set; } }
        public event EventHandler<ByPassStateEventArgs> ByPassStateChanged = delegate { };
        //================================================================================
        public int ActiveSessions { get; private set; }
        public bool IsRunning { get { return _isRunning; } }
        //================================================================================
        #endregion

        #region Public Methods
        //================================================================================
        public void StopProxy()
        {
            if (!_isRunning) return;

            _listenerToken.Cancel();
            _tcpListener.Stop();

            _sessionCancelTokens.ForEach(a => a.Dispose());
            _sessionCancelTokens.Clear();

            _isRunning = false;
            RaiseStateChanged(0, false);
        }
        //================================================================================
        public void StartProxy()
        {
            if (!_isRunning)
            {
                _isRunning = true;
                _tcpListener = new TcpListener(IPAddress.Any, _listenPort);
                _tcpListener.Start();

                _sessionCancelTokens = new List<ConnectionCanceller>();
                _listenerToken = new CancellationTokenSource();
                RaiseStateChanged(0, true);
                Task.Factory.StartNew(StartProxySync, _listenerToken.Token);
            }
        }
        //================================================================================
        public void SetLatency(int ms)
        {
            _latency = ms;
        }
        public void SetIsSingleConnection(bool isSingleConnection)
        {
            _isSingleConnection = isSingleConnection;
        }
        //================================================================================
        #endregion

        #region Private Methods
        //================================================================================
        private void RaiseStateChanged(int sessions, bool isRunning)
        {
            ActiveSessions = sessions;
            var handles = ByPassStateChanged;
            handles(this, new ByPassStateEventArgs(){ ActiveSessions = sessions, IsRunning=isRunning});
        }
        //================================================================================
        private void StartProxySync()
        {
            _logger.Info("Starting Proxy");
            var token = _listenerToken.Token;

            try
            {
                while (_isRunning && !token.IsCancellationRequested)
                {
                    var client = WaitForClients();
                    var target = ConnectToTarget(_toHost, _toPort);

                    StartTransfers(client, target);

                    if (_isSingleConnection) break;
                }
                _tcpListener.Stop();
                _isRunning = false;
                RaiseStateChanged(ActiveSessions, false);
            }
            catch { }
        }
        //================================================================================
        private TcpClient WaitForClients()
        {
            try
            {
                var client = _tcpListener.AcceptTcpClient();

                string remoteIp = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
                _logger.Info("Connected client {0}", remoteIp);

                return client;
            }
            catch (Exception e)
            {
                _logger.Error("Error listening to clients. {0}", e);
                throw;
            }
        }
        //================================================================================
        private TcpClient ConnectToTarget(string host, int port)
        {
            try
            {
                var target = new TcpClient();
                target.NoDelay = true;
                target.Connect(host, port);

                _logger.Info("Connected to target host: {0}:{1}", host, port);
                return target;
            }
            catch (Exception e)
            {
                _logger.Error("Unable to connect to target host {0}:{1}", host, port);
                throw;
            }
        }
        //================================================================================
        private void StartTransfers(TcpClient client, TcpClient target)
        {
            var token = new CancellationTokenSource();

            //Start task to send data from client to target
            var clientTast = Task.Factory.StartNew(_ =>
                DoWhileNotCancelled(client, target, token.Token, _logger.ClientBytes, "Client"),
                token.Token, TaskCreationOptions.LongRunning);

            //Start task to send reply from target back to client
            var targetTast = Task.Factory.StartNew(_ =>
                DoWhileNotCancelled(target, client, token.Token, _logger.TargetBytes, "Target"),
                token.Token, TaskCreationOptions.LongRunning);

            lock (_locker)
            {
                _sessionCancelTokens.Add(new ConnectionCanceller(token, client, target));
                RaiseStateChanged(_sessionCancelTokens.Count, true);
            }

            _logger.Info("Started Transfers");
        }
        //================================================================================
        private void DoWhileNotCancelled(TcpClient from, TcpClient to, CancellationToken token, Action<int> bytesRead, string name)
        {
            NetworkStream fromStream = from.GetStream();
            NetworkStream toStream = to.GetStream();

            using (from)//Each 'from' is disposed so don't need to dispose of 'to'
            {
                while (!token.IsCancellationRequested && from.Connected)
                {
                    if (!CopyFromStream(fromStream, toStream, bytesRead, name == "Target" ? _targetLogger : _clientLogger))
                    {
                        _logger.Info("Can not read from {0}", name);
                        break;
                    }
                    Thread.Sleep(100);
                }
                CheckClientDisconnect(from, to);
            }
        }
        //================================================================================        
        private void CheckClientDisconnect(TcpClient from, TcpClient to)
        {
            try
            {
                if (!from.Connected && !to.Connected)
                {
                    lock (_locker)
                    {
                        var token = _sessionCancelTokens.FirstOrDefault(a => a.IsOwnerForAny(from, to));
                        if (token != null)
                        {
                            _sessionCancelTokens.Remove(token);
                            var running = _isRunning && !_isSingleConnection;
                            RaiseStateChanged(_sessionCancelTokens.Count, running);
                        }
                    }

                }

                if (!from.Connected || IsSocketDisconnected(from.Client))//from connection was disconnected. Log and disconnect other.
                    _logger.Info("{0} client disconnected.", ((IPEndPoint)from.Client.RemoteEndPoint).Address);
            }
            catch { }
            Disconnect(from);
            Disconnect(to);
        }
        //================================================================================
        private void Disconnect(TcpClient client)
        {
            if (client.Connected)
            {
                string address = "";
                try 
                {
                    address = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString() +
                        ":" + ((IPEndPoint)client.Client.RemoteEndPoint).Port;
                    client.Close(); 
                }
                catch (Exception) { }
                _logger.Info("{0} client disconnected.", address);
            }
        }
        //================================================================================
        private bool IsSocketDisconnected(Socket socket)
        {
            try { return (socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0); }
            catch (SocketException) { return false; }
        }
        //================================================================================
        private bool CopyFromStream(NetworkStream from, NetworkStream to, Action<int> bytesRead, IByPasser logger)
        {
            try
            {
                if (from.CanRead && to.CanWrite)
                {
                    byte[] myReadBuffer = new byte[1000024];
                    int numberOfBytesRead = 0;

                    do
                    {
                        numberOfBytesRead = from.Read(myReadBuffer, 0, myReadBuffer.Length);
                        if (_latency != 0) Thread.Sleep(_latency);
                        if (numberOfBytesRead != 0)
                        {
                            numberOfBytesRead = logger.Write(to, myReadBuffer, 0, numberOfBytesRead);
                            bytesRead(numberOfBytesRead);
                        }
                        else
                        {
                            return false;
                        }
                    }
                    while (from.DataAvailable);

                    to.Flush();
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch { return false; }
        }
        //================================================================================
        #endregion
    }
}
