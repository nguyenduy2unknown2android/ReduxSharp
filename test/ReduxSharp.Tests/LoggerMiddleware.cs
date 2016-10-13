﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReduxSharp.Tests
{
    public class LoggerMiddleware<TState>
    {
        private readonly IStore<TState> _store;
        private readonly DispatchDelegate _next;
        private readonly LoggerOptions _options;

        public LoggerMiddleware(IStore<TState> store, DispatchDelegate next, LoggerOptions options)
        {
            _store = store;
            _next = next;
            _options = options;
        }

        public IAction Invoke(IAction action)
        {
            _options.Buffer.Add(action.GetType().FullName);
            return _next(action);
        }
    }

}
