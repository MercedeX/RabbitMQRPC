using System;
using System.Threading.Tasks;
using Messaging;
using Transport;
using static System.Console;

namespace Server
{
    class Server
    {
        static async Task Main(string[] args)
        {
            var listener = new CQRSServer(new SerializerImpl());

            listener.CommandReceived += async (sender, args) =>
            {
                if (args.Command is MyCommand cmd)
                {
                    WriteLine($"{cmd.Port}  {cmd.Name}");
                }

                await Task.Yield();
            };

            listener.QueryReceived += async (sender, args) =>
            {
                if (args.Query is MyCommand cmd)
                {
                    WriteLine(args.Key);
                    args.SetResult($"Hello World {args.Key}");
                }

                await Task.Yield();
            };

            WriteLine($"Server is running");

            while(ReadKey().Key != ConsoleKey.Escape);
            WriteLine("Good bye!");
        }
    }
}
