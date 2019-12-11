using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Messaging;
using Transport;
using static System.Console;

namespace Client
{
    class Client
    {
        static async Task Main(string[] args)
        {
            var bus = new CQRSImpl(new SerializerImpl());

            try
            {
                while (true)
                {
                    WriteLine("How many envelopes? (0: terminate)");
                    var str = ReadLine().ToUpper();
                    if (int.TryParse(str, out var count) && count>0)
                    {
                      
                        var commands = CreateCommands(count);
                        TimeSpan max = TimeSpan.Zero, min = TimeSpan.MaxValue;
                        var processingStart = DateTime.UtcNow;
                        foreach (var cmd in commands)
                        {
                            try
                            {
                                var st = DateTime.UtcNow;
                                var res = await bus.Ask<string>(cmd, CancellationToken.None);
                                var total = DateTime.UtcNow.Subtract(st);
                                min = total < min? total: min;
                                max = total > max? total: max;

                                WriteLine(res);
                            }
                            catch (Exception ex)
                            {
                                WriteLine($"Exception: {ex.Message}");
                            }
                        }

                        var totalProcessing = DateTime.UtcNow.Subtract(processingStart);
                        WriteLine($"{commands.Count}, Total Time: {totalProcessing.TotalSeconds} secs, Min: {min.TotalMilliseconds} ms   Max: {max.TotalMilliseconds} ms");

                    }
                    else break;
                }

               
                WriteLine("Good Bye!");
            }
            catch (Exception ex)
            {
                WriteLine(ex.ToString());
            }
        }

        private static List<MyCommand> CreateCommands(int limit)
        {
            var list = new List<MyCommand>(limit);
            for (var i = 0; i < limit; i++)
            {
                var ch = (char) (i % 100);
                list.Add(new MyCommand($"Command {i}", i));
            }

            return list;
        }
    }
}
