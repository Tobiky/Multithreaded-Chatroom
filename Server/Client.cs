using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    // The reason we do not use lock in here is because the only times it would be needed would be for the stream
    // but as that already implements async Task members we can use those.
    // If this at some point becomes hard to follow, here is something similar without actual implementation:
    // https://stackoverflow.com/a/20698153/7477867
    public sealed class Client : IDisposable
    {
        static int nextId = 0;
        readonly int id;

        // This is a short way of writing get-only properties
        // this would with normal syntax be written as:
        // public int ID {
        //      get {
        //          return id;
        //      }
        // }

        // It can also be written with a thing called lambda:
        // public int ID {
        //      get => id;
        // }

        // Lambdas are expressions that result in a function
        //      <variables> => <expression>
        // if there are more than one, or none, you encase the variables with parentheses like so:
        //      (<variables>) => <expression>
        // No variables would like like this:
        //      () => <expression>

        // The types in the lambda are infered from the context, so if the types are ambigious to the compiler
        // the lambda expression will produce an error.
        // An example is that you can't write this:
        //      var someLambda = variable => variable;
        // As both the input variable type and return type are unknown.

        // More info on lambdas can be found here:
        // https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/statements-expressions-operators/lambda-expressions
        public int ID => id;

        // The same event is used on the client to communicate to the server that a message has been recieved.
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

        // readonly provides both security and *some* increased performance (in *some* cases).
        // Through readonly you can't accidently change the object, and it's not something we want here.
        // We only want to interact with these objects.
        // It also makes sure they can't be changed from the outside through reflection 
        // (though this can be walked around through more reflection).
        readonly TcpClient client;
        readonly Encoding encoding;
        readonly CancellationToken cancellationToken;
        readonly CancellationTokenRegistration disposeRegistration;
        readonly Thread recieveThread;

        // The cancellationToken is intended to be the one from the server but this allows us to reuse
        // the class for other purposes as well.
        // The encoding parameter is so we need to hard-code less and also helps if there are any problems with
        // encoding or decoding when sending or receiving the messages.
        public Client(CancellationToken cancellationToken, TcpClient client, Encoding encoding) {
            // Interlocked is a class that lets us perform atomic operations on int's
            // in a thread safe manor.
            // The Increment() method takes in a reference to an int, so its position in memory
            // Increments it and return it to us.
            // The effect of this, as it matters to us, is the same as locking on a common independant object
            // and increment the value inside the lock statment
            // You can read more about Increment here:
            // https://docs.microsoft.com/en-us/dotnet/api/system.threading.interlocked.increment?view=netframework-4.7.2
            // and about Interlocked here:
            // https://docs.microsoft.com/en-us/dotnet/api/system.threading.interlocked?view=netframework-4.7.2
            id = Interlocked.Increment(ref nextId);


            this.client = client;
            this.cancellationToken = cancellationToken;
            this.encoding = encoding;

            // When the token is cancelled it means the client should stop which itself means that the client should be disposed.
            // The cancellationToken might also be a common token, which means that if the token is still alive (not cancelled)
            // but this client isn't, we want to deregister the dispose call so it doesn't call Dispose() on an already 
            // disposed object.
            // Calling Dispose() on a CancellationTokenRegistration will cause it unregister whatever callback (method)
            // was registered when it was created.
            disposeRegistration = this.cancellationToken.Register(Dispose);

            // We create a new instance of the thread class. This does not actually create a thread but prepare
            // the parameters for it (more info about that in the Reveive() method). 
            // The thread is created once Start() is called on this instance below.
            recieveThread = new Thread(Receiver);
        }
        public Client(CancellationToken cancellationToken, TcpClient client) : this(cancellationToken, client, Encoding.Default) {

        }

        // As the bool is only going to be used in the method beneath, we put it above the method.
        volatile bool runReciever = true;
        // This is the method that will run on a seperate thread to recieve data from outcoming sources.
        // NetworkStream can send and recieve data at the same time as long as this is done on seperate threads.
        // This is because Reading and Writing operations are so called blocking operations.
        // The name stems from that these operations need to wait for something and while they do they block
        // the thread from performing other tasks. (This is why async/await is good for not hogging up threads needlessly)
        private void Receiver(object token) {
            CancellationToken ct = (CancellationToken)token;
            NetworkStream connection = client.GetStream();

            // The buffer is used for the Read() method to write to.
            // The size of the buffer can be done manuall this way, but it can also be done using 
            // fields, external methods, or a parameter for the owning method.
            // But why do we need a buffer in the first place, can't the method just return an array?
            // A stream is as the name suggests; a stream, we don't know how much data is available
            // so we tell the stream to read up to a maximum amount and it returns how much it actually
            // *could* read.
            // Some streams do have a 'Available' property or the like, but this is just a state 
            // on how much has been stored in their internal buffers.
            byte[] buffer = new byte[256];

            while (!ct.IsCancellationRequested && runReciever) {
                if (connection.DataAvailable) {
                    // The Read() method returns an int describing how many bytes it actually read.
                    // This can be 0 up to the 'size' parameter (here that is 256).
                    int read = connection.Read(buffer, 0, 256);
                    string message = encoding.GetString(buffer, 0, read);

                    // Here we notify the subscribers (the server) that there is a new message.
                    IncommingMessage(new NewMessageEventArgs(message));
                }
                // When a thread is created, it is done so with different parameters (priority is one among them)
                // Using these paramaters a 'time slice' is calculated.
                // That thread runs for that time slice, then the CPU moves on to the next one.
                // This repeats forever. Well... Till the computer shuts down anyway.
                // Thread.Yield() tells the CPU that this thread doesn't need more of its current time slice
                // and the CPU schedules a new time slice in the future (also dependent on the parameters).
                if (runReciever) Thread.Yield();
                // This check is to make sure that the thread exits as fast as possible after runReciever
                // has become false
            }
        }

        bool startCalled = false;
        public void Start() {
            // We do not want to start more times, as that would create more than one listening/receiver thread.
            // This *can* work but that would mess up the information transaction rather than making it more
            // efficient.
            if (startCalled) return;
            startCalled = true;

            recieveThread.Start(cancellationToken);
        }

        public void SendMessage(string message) {
            // Here we translate the string into another format (that was chosen in the constructor)
            // strings are actually just arrays of char's, which are themselves just bytes.
            // The default encoding of C# is UTF-16, which stands for Unicode Tranformation Format 16
            // meaning the Unicode representation of symbols and characters
            // using 16 bits (2 bytes). This can seen if you look at the result of sizeof(char)
            // which will be 2.
            byte[] data = encoding.GetBytes(message);

            // The stream sends the data as soon as the internal buffer is filled
            // or the method finished writing our 'data' to it.

            // This is a short workload and we don't care when it's finished
            // so instead of waiting for the Task to complete we just create and start it
            client.GetStream()
                .WriteAsync(data, 0, data.Length, cancellationToken)
                .Start();
        }



        // This becomes has the same signature as the MessageReceivedHandler delegate and so can
        // be attached; hooked; follow; subscribe to the server event.
        // Since nothing other than the types in the assembly should use this handler, we make it internal.
        internal void RelayMessage(int sender, NewMessageEventArgs e) {
            // The sender is this instance, we don't need to care about the message or event
            if (sender == id) return;

            // Since we are just sending a message like normal, we can just use the method that
            // is used to send the messages normally.
            SendMessage(e.Message.Value);
        }

        // Through this method, we can fire the event in a thread safe manor from everywhere inside the class.
        private void IncommingMessage(NewMessageEventArgs e) {
            lock (messageReceivedEventPadlock) {
                // When the server is notified it will raise its own event with the same message and id.
                MessageReceivedEvent?.Invoke(id, e);

                // ?. is called a null conditional operator.
                // If the target (here, messageReceivedEvent) is null, it returns null
                // Otherwise it calls the specified member.
                // In normal syntax this is (explicitely):

                // MessageReceivedHandler handler = messageReceivedEvent;
                // if (handler == null) return null;
                // else return messageReceivedEvent.Invoke(id, new NewMessageEventArgs(message));

                // For this case it would be:
                // MessageReceivedHandler handler = messageReceivedEvent;
                // if (handler != null) messageReceivedEvent.Invoke(id, new NewMessageEventArgs(message));

                // as returns from operators are ignored the value is not assigned to anything. 
            }
        }

        // We will only use 'disposed' here so we initialize it where it is easily accessible and in view 
        // for the method.
        bool disposed = false;
        public void Dispose() {
            // This will avoid uneccessary calls to the underlying dispose methods.
            if (!disposed) {
                disposed = true;
                // Stop the receiver loop
                runReciever = false;

                // Here we tell this thread (the one the instance is running on) to wait for 
                // the specified thread ('receiveThread') to complete it operations and terminate.
                // We give it a maximum of 5 seconds to complete as any more would mean that the specified thread
                // is probably stuck in a loop that can't be exited without aborting the thread.
                recieveThread.Join(5000);

                // Since the thread is still alive after 5s in a dispose method we abort it.
                if (recieveThread.IsAlive) recieveThread.Abort();

                client.Dispose();
                disposeRegistration.Dispose();
            }
        }
    }
}
