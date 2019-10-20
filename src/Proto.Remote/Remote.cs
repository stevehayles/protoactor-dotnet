// -----------------------------------------------------------------------
//   <copyright file="Remote.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RSocket;
using RSocket.RPC;
using RSocket.Transports;

namespace Proto.Remote
{
    public static class Remote
    {
        private static readonly ILogger Logger = Log.CreateLogger(typeof(Remote).FullName);

        private static RSocketRPCServer _server;
        private static readonly Dictionary<string, Props> Kinds = new Dictionary<string, Props>();
        public static RemoteConfig RemoteConfig { get; private set; }
        public static PID ActivatorPid { get; private set; }

        private static EndpointReader _endpointReader;

        public static string[] GetKnownKinds()
        {
            return Kinds.Keys.ToArray();
        }

        public static void RegisterKnownKind(string kind, Props props)
        {
            Kinds.Add(kind, props);
        }

        public static Props GetKnownKind(string kind)
        {
            if (Kinds.TryGetValue(kind, out var props)){
                return props;
            }
            throw new ArgumentException($"No Props found for kind '{kind}'");
        }

        public static void Start(string hostname, int port)
        {
            Start(hostname, port, new RemoteConfig());
        }

        public static void Start(string hostname, int port, RemoteConfig config)
        {
            RemoteConfig = config;

            ProcessRegistry.Instance.RegisterHostResolver(pid => new RemoteProcess(pid));

            EndpointManager.Start();         
            _endpointReader = new EndpointReader();

            //var uri = new Uri($"tcp://{hostname}:{port}");
            //_server = new RSocketRPCServer(uri, _endpointReader);
            //_server.Start();

            var loopback = new LoopbackTransport();
            var server = new RSocketServer(loopback.Beyond);
            server.ConnectAsync().Wait();
            RSocketService.Register(server, _endpointReader);

            
            _ = Task.Run(async () =>
            {
                //var transport = new ClientSocketTransport(new Uri($"tcp://{hostname}:{port}"));
                var client = new RSocketClient(loopback);
                var service = new Remoting.RemotingClient(client);
                await client.ConnectAsync();

                var m = new[] { new RemoteDeliver(new Proto.MessageHeader(), "testmessage", new PID("add", "id"), new PID("add", "id"), 0) };
                var envelopes = new List<MessageEnvelope>();
                var typeNames = new Dictionary<string, int>();
                var targetNames = new Dictionary<string, int>();
                var typeNameList = new List<string>();
                var targetNameList = new List<string>();
                foreach (var rd in m)
                {
                    var targetName = rd.Target.Id;
                    var serializerId = rd.SerializerId == -1 ? 0 : rd.SerializerId;

                    if (!targetNames.TryGetValue(targetName, out var targetId))
                    {
                        targetId = targetNames[targetName] = targetNames.Count;
                        targetNameList.Add(targetName);
                    }

                    var typeName = "type";// Serialization.GetTypeName(rd.Message, serializerId);
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

                    var bytes = System.Text.Encoding.UTF8.GetBytes("MY PAYLOAD");// Serialization.Serialize(rd.Message, serializerId);
                    var envelope = new MessageEnvelope
                    {
                        MessageData = Google.Protobuf.ByteString.CopyFromUtf8("Test to see if data gets longer"),
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

                //await service.Connect(new ConnectRequest(), ReadOnlySequence<byte>.Empty);

                var results = await service.Receive(new[] { batch, batch, batch }.ToAsyncEnumerable()).ToListAsync();

                /*
                await foreach (var unit in service.Receive(new[] { batch, batch, batch }.ToAsyncEnumerable(), ReadOnlySequence<byte>.Empty))
                {
                    ;
                }
                */

            });

            var address = $"{hostname}:{port}";
            ProcessRegistry.Instance.Address = address;

            SpawnActivator();

            Logger.LogDebug($"Starting Proto.Actor server on {address}");
        }

        public static void Shutdown(bool gracefull = true)
        {
            try
            {
                if (gracefull)
                {
                    EndpointManager.Stop();
                    _endpointReader.Suspend(true);
                    StopActivator();
                    //_server.ShutdownAsync().Wait(10000);
                }
                else
                {
                    //_server.KillAsync().Wait(10000);
                }
                
                Logger.LogDebug($"Proto.Actor server stopped on {ProcessRegistry.Instance.Address}. Graceful:{gracefull}");
            }
            catch(Exception ex)
            {
                //_server.KillAsync().Wait(1000);
                Logger.LogError($"Proto.Actor server stopped on {ProcessRegistry.Instance.Address} with error:\n{ex.Message}");
            }
        }

        private static void SpawnActivator()
        {
            var props = Props.FromProducer(() => new Activator()).WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
            ActivatorPid = RootContext.Empty.SpawnNamed(props, "activator");
        }

        private static void StopActivator()
        {
            RootContext.Empty.Stop(ActivatorPid);
        }

        public static PID ActivatorForAddress(string address)
        {
            return new PID(address, "activator");
        }

        public static Task<ActorPidResponse> SpawnAsync(string address, string kind, TimeSpan timeout)
        {
            return SpawnNamedAsync(address, "", kind, timeout);
        }

        public static async Task<ActorPidResponse> SpawnNamedAsync(string address, string name, string kind, TimeSpan timeout)
        {
            var activator = ActivatorForAddress(address);

            var res = await RootContext.Empty.RequestAsync<ActorPidResponse>(activator, new ActorPidRequest
            {
                Kind = kind,
                Name = name
            }, timeout);

            return res;
        }

        public static void SendMessage(PID pid, object msg, int serializerId)
        {
            var (message, sender, header) = Proto.MessageEnvelope.Unwrap(msg);

            var env = new RemoteDeliver(header, message, pid, sender, serializerId);
            EndpointManager.RemoteDeliver(env);
        }
    }
}