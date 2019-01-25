﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Tapeti.Config;
using Tapeti.Connection;

// ReSharper disable UnusedMember.Global

namespace Tapeti
{
    public class TapetiConnection : IDisposable
    {
        private readonly IConfig config;
        public TapetiConnectionParams Params { get; set; }

        private readonly Lazy<TapetiWorker> worker;
        private TapetiSubscriber subscriber;

        public TapetiConnection(IConfig config)
        {
            this.config = config;
            (config.DependencyResolver as IDependencyContainer)?.RegisterDefault(GetPublisher);

            worker = new Lazy<TapetiWorker>(() => new TapetiWorker(config)
            {
                ConnectionParams = Params ?? new TapetiConnectionParams(),
                ConnectionEventListener = new ConnectionEventListener(this)
            });
        }

        public event EventHandler Connected;

        public event EventHandler Disconnected;

        public event EventHandler Reconnected;

        public async Task<ISubscriber> Subscribe(bool startConsuming = true)
        {
            if (subscriber == null)
            {
                subscriber = new TapetiSubscriber(() => worker.Value, config.Queues.ToList());
                await subscriber.BindQueues();
            }

            if (startConsuming)
                await subscriber.Resume();

            return subscriber;
        }


        public ISubscriber SubscribeSync(bool startConsuming = true)
        {
            return Subscribe(startConsuming).Result;
        }


        public IPublisher GetPublisher()
        {
            return new TapetiPublisher(() => worker.Value);
        }


        public async Task Close()
        {
            if (worker.IsValueCreated)
                await worker.Value.Close();
        }


        public void Dispose()
        {
            Close().Wait();
        }

        private class ConnectionEventListener: IConnectionEventListener
        {
            private readonly TapetiConnection owner;

            internal ConnectionEventListener(TapetiConnection owner)
            {
                this.owner = owner;
            }

            public void Connected()
            {
                owner.OnConnected(new EventArgs());
            }

            public void Disconnected()
            {
                owner.OnDisconnected(new EventArgs());
            }

            public void Reconnected()
            {
                owner.OnReconnected(new EventArgs());
            }
        }

        protected virtual void OnConnected(EventArgs e)
        {
            Task.Run(() => Connected?.Invoke(this, e));
        }

        protected virtual void OnReconnected(EventArgs e)
        {
            Task.Run(() =>
            {
                subscriber?.RebindQueues().ContinueWith((t) =>
                {
                    Reconnected?.Invoke(this, e);
                });
            });
        }

        protected virtual void OnDisconnected(EventArgs e)
        {
            Task.Run(() => Disconnected?.Invoke(this, e));
        }
    }
}
