using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Diagnostics.CodeAnalysis;

namespace ByPassProxy
{
    public class ActionCommand : ActionCommandGen<object>
    {
        public ActionCommand(Action action) : base(_ => action()) { }
        public ActionCommand(Action action, Func<bool> canExec) : base(_ => action(), _=>canExec()) { }
    }

    public class ActionCommandGen<T> : ICommand
    {
        private Action<T> _executeMethod;
        protected Func<T, bool> _canExecuteMethod;
        private List<WeakReference> _canExecuteChangedHandlers;

        public ActionCommandGen(Action<T> executeMethod) : this(executeMethod, (T meth) => { return true; }) { }

        public ActionCommandGen(Action<T> executeMethod, Func<T, bool> canExecuteMethod)
        {
            _executeMethod = executeMethod;
            _canExecuteMethod = canExecuteMethod;
        }

        public bool CanExecute()
        {
            return CanExecute(null);
        }

        public bool CanExecute(T parameter)
        {
            if (_canExecuteMethod == null) return true;
            return _canExecuteMethod(parameter);
        }

        public bool CanExecute(object parameter)
        {
            var p = parameter == null ? default(T) : (T)parameter;
            return CanExecute(p);
        }

        public void Execute()
        {
            Execute(null);
        }

        public void Execute(T parameter)
        {
            if (_executeMethod != null)
            {
                _executeMethod(parameter);
            }
        }

        public void Execute(object parameter)
        {
            var p = parameter == null ? default(T) : (T)parameter;
            Execute(p);
        }

        public event EventHandler CanExecuteChanged
        {
            add
            {
                if (_canExecuteMethod != null)
                {
                    CommandManager.RequerySuggested += value;
                }
            }

            remove
            {
                if (_canExecuteMethod != null)
                {
                    CommandManager.RequerySuggested -= value;
                }
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}