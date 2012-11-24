using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ByPassProxy
{
    public class SocketViewModel : ViewModelBase
    {
        private double _totalKb;
        public double TotalKb { get { return _totalKb; } set { _totalKb += value; RaisePropertyChanged("TotalKb"); } }

        public bool ShowASCII { get; set; }

        public string Data { get; private set; }

        public void LogData(byte[] buffer, int from, int length)
        {
            ASCIIEncoding encoder = new ASCIIEncoding();
            var request = encoder.GetString(buffer, from, length);

            Data = Data + request;
            RaisePropertyChanged("Data");
        }
    }
}
