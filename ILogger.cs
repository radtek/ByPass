using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ByPassProxy
{
    public interface ILogger
    {
        void Info(string message, params object[] param);
        void Error(string message, params object[] param);

        void ClientBytes(int sent);
        void ClientData(string message);

        void TargetBytes(int sent);
        void TargetData(string message);
    }
}
