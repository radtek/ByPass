using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace ByPassProxy
{
    public class Logger
    {
        private TraceSource _tracer;
        private static Logger _logger;
        private static Logger Singleton { get { if (_logger == null) _logger = new Logger(); return _logger; } }

        private Logger()
        {
            _tracer = new TraceSource(this.GetType().Namespace);
            _tracer.Switch.Level = SourceLevels.All;
        }

        public static void Info(string message, params object[] args)
        {
            Singleton._tracer.TraceEvent(TraceEventType.Information, 0, string.Format(message, args));
        }

        public static void Error(string message, params object[] args)
        {
            Singleton._tracer.TraceEvent(TraceEventType.Error, 0, string.Format(message, args));
        }

        public static void AddListener(Action<string> logAction)
        {
            Singleton._tracer.Listeners.Add(new Foo(logAction));
        }

        class Foo : TraceListener
        {
            private Action<string> _logAction;
            public Foo(Action<string> logAction)
            {
                _logAction = logAction;
                this.TraceOutputOptions = TraceOptions.None;
            }

            public override void Write(string message)
            {
                _logAction(message);
            }

            public override void WriteLine(string message)
            {
                Write(message + Environment.NewLine);
            }

            public override void  TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
            {
                WriteLine(eventType.ToString() + ": " + message);
            }
        }

    }
}
