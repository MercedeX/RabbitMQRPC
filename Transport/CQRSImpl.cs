using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<Unit>> _commandResults;
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<object>> _queryResults;

        private readonly AMQP.ConnectionFactory _factory;
        private readonly AMQP.IConnection _connection;
        private readonly AMQP.IModel _model;
        private readonly string KResponseQueue;
        private const string KNQExchange = "NQ.Exchange";


        public CQRSImpl(ISerializer serializer)
        {
            _serializer = serializer;
            
            _commandResults = new ConcurrentDictionary<Guid, TaskCompletionSource<Unit>>(10, 4096);
            _queryResults = new ConcurrentDictionary<Guid, TaskCompletionSource<object>>(10, 4096);


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

            SetupResponseListener();
        }

        private void SetupResponseListener()
        {
            var listener = new RabbitMQ.Client.Events.EventingBasicConsumer(_model);
            _model.BasicConsume(KResponseQueue, true, listener);
            listener.Received += (sender, args) =>
            {
                if (Guid.TryParse(args.BasicProperties.CorrelationId, out var key) )
                {
                    if (_commandResults.ContainsKey(key))
                    {
                        _commandResults.TryRemove(key, out var tcs);
                        var r1 = _serializer.Deserialize(args.Body).FirstOrDefault();
                        switch (r1)
                        {
                            case Unit v:
                                tcs.SetResult(v);
                                break;
                            case Exception e:
                                tcs.SetException(e);
                                tcs.SetCanceled();
                                break;
                        } 
                    }
                    else if (_queryResults.ContainsKey(key))
                    {
                        _queryResults.TryRemove(key, out var tcs);
                        var r1 = _serializer.Deserialize(args.Body).FirstOrDefault();
                        switch (r1)
                        {
                            case IEnumerable<object> list:
                                tcs.SetResult(list);
                                break;
                            case Exception e:
                                tcs.SetException(e);
                                tcs.SetCanceled();
                                break;
                            case object obj:
                                tcs.SetResult(obj);
                                break;
                        } 
                    }
                }
            };
        }

        private void SendEnvelope<TCommand>(Guid key, TCommand command, string topic)
        {
            var validationResult = CommandIsValid(command);
            if (validationResult.ret)
            {
                    // Send thru AMQP now
                    var props = _model.CreateBasicProperties();
                    props.CorrelationId = key.ToString();
                    props.ReplyTo = KResponseQueue;
                    props.Persistent = true;

                    var data = _serializer.Serialize(command).FirstOrDefault();
                    _model.BasicPublish(KNQExchange, topic, props, data);
            }
            else throw new Exception($"not a valid {topic}, could not find validation available on command");
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
            var key = Guid.NewGuid();
            var tcs = new TaskCompletionSource<Unit>();

            try
            {
                if (_commandResults.TryAdd(key, tcs))
                {
                    SendEnvelope(key, command, "command");
                }
                else throw new Exception("Could not add the command to the waiting queue");
            }
            catch (Exception ex) when (ex.Message.Contains("waiting queue"))
            {
                tcs.SetException(ex);
                tcs.SetCanceled();
            }
            catch (Exception ex)
            {
                _commandResults.TryRemove(key, out var _);
                tcs.SetException(ex);
                tcs.SetCanceled();
            }

            return tcs.Task;
        }

        public async Task<TResponse> Ask<TResponse>(object request, CancellationToken token) where TResponse: class
        {
            var key = Guid.NewGuid();
            var tcs = new TaskCompletionSource<object>();

            try
            {
                if (_queryResults.TryAdd(key, tcs))
                {
                    SendEnvelope(key, request, "query");
                }
                else throw new Exception("Could not add the query to the waiting queue");
            }
            catch (Exception ex) when (ex.Message.Contains("waiting queue"))
            {
                tcs.SetException(ex);
                tcs.SetCanceled();
            }
            catch (Exception ex)
            {
                _queryResults.TryRemove(key, out var _);
                tcs.SetException(ex);
                tcs.SetCanceled();
            }

            var tmp = await tcs.Task;
            return tmp as TResponse;
        }

    }
}
