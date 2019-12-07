using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation.Results;
using LanguageExt;
using Newtonsoft.Json;
using RabbitMQ.Client;
using AMQP = RabbitMQ.Client;
using Unit = System.Reactive.Unit;

using static LanguageExt.Prelude;


namespace Transport
{
    public class CQRSImpl: ICQRS, IDisposable
    {
        private readonly ISerializer _serializer;
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<Unit>> _responses;

        private readonly AMQP.ConnectionFactory _factory;
        private readonly AMQP.IConnection _connection;
        private readonly AMQP.IModel _model;
        private readonly string KResponseQueue;
        private const string KNQExchange = "NQ.Exchange";


        public CQRSImpl(ISerializer serializer)
        {
            _serializer = serializer;
            _responses = new ConcurrentDictionary<Guid, TaskCompletionSource<Unit>>(10, 4096);

            KResponseQueue = $"NQ.Responses.{Guid.NewGuid()}";

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
            var ok = _model.QueueDeclare(KResponseQueue, false, true, true);

            _model.ExchangeDeclare(KNQExchange, "topic", true, false);

            var listener = new RabbitMQ.Client.Events.EventingBasicConsumer(_model);
            _model.BasicConsume(KResponseQueue, true, listener);

            listener.Received += (sender, args) =>
            {
                
                if (Guid.TryParse(args.BasicProperties.CorrelationId, out var key) && _responses.ContainsKey(key))
                {
                    _responses.TryRemove(key, out var tcs);
                    var r1 = _serializer.Deserialize(args.Body).FirstOrDefault();
                    var r2 = r1 is Unit? Unit.Default: Unit.Default;
                    tcs.SetResult(r2);
                }
            };
        }
        private Task<Unit> SendEnvelope<TCommand>(TCommand command, string topic)
        {
            var reply = new TaskCompletionSource<Unit>();

            try
            {
                var key = Guid.NewGuid();

                var validationResult = CommandIsValid(command);
                if (validationResult.ret)
                {
                    if (_responses.TryAdd(key, reply))
                    {
                        // Send thru AMQP now
                        var props = _model.CreateBasicProperties();
                        props.CorrelationId = key.ToString();
                        props.ReplyTo = KResponseQueue;
                        props.Persistent = true;

                        var data = _serializer.Serialize(command).FirstOrDefault();
                        _model.BasicPublish(KNQExchange, topic, props, data);
                    }
                    else throw new FluentValidation.ValidationException(validationResult.result.Errors);
                }
                else throw new Exception($"not a valid {topic}, could not find validation available on command");
            }
            catch (Exception ex)
            {
                reply.SetCanceled();
                reply.SetException(ex);
            }

            return reply.Task;
        }

        private (bool ret, ValidationResult result) CommandIsValid<TCommand>(TCommand command)
        {
            var ret = (ret: true, result: new ValidationResult());

            if (command is FluentValidation.AbstractValidator<TCommand> cmd)
            {
                var res = cmd.Validate(command);
                ret = (!res.Errors.Any(), res);
            }

            return ret;
        }


        public void Dispose()
        {
            _model.Close();
            _model.Dispose();

            _connection.Close();
            _connection.Dispose();
        }



        public Task<Unit> Execute<TCommand>(TCommand command, CancellationToken token)
        {
            return SendEnvelope(command, "command");
        }
        public Task<TResponse> Ask<TResponse, TRequest>(TRequest request, CancellationToken token) => throw new NotImplementedException();

    }
}
