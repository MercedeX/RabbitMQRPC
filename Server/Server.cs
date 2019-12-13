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
            var config = new AMQPConfiguration
            {
                Host = "localhost",
                UserId = "nquser",
                Password = "nquser#123",
                VirtualHost = "/",
                Port = 5672
            };

            var listener = new CQRSServer(config, new SerializerImpl());

            listener.CommandReceived += (sender, args) =>
            {
                if (args.Command is MyCommand cmd)
                {
                    WriteLine($"{cmd.Port}  {cmd.Name}");
                    if (cmd.Port == 8081)
                        throw new Exception("Cannot use this port");
                }
            };

            listener.QueryReceived += (sender, args) =>
            {
                if (args.Query is MyCommand cmd)
                {
                    WriteLine(args.Key);
                    if (cmd.Port == 8081)
                        throw new PlatformNotSupportedException();
                    args.SetResult($"Hello World {args.Key}");
                }
            };

            WriteLine($"Server is running");

            while(ReadKey().Key != ConsoleKey.Escape);
            WriteLine("Good bye!");
        }
    }
}
