using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using AMQP = RabbitMQ.Client;
using Unit = System.Reactive.Unit;
using static LanguageExt.Prelude;

namespace Transport
{
    public class CommandArgs : EventArgs
    {
        public object Command { get;  }
        public Guid Key { get;   }

        public CommandArgs(Guid key, object command)
        {
            Key = key;
            Command = command;
        }
    }

    public class QueryArgs : EventArgs
    {
        public object Query { get; }
        public Guid Key { get;   }

        public QueryArgs(Guid key, object query)
        {
            Key = key;
            Query = query;
        }

        public void SetResult<TObject>(TObject obj)
        {
            Result = obj;
        }

        public object Result { get; private set; }
    }

    public class CQRSServer
    {
        private readonly ISerializer _serializer;
        private readonly AMQP.ConnectionFactory _factory;
        private readonly AMQP.IConnection _connection;
        private readonly AMQP.IModel _model;

        private readonly EventingBasicConsumer _cmdListener;
        private readonly EventingBasicConsumer _qryListener;

        private const string KCommandQueue = "NQ.Commands";
        private const string KQueryQueue = "NQ.Queries";
        private const string KNQExchange = "NQ.Exchange";

        public event EventHandler<CommandArgs> CommandReceived;
        public event EventHandler<QueryArgs> QueryReceived;

        public CQRSServer(ISerializer serializer)
        {
            _serializer = serializer;
            _factory = new RabbitMQ.Client.ConnectionFactory
            {
                HostName = "localhost",
                UserName = "nquser",
                Password = "nquser#123",
                VirtualHost = "/",
                Port = 5672
            };
            _connection = _factory.CreateConnection();
            _model = _connection.CreateModel();

            _model.ExchangeDeclare(KNQExchange, "topic", true, false);
            
            var okCmd = _model.QueueDeclare(KCommandQueue, true, false, false);
            _model.QueueBind(KCommandQueue, KNQExchange, "command");

            var okQry = _model.QueueDeclare(KQueryQueue, true, false, false);
            _model.QueueBind(KQueryQueue, KNQExchange, "query");

            
            _cmdListener = new AMQP.Events.EventingBasicConsumer(_model);
            _model.BasicConsume(KCommandQueue, false, _cmdListener);
            _cmdListener.Received += async (sender, args)=>
            {
                if (Guid.TryParse(args.BasicProperties.CorrelationId, out var key))
                {
                    var query = _serializer.Deserialize(args.Body);

                    var replyTo = args.BasicProperties.ReplyTo;

                    var ret = query.IfSomeAsync(x=>ProcessCommand(key, replyTo, x))
                        .ConfigureAwait(true)
                        .GetAwaiter()
                        .GetResult();

                    _model.BasicAck(args.DeliveryTag, false);
                   
                    Respond(key, replyTo, Unit.Default)
                        .GetAwaiter()
                        .GetResult();
                }

                await Task.Yield();
            };



            _qryListener = new AMQP.Events.EventingBasicConsumer(_model);
            _model.BasicConsume(KQueryQueue, false, _qryListener);
            _qryListener.Received += (sender, args) =>
            {
                if (Guid.TryParse(args.BasicProperties.CorrelationId, out var key))
                {
                    var query = _serializer.Deserialize(args.Body);
                    query.IfSomeAsync(x=>ProcessQuery(key, args.BasicProperties.ReplyTo, x)).GetAwaiter().GetResult();
                    _model.BasicAck(args.DeliveryTag, false);
                }

                //return Task.FromException(new Exception("No sender found"));
            };
        }



        private Either<Exception, Unit> ProcessCommand(Guid key, string replyTo, object query)
        {
            var ret = Either<Exception, Unit>.Bottom;
            try
            {
                CommandReceived?.Invoke(this, new CommandArgs(key, query));
                ret = Right(Unit.Default);
            }
            catch (Exception ex)
            {

               ret = Left(ex);
            }

            return ret;
        }

        private async Task ProcessQuery(Guid key, string replyTo, object query)
        {
            var args = new QueryArgs(key, query);
            QueryReceived?.Invoke(this, args);
            await Respond(key, replyTo, args.Result);
        }


        private Task Respond(in Guid key, in string replyQueue, in object payload)
        {
            var reply = new TaskCompletionSource<Unit>();

            try
            {
                // Send thru AMQP now
                var props = _model.CreateBasicProperties();
                props.CorrelationId = key.ToString();
                props.Persistent = true;

                var data = _serializer.Serialize(payload);
                var que = replyQueue;
                data.IfSome(x => _model.BasicPublish("", que, props, x));
                reply.SetResult(Unit.Default);
            }
            catch (Exception ex)
            {
                reply.SetCanceled();
                reply.SetException(ex);
            }

            return reply.Task;
        }

    }
}
