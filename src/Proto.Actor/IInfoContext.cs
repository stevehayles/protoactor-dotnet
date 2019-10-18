// -----------------------------------------------------------------------
//   <copyright file="IContext.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Proto
{
    public interface IInfoContext
    {
        /// <summary>
        ///     Gets the PID for the parent of the current actor.
        /// </summary>
        PID Parent { get; }

        /// <summary>
        ///     Gets the PID for the current actor.
        /// </summary>
        PID Self { get; }

        /// <summary>
        ///     Gets the PID of the actor that sent the currently processed message.
        /// </summary>
        PID Sender { get; }

        /// <summary>
        ///     Gets the actor associated with this context.
        /// </summary>
        IActor Actor { get; }
    }
}