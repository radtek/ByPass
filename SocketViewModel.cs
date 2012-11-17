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
    }
}
