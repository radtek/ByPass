using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading;
using System.Windows.Threading;
using System.Windows;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace ByPassProxy
{
    public class MainViewModel : ViewModelBase, ILogger
    {
        #region Fields
        //================================================================================
        private readonly ConfigStore _configStore;
        private ProxyService _service; 
        //================================================================================
        #endregion

        //================================================================================
        public MainViewModel()
        {
            _configStore = new ConfigStore();
            Log = new ObservableCollection<string>();

            ListenPort = _configStore.ListenPort;
            TargetHostAndPort = _configStore.Target;
            _delayInt = _configStore.DelayMs;
            _isSingleConnection = _configStore.SingleConnection;

            Client = new SocketViewModel();
            Target = new SocketViewModel();
        }
        //================================================================================

        #region Properties
        //================================================================================
        public SocketViewModel Client { get; private set; }
        public SocketViewModel Target { get; private set; }
        //================================================================================

        //================================================================================
        public int ListenPort { get; set; }

        private string _targetHost; private int _targetPort;
        public string TargetHostAndPort
        {
            get { return _targetHost + ":" + _targetPort; }
            set { _targetPort = 0; var sp = value.Split(':'); if (sp.Length < 1) return; _targetHost = sp[0]; _targetPort = int.Parse(sp[1]); }
        }

        private int _delayInt = 0;
        public int DelayInt
        {
            get { return _delayInt; }
            set { _delayInt = value; if (_service != null) _service.SetLatency(value); }
        }

        private bool _isSingleConnection = false;
        public bool IsSingleConnection
        {
            get { return _isSingleConnection; }
            set { _isSingleConnection = value; if (_service != null) _service.SetIsSingleConnection(value); }
        }

        public string Status
        {
            get
            {
                return string.Format("{0}, Active Sessions: {1}", 
                    _service != null ? _service.IsRunning ? "Running" : "Stopped" : "Stopped",
                    _service != null ? _service.ActiveSessions : 0);
            }
            set { RaisePropertyChanged("Status"); }
        }

        public bool ClientShowASCII { get; set; }
        public bool TargetShowASCII { get; set; }
        //================================================================================

        //================================================================================
        private bool _isRunning;
        public bool IsRunning 
        { 
            get { return _isRunning; } 
            set  { _isRunning = value; RaisePropertyChanged("IsRunning"); RaisePropertyChanged("IsNotRunning"); }
        }
        public bool IsNotRunning { get { return !_isRunning; } }
        //================================================================================

        public string ActionButtonText { get { return IsRunning ? "Stop" : "Start"; } }
        public ICommand ActionButtonCommand { get { 
            return new ActionCommand(OnActionButtonClicked, 
                ()=>ListenPort!=0 && _targetPort!=0 && !string.IsNullOrWhiteSpace(_targetHost)); } }
        //================================================================================

        //================================================================================
        public ObservableCollection<string> Log { get; private set; }
        //================================================================================
        #endregion

        #region Public Methods
        //================================================================================
        public void OnViewClosing()
        {
            //Save the options
            _configStore.ListenPort = ListenPort;
            _configStore.Target = TargetHostAndPort;
            _configStore.DelayMs = _delayInt;
            _configStore.SingleConnection = _isSingleConnection;
            _configStore.Save();
        }

        public void Info(string message, params object[] param)
        {
            Queue(() => AddToLog(DateTime.Now.ToShortTimeString() + ": " + string.Format(message, param)));
        }

        public void Error(string message, params object[] param)
        {
            Info(message, param);
        }

        public void ClientBytes(int bytes)
        {
            Client.TotalKb = bytes / 1024d;
        }

        public void ClientData(string message)
        {
            throw new NotImplementedException();
        }

        public void TargetBytes(int bytes)
        {
            Target.TotalKb = bytes / 1024d;
        }

        public void TargetData(string message)
        {
            throw new NotImplementedException();
        }
        //================================================================================
        #endregion

        #region Private Methods
        //================================================================================
        private void AddToLog(string message)
        {
            Log.Insert(0, message);
        }
        //================================================================================
        private void OnActionButtonClicked()
        {
            if (!IsRunning)
            {
                _service = SwapServiceEvents();
                _service.StartProxy();
                IsRunning = true; RaisePropertyChanged("ActionButtonText");
            }
            else
            {
                if (_service != null) _service.StopProxy();
                IsRunning = false; RaisePropertyChanged("ActionButtonText");
            }
        }
        //================================================================================
        private ProxyService SwapServiceEvents()
        {
            if (_service != null)
                _service.ByPassStateChanged -= OnServiceStateChanged;

            Action<byte[], int, int> clog = null;
            Action<byte[], int, int> tlog = null;

            if (ClientShowASCII) clog = Client.LogData;
            if (TargetShowASCII) tlog = Target.LogData;

            var service = new ProxyService(ListenPort, _targetHost, _targetPort, this, clog, tlog);
            service.SetIsSingleConnection(IsSingleConnection);
            service.SetLatency(this.DelayInt);
            service.ByPassStateChanged += OnServiceStateChanged;
            return service;
        }
        //================================================================================
        private void OnServiceStateChanged(object o, ByPassProxy.ProxyService.ByPassStateEventArgs args)
        {
            Status = string.Empty;
            if (!args.IsRunning && args.ActiveSessions == 0)
            {
                IsRunning = false;
                RaisePropertyChanged("ActionButtonText");
            }
        }
        //================================================================================
        #endregion
    }
}
