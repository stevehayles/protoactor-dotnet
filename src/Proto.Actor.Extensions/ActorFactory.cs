using System;
using Microsoft.Extensions.DependencyInjection;

namespace Proto
{
    public class ActorFactory : IActorFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ActorPropsRegistry _actorPropsRegistry;
        private readonly ActorSystem _actorSystem;

        public ActorFactory(ActorSystem actorSystem, IServiceProvider serviceProvider, ActorPropsRegistry actorPropsRegistry)
        {
            _actorSystem = actorSystem;
            _serviceProvider = serviceProvider;
            _actorPropsRegistry = actorPropsRegistry;
        }

        public PID RegisterActor<T>(T actor, string id = null, string address = null, IContext parent = null)
            where T : IActor
        {
            id = id ?? ProcessRegistry.Instance.NextId();

            return GetOrCreateActor(id, address, parent, () => CreateActor<T>(id, parent, () => new Props().WithProducer(() => actor)));
        }

        public PID GetActor(string id, string address = null, IContext parent = null)
        {
            return GetOrCreateActor(id, address, parent, () => throw new InvalidOperationException($"Actor not created {id}"));
        }

        public PID GetActor<T>(string id = null, string address = null, IContext parent = null, Func<Props, Props> props = null, params object[] parameters)
            where T : IActor
        {
            id = id ?? _actorSystem.ProcessRegistry.NextId();

            var newProps = new Props().WithProducer(() => ActivatorUtilities.CreateInstance<T>(_serviceProvider, parameters));

            var producer = (props != null)
                ? (Func<Props>)(() => props(newProps))
                : () => newProps;

            return GetOrCreateActor(id, address, parent, () => CreateActor<T>(id, parent, producer));
        }

        private PID GetOrCreateActor(string id, string address, IContext parent, Func<PID> create)
        {
            address = address ?? "nonhost";

            var pidId = id;
            if (parent != null)
            {
                pidId = $"{parent.Self.Id}/{id}";
            }

            var pid = new PID(address, pidId);
            var reff = ProcessRegistry.Instance.Get(pid);
            if (reff is DeadLetterProcess)
            {
                pid = create();
            }

            return pid;
        }

        private PID CreateActor<T>(string id, IContext parent, Func<Props> producer)
            where T : IActor
        {
            var props = (_actorPropsRegistry.RegisteredProps.TryGetValue(typeof(T), out var registeredProps))
                ? registeredProps(producer())
                : producer();

            if (parent == null)
            {
                return _actorSystem.Root.SpawnNamed(props, id);
            }

            return parent.SpawnNamed(props, id);
        }
    }
}