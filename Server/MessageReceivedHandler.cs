using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    // This will be our event delegate type, much like the standard EventHandler one but we use
    // an int id instead of object.
    // We are doing this because we want the client objects to compare their id to the senderId
    // and ignore if it is theirs.
    // As value types (types that are pass by value; copied) are thread safe due to their nature
    // (copy means no thread conflict, basically) we don't need a ThreadLocal or any of that sort.
    public delegate void MessageReceivedHandler(int senderId, NewMessageEventArgs newMessage);

    // All types are derived from object, which means that value types and then also int are too.
    // Why not just cast int to an object?
    // This is called a boxing operation (unboxing for the opposite; getting a value type from an object).
    // These operations are quite costly as they require the runtime/computer to create an object
    // on the stack, create a reference to it and put that on the heap, (these two are done normally on the creation
    // of reference types), and then make the object encapsulate the value type (the costly part).
    // Or the reverse for unboxing.
    // That is why un/boxing should be avoided as much as possible.
}
