// -----------------------------------------------------------------------
//   <copyright file="EndpointReader.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Proto.Mailbox;
using RSocket.RPC;
using static Proto.Remote.Remoting;

namespace Proto.Remote
{
    public class EndpointReader : IRSocketService, IRemoting
    {     
        private bool _suspended;

        public string ServiceName => __Service;

        public Task<ConnectResponse> Connect(ConnectRequest message, ReadOnlySequence<byte> metadata)
        {
            if (_suspended)
                throw new Exception("Suspended");

            return Task.FromResult(new ConnectResponse() { DefaultSerializerId = Serialization.DefaultSerializerId });
        }

        public async IAsyncEnumerable<Unit> Receive(IAsyncEnumerable<MessageBatch> messageBatches, ReadOnlySequence<byte> metadata)
        {
            var targets = new PID[100];

            await foreach(var batch in messageBatches)
            {
                if (_suspended)
                    break;

                //only grow pid lookup if needed
                if (batch.TargetNames.Count > targets.Length)
                {
                    targets = new PID[batch.TargetNames.Count];
                }

                for (int i = 0; i < batch.TargetNames.Count; i++)
                {
                    targets[i] = new PID(ProcessRegistry.Instance.Address, batch.TargetNames[i]);
                }

                var typeNames = batch.TypeNames.ToArray();
                foreach (var envelope in batch.Envelopes)
                {
                    var target = targets[envelope.Target];
                    var typeName = typeNames[envelope.TypeId];
                    var message = Serialization.Deserialize(typeName, envelope.MessageData, envelope.SerializerId);

                    if (message is Terminated msg)
                    {
                        var rt = new RemoteTerminate(target, msg.Who);
                        EndpointManager.RemoteTerminate(rt);
                    }
                    else if (message is SystemMessage sys)
                    {
                        target.SendSystemMessage(sys);
                    }
                    else
                    {
                        Proto.MessageHeader header = null;
                        if (envelope.MessageHeader != null)
                        {
                            header = new Proto.MessageHeader(envelope.MessageHeader.HeaderData);
                        }
                        var localEnvelope = new Proto.MessageEnvelope(message, envelope.Sender, header);
                        RootContext.Empty.Send(target, localEnvelope);
                    }
                }

                yield return new Unit();
            }
        }

        public IAsyncEnumerable<ReadOnlySequence<byte>> Dispatch(ReadOnlySequence<byte> data, string method, ReadOnlySequence<byte> tracing, ReadOnlySequence<byte> metadata, IAsyncEnumerable<ReadOnlySequence<byte>> messages)
        {
            switch (method)
            {
                case __Method_Connect:
                    {
                        return Connect(ConnectRequest.Parser.ParseFrom(data.ToArray()), metadata)
                            .ToAsyncEnumerable()
                            .Select(result => new ReadOnlySequence<byte>(Google.Protobuf.MessageExtensions.ToByteArray(result)));
                    }
                case __Method_Receive:
                    {
                        return Receive(messages.Select(message =>
                        {
                            return MessageBatch.Parser.ParseFrom(message.ToArray());
                        }), metadata).Select(unit => new ReadOnlySequence<byte>(Google.Protobuf.MessageExtensions.ToByteArray(unit)));
                    }
                default: throw new InvalidOperationException("Unknown method: " + method);
            }
        }

        public void Suspend(bool suspended)
        {
            _suspended = suspended;
        }
    }
}