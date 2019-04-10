using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    class Program
    {
        static void Main(string[] args) {
            Server server = new Server();
            // Start the server
            server.Start();
            // Listen for incomming requests and handle them on another thread (through a task).
            server.Listen().Start();


            bool run = true;
            void Cancel() {
                run = false;
                server.Dispose();
            }

            // CancelKey is Ctrl+C or Ctrl+Break, when those are press we want to kill the server and exit the loop
            Console.CancelKeyPress += (sender, e) => Cancel();

            while (run) {
                if (Console.KeyAvailable) {
                    // When the escape key is pressed we want to kill the server and exit the loop
                    if (Console.ReadKey().Key == ConsoleKey.Escape)
                        Cancel();
                }
            }
        }
    }
}
