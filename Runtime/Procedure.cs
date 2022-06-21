﻿namespace Labyrinth.Runtime
{
    using Bolt;

    public struct Procedure : IRemote<byte>
    {
        public enum Rule
        {
            Any,
            Server,
            Client
        }

        public Procedure(byte value, Rule control, Relevance relevancy, Read callback)
        {
            Value = value;
            Control = control;
            Relevancy = relevancy;
            Callback = callback;
        }
        public byte Value { get; }
        public Rule Control { get; }
        public Relevance Relevancy { get; }
        public Read Callback { get; }
    }
}