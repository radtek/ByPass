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
        private ProxyService _service; 
        //================================================================================
        #endregion

        //================================================================================
        public MainViewModel()
        {
            Log = new ObservableCollection<string>();

            ListenPort = 5032;
            TargetHostAndPort = "172.26.145.217:5032";
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
            set { _targetPort = 0;  var sp = value.Split(':'); _targetHost = sp[0]; _targetPort = int.Parse(sp[1]); }
        }

        private int _delayInt = 0;
        public int DelayInt
        {
            get { return _delayInt; }
            set { _delayInt = value; if (_service != null) _service.SetLatency(value); }
        }
        //================================================================================

        //================================================================================
        private bool _isRunning;
        public string ActionButtonText { get { return _isRunning ? "Stop" : "Start"; } }
        public ICommand ActionButtonCommand { get { 
            return new ActionCommand(OnActionButtonClicked, 
                ()=>ListenPort!=0 && _targetPort!=0 && !string.IsNullOrWhiteSpace(_targetHost)); } }
        //================================================================================

        //================================================================================
        public ObservableCollection<string> Log { get; private set; }
        //================================================================================
        #endregion

        //================================================================================
        private void AddToLog(string message)
        {
            Log.Insert(0, message);
        }

        private void OnActionButtonClicked()
        {
            if (!_isRunning)
            {
                _service = new ProxyService(ListenPort, _targetHost, _targetPort, this);
                _service.StartProxy();
                _isRunning = true; RaisePropertyChanged("ActionButtonText");
            }
            else
            {
                if (_service != null) _service.StopProxy();
                _isRunning = false; RaisePropertyChanged("ActionButtonText");
            }
        }
        //================================================================================
    
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
    }
}
