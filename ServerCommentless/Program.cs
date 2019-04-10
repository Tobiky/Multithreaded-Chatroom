using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerCommentless
{
    class Program
    {
        static void Main(string[] args) {
            Server server = new Server();
            server.Start();
            server.Listen().Start();

            bool run = true;
            void Cancel() {
                run = false;
                server.Dispose();
            }

            Console.CancelKeyPress += (sender, e) => Cancel();

            while (run) {
                if (Console.KeyAvailable) {
                    if (Console.ReadKey().Key == ConsoleKey.Escape)
                        Cancel();
                }
            }
        }
    }
}
