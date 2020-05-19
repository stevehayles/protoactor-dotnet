﻿// -----------------------------------------------------------------------
//   <copyright file="MemberStatus.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Proto.Cluster
{
    public class MemberStatus
    {
        public MemberStatus(string memberId, string host, int port, IReadOnlyCollection<string> kinds, bool alive, IMemberStatusValue statusValue)
        {
            MemberId = memberId;
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Kinds = kinds ?? throw new ArgumentNullException(nameof(kinds));
            Port = port;
            Alive = alive;
            StatusValue = statusValue;
        }

        public string Address => Host + ":" + Port;
        public string MemberId { get; }
        public string Host { get; }
        public int Port { get; }
        public IReadOnlyCollection<string> Kinds { get; }
        public bool Alive { get; }
        public IMemberStatusValue StatusValue { get; }
    }

    public interface IMemberStatusValue
    {
        bool IsSame(IMemberStatusValue val);
    }

    public interface IMemberStatusValueSerializer
    {
        string Serialize(IMemberStatusValue val);
        IMemberStatusValue Deserialize(string val);
    }

    internal class NullMemberStatusValueSerializer : IMemberStatusValueSerializer
    {
        public string Serialize(IMemberStatusValue val)
        {
            return "";
        }

        public IMemberStatusValue Deserialize(string val)
        {
            return null;
        }
    }
}