using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace ServerCommentless
{
    public class NewMessageEventArgs : EventArgs, IDisposable
    {
        public ThreadLocal<string> Message { get; }


        public NewMessageEventArgs(string message) {
            Message = new ThreadLocal<string>(() => string.Copy(message));
        }

        bool disposed = false;
        public void Dispose() {
            if (!disposed) {
                disposed = true;
                Message.Dispose();
            }
        }
    }
}
