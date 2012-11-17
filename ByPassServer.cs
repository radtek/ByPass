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
        TcpClient _client;
        TcpClient _target;

        CancellationTokenSource _clientToTarget;
        CancellationTokenSource _targetToClient;
        CancellationTokenSource _listenerToken = new CancellationTokenSource();

        private bool _isRunning;
        private bool _isConnected;
        private bool _isCancelled;

        private int _latency = 0;
        //================================================================================
        #endregion

        #region Constructor
        //================================================================================
        public ProxyService(int listenPort, string toHost, int toPort, ILogger logger)
        {
            _logger = logger;
            _listenPort = listenPort;
            _toHost = toHost;
            _toPort = toPort;
        }
        //================================================================================
        #endregion

        #region Public Methods
        //================================================================================
        public void StopProxy()
        {
            if (!_isRunning) return;

            _isCancelled = true;

            _listenerToken.Cancel();
            _tcpListener.Stop();

            if (_clientToTarget != null) _clientToTarget.Cancel();
            if (_clientToTarget != null) _targetToClient.Cancel();

            if (_client != null) _client.Close();
            if (_target != null) _target.Close();
        }
        //================================================================================
        public void StartProxy()
        {
            if (!_isRunning)
            {
                _isCancelled = false;
                _isRunning = true;
                _tcpListener = new TcpListener(IPAddress.Any, _listenPort);
                _tcpListener.Start();
                Task.Factory.StartNew(StartProxySync, _listenerToken.Token);
            }
        }
        //================================================================================
        public void SetLatency(int ms)
        {
            _latency = ms;
        }
        //================================================================================
        #endregion

        #region Private Methods
        //================================================================================
        private void StartProxySync()
        {
            _logger.Info("Starting Proxy");

            _client = WaitForClients();
            _target = ConnectToTarget(_toHost, _toPort);

            StartTransfers(_client, _target);
            _isConnected = true;
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
            //Start task to send data from client to target
            _clientToTarget = new CancellationTokenSource();
            var clientTast = Task.Factory.StartNew(_ =>
                DoWhileNotCancelled(client, target, _clientToTarget.Token, _logger.ClientBytes, "Client"), 
                _clientToTarget.Token, TaskCreationOptions.LongRunning);

            //Start task to send reply from target back to client
            _targetToClient = new CancellationTokenSource();
            var targetTast = Task.Factory.StartNew(_ =>
                DoWhileNotCancelled(target, client, _targetToClient.Token, _logger.TargetBytes, "Target"), 
                _targetToClient.Token, TaskCreationOptions.LongRunning);

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
                    if (!CopyFromStream(fromStream, toStream, bytesRead))
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
        private void CheckClientDisconnect(TcpClient from, TcpClient to)
        {
            lock(_locker)
            {
                if (_isCancelled)//User clicked the STOP button
                {
                    if(from.Connected && to.Connected)
                        _logger.Info("Disconnecting...");

                    Disconnect(from);
                    Disconnect(to);
                    return;
                }

                if (!from.Connected && !to.Connected)//Disconnection second time round
                {
                    //Restart the proxy
                    StopProxy();
                    StartProxy();
                }
                else if (!from.Connected || IsSocketDisconnected(from.Client))//from connection was disconnected. Log and disconnect other.
                {
                    _logger.Info("{0} client disconnected.", ((IPEndPoint)from.Client.RemoteEndPoint).Address);
                    Disconnect(from);
                    Disconnect(to);
                }
            }
        }
        //================================================================================
        private bool IsSocketDisconnected(Socket socket)
        {
            try { return (socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0); }
            catch (SocketException) { return false; }
        }
        //================================================================================
        private bool CopyFromStream(NetworkStream from, NetworkStream to, Action<int> bytesRead)
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
                        to.Write(myReadBuffer, 0, numberOfBytesRead);
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
        //================================================================================
        #endregion
    }
}
