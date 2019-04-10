using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServerCommentless
{
    public sealed class Client : IDisposable
    {
        static int nextId = 0;


        readonly int id;
        public int ID => id;

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


        readonly TcpClient client;
        readonly Encoding encoding;
        readonly CancellationToken cancellationToken;
        readonly CancellationTokenRegistration disposeRegistration;
        readonly Thread recieveThread;


        public Client(CancellationToken cancellationToken, TcpClient client, Encoding encoding) {
            id = Interlocked.Increment(ref nextId);


            this.client = client;
            this.cancellationToken = cancellationToken;
            this.encoding = encoding;

            disposeRegistration = this.cancellationToken.Register(Dispose);

            recieveThread = new Thread(Receiver);
        }
        public Client(CancellationToken cancellationToken, TcpClient client) : this(cancellationToken, client, Encoding.Default) {

        }

        volatile bool runReciever = true;
        private void Receiver(object token) {
            CancellationToken ct = (CancellationToken)token;
            NetworkStream connection = client.GetStream();

            byte[] buffer = new byte[256];

            while (!ct.IsCancellationRequested && runReciever) {
                if (connection.DataAvailable) {
                    int read = connection.Read(buffer, 0, 256);
                    string message = encoding.GetString(buffer, 0, read);

                    IncommingMessage(new NewMessageEventArgs(message));
                }
                if (runReciever) Thread.Yield();
            }
        }

        bool startCalled = false;
        public void Start() {
            if (startCalled) return;
            startCalled = true;

            recieveThread.Start(cancellationToken);
        }

        public void SendMessage(string message) {
            byte[] data = encoding.GetBytes(message);

            client.GetStream()
                .WriteAsync(data, 0, data.Length, cancellationToken)
                .Start();
        }


        internal void RelayMessage(int sender, NewMessageEventArgs e) {
            if (sender == id) return;

            SendMessage(e.Message.Value);
        }

        private void IncommingMessage(NewMessageEventArgs e) {
            lock (messageReceivedEventPadlock) {
                MessageReceivedEvent?.Invoke(id, e);
            }
        }

        bool disposed = false;
        public void Dispose() {
            if (!disposed) {
                disposed = true;
                runReciever = false;

                recieveThread.Join(5000);
                if (recieveThread.IsAlive) recieveThread.Abort();

                client.Dispose();
                disposeRegistration.Dispose();
            }
        }
    }
}
