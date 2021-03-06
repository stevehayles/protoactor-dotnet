﻿// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Messages;
using Proto;
using Proto.Remote;
using ProtosReflection = Messages.ProtosReflection;

namespace Node2
{
    public class EchoActor : IActor
    {
        private PID _sender;

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case StartRemote sr:
                    Console.WriteLine("Starting");
                    _sender = sr.Sender;
                    context.Respond(new Start());
                    return Actor.Done;
                case Ping _:
                    context.Send(_sender, new Pong());
                    return Actor.Done;
                default:
                    return Actor.Done;
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var system = new ActorSystem();
            var context = new RootContext(system);
            var serialization = new Serialization();
            serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
            var Remote = new Remote(system, serialization);
            Remote.Start("127.0.0.1", 12000);
            context.SpawnNamed(Props.FromProducer(() => new EchoActor()), "remote");
            Console.ReadLine();
        }
    }
}