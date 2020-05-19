using System;
using System.Collections.Generic;
using System.Text;

namespace Proto
{
    public interface IProtoActorModule
    {
        void Init(ActorPropsRegistry actorPropsRegistry);
    }
}
