using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Proto
{
    public interface IActorModule
    {
        void Configure(IServiceCollection services);
    }
}
