using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace Proto
{
    public abstract class ActorModule : IActorModule
    {
        public void Configure(IServiceCollection services)
        {
            services.AddSingleton<IActorFactory, ActorFactory>();

            var registry = new ActorPropsRegistry();
            Load(registry);
            services.AddSingleton(registry);
        }

        public abstract void Load(ActorPropsRegistry props);
    }
}
