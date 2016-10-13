﻿using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ReduxSharp
{
    /// <summary>
    /// A builder for <see cref="Store{TState}"/>
    /// </summary>
    /// <typeparam name="TState">A type of root state tree</typeparam>
    public class StoreBuilder<TState> : IStoreBuilder<TState>
    {
        private readonly IReducer<TState> _reducer;

        private readonly List<MiddlewareDelegate<TState>> _middlewares
            = new List<MiddlewareDelegate<TState>>();

        private TState _initialState = default(TState);

        /// <summary>
        /// Initializes a new instance of <see cref="StoreBuilder{TState}"/> class.
        /// </summary>
        /// <param name="reducer">
        /// A reducing object that returns the next state tree.
        /// </param>
        public StoreBuilder(IReducer<TState> reducer)
        {
            if (reducer == null) throw new ArgumentNullException(nameof(reducer));

            _reducer = reducer;
        }

        /// <summary>
        /// Add or replace an initial state.
        /// </summary>
        /// <param name="initialState">
        /// The initial state.
        /// </param>
        /// <returns>The <see cref="IStoreBuilder{TState}"/>.</returns>
        public IStoreBuilder<TState> UseInitialState(TState initialState)
        {
            _initialState = initialState;
            return this;
        }

        /// <summary>
        /// Adds a middleware delegate to the store's dispatch pipeline.
        /// </summary>
        /// <param name="middleware">The middleware delegate.</param>
        /// <returns>The <see cref="IStoreBuilder{TState}"/> instance.</returns>
        public IStoreBuilder<TState> UseMiddleware(MiddlewareDelegate<TState> middleware)
        {
            if (middleware == null) throw new ArgumentNullException(nameof(middleware));

            _middlewares.Add(middleware);
            return this;
        }

        /// <summary>
        /// Builds a <see cref="IStore{TState}"/>.
        /// </summary>
        /// <returns>The <see cref="IStore{TState}"/>.</returns>
        public IStore<TState> Build()
        {
            return new Store<TState>(_reducer, _initialState, _middlewares.ToArray());
        }

        private const string InvokeMethodName = "Invoke";

        /// <summary>
        /// Adds a middleware type to the store's dispatch pipeline.
        /// </summary>
        /// <typeparam name="TMiddleware">The middleware type.</typeparam>
        /// <param name="args">The arguments to pass to the middleware type instance's constructor.</param>
        /// <returns>The <see cref="IStoreBuilder{TState}"/> instance.</returns>
        public IStoreBuilder<TState> UseMiddleware<TMiddleware>(params object[] args)
        {
            return UseMiddleware(typeof(TMiddleware), args);
        }

        /// <summary>
        /// Adds a middleware type to the store's dispatch pipeline.
        /// </summary>
        /// <param name="middleware">The middleware type.</param>
        /// <param name="args">The arguments to pass to the middleware type instance's constructor.</param>
        /// <returns>The <see cref="IStoreBuilder{TState}"/> instance.</returns>
        public IStoreBuilder<TState> UseMiddleware(Type middleware, params object[] args)
        {
            return UseMiddleware((store, next) =>
            {
                var typeInfo = middleware.GetTypeInfo();

                var invokeMethod = typeInfo.GetDeclaredMethod(InvokeMethodName);
                if (invokeMethod == null)
                {
                    throw new InvalidOperationException($"{middleware.Name} require {InvokeMethodName} method");
                }

                var invokeParameters = invokeMethod.GetParameters();
                if (invokeParameters.Length == 0)
                {
                    throw new InvalidOperationException($"{InvokeMethodName} required action argument");
                }

                var ctorArgs = new object[args.Length + 2];
                ctorArgs[0] = store;
                ctorArgs[1] = next;
                Array.Copy(args, 0, ctorArgs, 2, args.Length);
                var instance = Activator.CreateInstance(middleware, ctorArgs);

                var factory = Compile<object>(invokeMethod, invokeParameters);

                return action =>
                {
                    return factory(instance, action);
                };
            });
        }

        private static Func<T, IAction, IAction> Compile<T>(MethodInfo methodInfo, ParameterInfo[] parameters)
        {
            var middleware = typeof(T);
            var actionArg = Expression.Parameter(typeof(IAction), "action");
            var instanceArg = Expression.Parameter(middleware, "instance");

            // Invoke method's argument is action only.
            var methodArguments = new Expression[parameters.Length];
            methodArguments[0] = actionArg;

            Expression middleweareInstanceArg = instanceArg;
            if (methodInfo.DeclaringType != typeof(T))
            {
                middleweareInstanceArg = Expression.Convert(middleweareInstanceArg, methodInfo.DeclaringType);
            }

            var body = Expression.Call(middleweareInstanceArg, methodInfo, methodArguments);

            var lambda = Expression.Lambda<Func<T, IAction, IAction>>(body, instanceArg, actionArg);

            return lambda.Compile();
        }
    }
}
