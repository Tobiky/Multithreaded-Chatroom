using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace Server
{
    // If this at some point becomes hard to follow, here is something similar without actual implementation:
    // https://stackoverflow.com/a/20698153/7477867
    // the sealed modifier tells the compiler that this class is not to be inherited by any other class.
    public sealed class Server : IDisposable
    {
        // Common independant object to lock for thread safety.
        // We initialize it here because it will not be used in any other part of the code
        // and is compiled into the same thing as if we did it in the constructor.

        // Side note: This is not entirely true as compiling into debug will inject some code or keep code
        // unoptimized to make sure debugging is consistent and precise (as possible).
        readonly object messageReceivedEventPadlock = new object();
        event MessageReceivedHandler MessageReceivedEvent;
        // Note: Check the MessageReceivedHandler.cs file for more comments and info


        // Through the 'event' keyword comes special property syntax 'add' and 'remove'
        // They are used when ChatroomServer.MessageReceived += SomeHandler ('add') or 
        // ChatroomServer.MessageReceived -= SomeHandler ('remove') is called.

        // Because this event will be accessed or used across multiple threads we need to make it thread safe.
        // We do this by locking a common independant object and then manipulate it according to our will.
        //      common meaning it is locked for all actions on the object we are trying to make thread safe
        //      independant meaning locking it will not affect anything else other than it and the object we're working on
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

        // The reason we are using readonly will be explained in the Client.cs file as there is already a lot
        // in here. (It is not of high importance)

        // It is good practice to use the private access modifier, however in personal code it is fine not to
        // as class members are inherently private/protected (They can be accessed by class itself and derived classes)
        readonly TcpListener server;

        // CancellationTokens are used to cancel most multi threaded processed.
        // This will allow us to cancel the client threads/tasks for any reason,
        // among them closing the server, through the Cancel() method.
        readonly CancellationTokenSource tokenSource;

        // A list containing all the threads we have created
        // This is to be used when the server is closing.
        readonly object clientsPadlock = new object();
        Dictionary<int, Client> clients;


        public Server(IPAddress address, int port) {
            server = new TcpListener(address, port);
            tokenSource = new CancellationTokenSource();
            clients = new Dictionary<int, Client>();
        }

        // 'this' keyword can many times be used the same way as the 'base' keyword
        //      Here ':' means to take after; to build on
        //      The follwing 'this()' means the constructor from this class
        // Explicitely this would be (not correct syntax, just for clearification):
        //      public ChatroomServer(int port) : ChatroomServer(IPAddress.Any, port)
        // As suggested, we are continuing from where another constructor ended.
        public Server(int port) : this(IPAddress.Any, port) {

        }

        // Port 0 will cause a system call - that is, a call to the OS - for an available port, as described here:
        //      - https://www.lifewire.com/port-0-in-tcp-and-udp-818145
        //      - https://en.wikipedia.org/wiki/List_of_TCP_and_UDP_port_numbers
        // This means that we will get a port other than 0 automatically.
        // The reason we are using the internal access modifier (see what it does below) is because
        // we don't know what the port is. 
        
        // It is possible to retrieve it and it is not very difficult either but it is still not a good idea since:
        //      - The clients have to know it as well
        //      - It will most likely be different every time
        
        // internal:
        //      MS Docs: https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/internal
        //      The 'internal' access modifier tells the compiler that this class, or method if it is applied to such,
        //      is only accessible to other members (classes, methods, etc) of same assembly.
        //      In basic terms, the assembly is what the project is compiled into (the .dll or .exe)
        
        // The reasos we have this, despite the shortcommings listed above, is for testing purposes.
        // Some code does not activate IntelliSense or the live IDE debugging but is noticed by the compiler.
        // It also provides a quick way to skip problems such as the port already being used or
        // fiddling with constructor parameters.
        internal Server() : this(0) {

        }

        // This method starts the listening process for the server
        // Every attempted connection to the server will queued till Stop() is called
        // or it hits the max queue capacity.

        // As we do not know when the program is ready to start listening
        // we do only expose the start method.

        // More info on TcpListener.Start() can be found here:
        // https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.tcplistener.start?view=netframework-4.7.2
        public void Start() {
            server.Start();
        }

        // The params keyword allow us to give any amount of arguments of the same type
        // to the method.
        // The declaration of it is as following: params Type[] parameterName
        // Inside the method it is treated as a normal array of that type.
        private async Task Listen(params CancellationToken[] cancellationTokens) {
            // The => is called lambda.
            // It is a simple way of creating a function anywhere and results in method that would execute the same way.
            // Their types are, however, context dependant. This means that lambdas need to infer from the context
            // to know what types they're dealing with.

            // For example, the EventHandler delegate (object, EventArgs) would have a lambda like this:
            // (sender, e) => (sender as SomeClass).SomeMethod(e);
            // The result on the right hand side of the lambda operator is what the lambda will result in. Here; void.

            // The lambda p => p + 3 would be 
            // int Method(int p) {
            //      return p + 3;
            // }

            // .All is an extension method for the type IEnumerable<T>, meaning that all types that derive from
            // IEnumerable<T> can have this method called on them.
            // Extension methods are declared in a static class (does not need to be directly related to any specific class)
            // with the first parameter trailing after a 'this' keyword, like this:
            //      public static ResultType SomeExtension(this TypeToExtend, SomeType parameter, ...)

            // .All takes in a predicate, something to evaluate if something else is true or false, in form of
            // a Func delegate with a CancellationToken as parameter and bool as result.
            while (cancellationTokens.All(token => !token.IsCancellationRequested)) {
                // async/await and Tasks are part of C#'s simplified/encapsulating model of threads.
                // To use await, the owning method has to have the async keyword as part of its signature
                // and Task (for void returning methods) or Task<T> (for non-void returning methods) as 
                // return type.

                // awaiting a Task or Task<T> will cause the thread the task is working on to
                // work on something else while it waits for another Task to finish.

                // This works well for our purposes as AcceptTcpClient will wait till another client tries to connect
                // during that time, this thread can work on something else.
                TcpClient client = await server.AcceptTcpClientAsync();
                Client encapsulatingClient = new Client(tokenSource.Token, client);

                // When the client recieves a message, which it then notifies the server about
                encapsulatingClient.MessageReceived += (id, e) => MessageReceivedEvent(id, e);

                lock (clientsPadlock)
                    clients.Add(encapsulatingClient.ID, encapsulatingClient);

                // Yield the current CPU time slice, explained more in Client.cs.
                Thread.Yield();
            }
        }
        // If we don't have any other tokens we want to use we just use the internal one.
        public Task Listen() {
            return Listen(tokenSource.Token);
        }


        // The main reason for using Dispose is so we can delete this resource (remove it from memory essentially)
        // manually if there is an appropriate situation.
        // You can read more about Dispose and Dispose pattern here:
        // https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose

        // As we do not have a direct access to unmanaged resources and the class is sealed
        // we just call Dispose on all necessary components as well as some things to help the garbage collecter
        // to see unused resources.
        // Otherwise we would implement Dispose(bool) as well.
        // Note that this is also a bit of a hack. We know that Dispose will be called in Program.cs

        bool disposed = false;
        public void Dispose() {
            if (!disposed) {
                disposed = true;

                // Stop listening for requests.
                // This will also close the underlying socket.
                // You can read more about it here:
                // https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.tcplistener.stop?view=netframework-4.7.2#System_Net_Sockets_TcpListener_Stop
                server.Stop();

                // This will cancel the token that comes from the source and so all actions that are dependant on it.
                tokenSource.Cancel();

                // Cleaning up the clients
                foreach (var client in clients.Values)
                    client.Dispose();

                // Here we set clients to null so the garbage collector cleans up the 
                // leftovers. We do this because it is much more efficient than a 
                // .Clear()
                clients = null;
            }
        }
    }
}
