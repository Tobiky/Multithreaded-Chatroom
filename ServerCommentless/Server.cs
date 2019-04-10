using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace ServerCommentless
{
    public sealed class Server : IDisposable
    {
        readonly object messageReceivedEventPadlock = new object();
        event MessageReceivedHandler MessageReceivedEvent;
 
        public event MessageReceivedHandler MessageReceived {
            add {
                lock (messageReceivedEventPadlock)
                    MessageReceivedEvent += value;
            }
            remove {
                lock (messageReceivedEventPadlock)
                    MessageReceivedEvent -= value;
            }
        }

        readonly TcpListener server;
        readonly CancellationTokenSource tokenSource;

        readonly object clientsPadlock = new object();
        Dictionary<int, Client> clients;


        public Server(IPAddress address, int port) {
            server = new TcpListener(address, port);
            tokenSource = new CancellationTokenSource();
            clients = new Dictionary<int, Client>();
        }
        public Server(int port) : this(IPAddress.Any, port) {

        }
        internal Server() : this(0) {

        }


        public void Start() {
            server.Start();
        }

        private async Task Listen(params CancellationToken[] cancellationTokens) {
            while (cancellationTokens.All(token => !token.IsCancellationRequested)) {
                TcpClient client = await server.AcceptTcpClientAsync();
                Client encapsulatingClient = new Client(tokenSource.Token, client);

                encapsulatingClient.MessageReceived += (id, e) => MessageReceivedEvent(id, e);

                lock (clientsPadlock)
                    clients.Add(encapsulatingClient.ID, encapsulatingClient);

                Thread.Yield();
            }
        }
        public Task Listen() {
            return Listen(tokenSource.Token);
        }

        bool disposed = false;
        public void Dispose() {
            if (!disposed) {
                disposed = true;

                server.Stop();

                tokenSource.Cancel();

                foreach (var client in clients.Values)
                    client.Dispose();

                clients = null;
            }
        }
    }
}
