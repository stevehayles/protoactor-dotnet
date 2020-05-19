using System;
using Microsoft.Extensions.DependencyInjection;

namespace Proto
{
    public static class ServiceCollectionExtensions
    {
        public static void AddProtoActor(this IServiceCollection services, Action<ActorPropsRegistry> registerAction = null)
        {
            services.AddSingleton<IActorFactory, ActorFactory>();

            var registry = new ActorPropsRegistry();
            registerAction?.Invoke(registry);
            services.AddSingleton(registry);
            services.AddSingleton<ActorSystem>();
        }
        public static void AddProtoActor<T>(this IServiceCollection services) where T : IProtoActorModule
        {
            services.AddSingleton<ActorPropsRegistry>();

            services.AddSingleton<IActorFactory, ActorFactory>(provider =>
            {
                var module = (T)ActivatorUtilities.CreateInstance(provider, typeof(T));
                module.Init(provider.GetService<ActorPropsRegistry>());

                return (ActorFactory)ActivatorUtilities.CreateInstance(provider, typeof(ActorFactory));
            });
        }
    }
}