// -----------------------------------------------------------------------
//   <copyright file="EndpointWriter.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AsyncCollection;
using Microsoft.Extensions.Logging;
using RSocket;
using RSocket.Transports;

namespace Proto.Remote
{
    public class EndpointWriter : IActor
    {
        private int _serializerId;
        private readonly string _address;
        private readonly ILogger _logger = Log.CreateLogger<EndpointWriter>();
        private readonly BlockingCollection<MessageBatch> _messageBatchQueue;
        private readonly IAsyncEnumerable<MessageBatch> _messages;
        private readonly AsyncCollection<MessageBatch> _messageQueue;

        private Remoting.RemotingClient _client;

        public EndpointWriter(string address)
        {
            _address = address;
            //_messageBatchQueue = new BlockingCollection<MessageBatch>();
            //_messages = Messages();

            _messageQueue = new AsyncCollection<MessageBatch>();
        }

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    await StartedAsync();
                    break;
                case Stopped _:
                    await StoppedAsync();
                    _logger.LogDebug($"Stopped EndpointWriter at {_address}");
                    break;
                case Restarting _:
                    await RestartingAsync();
                    break;
                case EndpointTerminatedEvent _:
                    context.Stop(context.Self);
                    break;
                case IEnumerable<RemoteDeliver> m:
                    var envelopes = new List<MessageEnvelope>();
                    var typeNames = new Dictionary<string, int>();
                    var targetNames = new Dictionary<string, int>();
                    var typeNameList = new List<string>();
                    var targetNameList = new List<string>();
                    foreach (var rd in m)
                    {
                        var targetName = rd.Target.Id;
                        var serializerId = rd.SerializerId == -1 ? _serializerId : rd.SerializerId;

                        if (!targetNames.TryGetValue(targetName, out var targetId))
                        {
                            targetId = targetNames[targetName] = targetNames.Count;
                            targetNameList.Add(targetName);
                        }

                        var typeName = Serialization.GetTypeName(rd.Message, serializerId);
                        if (!typeNames.TryGetValue(typeName, out var typeId))
                        {
                            typeId = typeNames[typeName] = typeNames.Count;
                            typeNameList.Add(typeName);
                        }

                        MessageHeader header = null;
                        if (rd.Header != null && rd.Header.Count > 0)
                        {
                            header = new MessageHeader();
                            header.HeaderData.Add(rd.Header.ToDictionary());
                        }

                        var bytes = Serialization.Serialize(rd.Message, serializerId);
                        var envelope = new MessageEnvelope
                        {
                            MessageData = bytes,
                            Sender = rd.Sender,
                            Target = targetId,
                            TypeId = typeId,
                            SerializerId = serializerId,
                            MessageHeader = header,
                        };

                        envelopes.Add(envelope);
                    }

                    var batch = new MessageBatch();
                    batch.TargetNames.AddRange(targetNameList);
                    batch.TypeNames.AddRange(typeNameList);
                    batch.Envelopes.AddRange(envelopes);

                    _messageQueue.Add(batch);

                    await foreach (var x in _client.Receive(new [] { batch }.ToAsyncEnumerable(), ReadOnlySequence<byte>.Empty))
                    {
                        ;
                    }

                    break;
            }
        }

        async IAsyncEnumerable<MessageBatch> Messages()
        {
            int a = 6;
            foreach (var messageBatch in _messageBatchQueue.GetConsumingEnumerable())
            {
                yield return messageBatch;
                await Task.Delay(0);
            }
        }

        //shutdown channel before restarting
        private Task RestartingAsync() => Task.CompletedTask; // TODO look at proper shutdown and restart

        //shutdown channel before stopping
        private Task StoppedAsync() => Task.CompletedTask; // TODO look at proper shutdown and restart

        private async Task StartedAsync()
        {
            _logger.LogDebug($"Connecting to address {_address}");

            var transport = new ClientSocketTransport(new Uri($"tcp://{_address}"));
            var rsocketClient = new RSocketClient(transport);
            await rsocketClient.ConnectAsync();

            _client = new Remoting.RemotingClient(rsocketClient);

            try
            {
                var res = await _client.Connect(new ConnectRequest(), ReadOnlySequence<byte>.Empty);
                _serializerId = res.DefaultSerializerId;

                /*
                _ = Task.Run(() =>
                {
                    _client.Receive(_messageQueue, ReadOnlySequence<byte>.Empty);
                });  */  
            }
            catch (Exception ex)
            {
                _logger.LogError($"RPC Failed to connect to address {_address}\n{ex}");
                //Wait for 2 seconds to restart and retry
                //Replace with Exponential Backoff
                await Task.Delay(2000);
                throw;
            }

            // TODO Understand the existing functionality and replicate if relevant
            _ = Task.Factory.StartNew(async () =>
            {
                try
                {
                    
                    //await _stream.ResponseStream.ForEachAsync(i => Actor.Done);
                }
                catch (Exception x)
                {
                    _logger.LogError($"Lost connection to address {_address}, reason {x.Message}");
                    var terminated = new EndpointTerminatedEvent
                    {
                        Address = _address
                    };
                    Actor.EventStream.Publish(terminated);
                }
            });

            var connected = new EndpointConnectedEvent
            {
                Address = _address
            };

            Actor.EventStream.Publish(connected);

            _logger.LogDebug($"Connected to address {_address}");
        }
    }
}