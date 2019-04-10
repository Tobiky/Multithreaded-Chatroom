using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Server
{
    // This class provides information about a new message that the server has recieved (and is to be relayed).
    public class NewMessageEventArgs : EventArgs, IDisposable
    {
        // ThreadLocals are objects that generate a specified value for each thread that accesses it.
        // This means that the stack (as in heap and stack) of each thread gets its own copy of the value.
        // You can read more about 'ThreadLocal's here:
        // https://docs.microsoft.com/en-us/dotnet/api/system.threading.threadlocal-1?view=netframework-4.7.2

        // This is the message that the server has recieved which is to be relayed to all other clients.
        public ThreadLocal<string> Message { get; }

        // Whenever a message is recieved that is to be relayed to all other clients, a new instance
        // of this is created containing that message so it is easily accessable.
        public NewMessageEventArgs(string message) {
            // Since strings are pass by reference, meaning that we pass the reference to the string and not a copy,
            // we make a copy of the string and return that.
            // Several threads trying to access the same values can cause problems, most of which
            // come from how memory works.
            // This is not true for all cases but it's better to be safe than sorry.
            Message = new ThreadLocal<string>(() => string.Copy(message));
        }

        // We implement IDisposable because ThreadLocal is, or holds, an unmanaged resource (a resource 
        // that is or not entirely managed by the garbage collector).
        // This means we can throw an instance away if we find the chance to and the garbage collector
        // doesn't need to finalize the instance (removing it from memory through a specialized constructor, simply speaking)
        // We use a bool to make sure disposed isn't effectively used more than once.
        bool disposed = false;
        public void Dispose() {
            // As it is only the ThreadLocal that needs to be manually managed and it is the only
            // field we have, we can just call the ThreadLocal's Dispose method.
            if (!disposed) {
                disposed = true;
                Message.Dispose();
            }
        }
    }
}
