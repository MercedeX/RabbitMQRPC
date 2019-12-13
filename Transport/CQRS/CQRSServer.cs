using LanguageExt;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Threading.Tasks;
using static LanguageExt.Prelude;
using AMQP = RabbitMQ.Client;
using Unit = System.Reactive.Unit;

namespace Transport
{
    public class CQRSServer
    {
        readonly AMQPConfiguration _config;
        readonly ISerializer _serializer;
        readonly AMQP.ConnectionFactory _factory;
        readonly AMQP.IConnection _connection;
        readonly AMQP.IModel _model;

        readonly EventingBasicConsumer _cmdListener;
        readonly EventingBasicConsumer _qryListener;

        public event EventHandler<CommandArgs> CommandReceived;
        public event EventHandler<QueryArgs> QueryReceived;

        public CQRSServer(AMQPConfiguration config, ISerializer serializer)
        {
            _config = config;
            _serializer = serializer;

            _factory = new RabbitMQ.Client.ConnectionFactory
            {
                HostName = _config.Host,
                UserName = _config.UserId,
                Password = _config.Password,
                VirtualHost = _config.VirtualHost,
                Port = _config.Port,
                UseBackgroundThreadsForIO = true
            };
            _connection = _factory.CreateConnection();
            _model = _connection.CreateModel();

            
            _model.ExchangeDeclare(CQRSKonstants.KNQExchange, CQRSKonstants.K_EXCHANGE_TYPE_TOPIC, true, false);
            
            var okCmd = _model.QueueDeclare(CQRSKonstants.KCommandQueue, true, false, false);
            _model.QueueBind(CQRSKonstants.KCommandQueue, CQRSKonstants.KNQExchange, CQRSKonstants.KCommandTopic);

            var okQry = _model.QueueDeclare(CQRSKonstants.KQueryQueue, true, false, false);
            _model.QueueBind(CQRSKonstants.KQueryQueue, CQRSKonstants.KNQExchange, CQRSKonstants.KQueryTopic);
            
            _cmdListener = new AMQP.Events.EventingBasicConsumer(_model);
            _model.BasicConsume(CQRSKonstants.KCommandQueue, false, _cmdListener);
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
            _model.BasicConsume(CQRSKonstants.KQueryQueue, false, _qryListener);
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


        async Task ProcessCommand(Guid key, string replyTo, object query)
        {
            var ret = Either<Exception, Unit>.Bottom;
            try
            {
                CommandReceived?.Invoke(this, new CommandArgs(key, query));
                await Respond(key, replyTo, Unit.Default);
            }
            catch (Exception ex)
            {
               await Respond(key, replyTo, ex);
            }
        }
        async Task ProcessQuery(Guid key, string replyTo, object query)
        {
            try
            {
                var args = new QueryArgs(key, query);
                QueryReceived?.Invoke(this, args);
                await Respond(key, replyTo, args.Result);
            }
            catch (Exception ex)
            {
                await Respond(key, replyTo, ex);
            }
        }
        async Task Respond(Guid key, string replyQueue, object payload)
        {
            var reply = new TaskCompletionSource<Unit>();

            try
            {
                if (payload!=null)
                {
                    var props = _model.CreateBasicProperties();
                    props.CorrelationId = key.ToString();
                    props.Persistent = true;

                    var data = _serializer.Serialize(payload);
                    var que = replyQueue;
                    data.IfSome(x => _model.BasicPublish("", que, props, x));
                    reply.SetResult(Unit.Default); 
                }
            }
            catch (Exception ex)
            {
                reply.SetCanceled();
                reply.SetException(ex);
            }

            await reply.Task;
        }

    }
}
