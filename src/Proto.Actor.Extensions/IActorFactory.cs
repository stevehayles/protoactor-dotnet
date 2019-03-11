using System;

namespace Proto
{
    public interface IActorFactory
    {
        /// <summary>
        /// Get or Create a Local Actor by Type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id"></param>
        /// <param name="address"></param>
        /// <param name="parent"></param>
        /// <param name="props"></param>
        /// <param name="parameters"></param>
        /// <returns>The PID of the Actor</returns>
        PID GetActor<T>(string id = null, string address = null, IContext parent = null, Func<Props, Props> props = null, params object[] parameters)
            where T : IActor;

        /// <summary>
        /// Get a Local Actor by id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="address"></param>
        /// <param name="parent"></param>
        /// <returns>The PID of the Actor</returns>
        PID GetActor(string id, string address = null, IContext parent = null);

        /// <summary>
        /// Register an existing instance as an Actor by Type or Id.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="actor"></param>
        /// <param name="id"></param>
        /// <param name="address"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        PID RegisterActor<T>(T actor, string id = null, string address = null, IContext parent = null)
            where T : IActor;
    }
}