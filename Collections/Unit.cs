﻿using System;
using System.Collections.Generic;

namespace Labyrinth.Collections
{
    using Bolt;
    using Labyrinth.Background;

    // Base class for all collections types
    public abstract class Unit
    {
        private uint m_steps = 0/*total number of changes*/;
        private uint m_marker = 0/*frist step of changes that will be sent*/;
        private bool m_reconfiguring = false;
        private readonly Queue<Change> m_changes = new Queue<Change>();
        private readonly Dictionary<uint, Action> m_pending = new Dictionary<uint, Action>();

        public ulong identifier { get; internal set; }
        public bool changed => m_changes.Count > 0;

        protected Unit()
        {
            // until apply is called
            m_reconfiguring = true;
        }

        // instance id for an instance (clone) of a class (for static classes 0)
        // member differentiates between each unit within an instance
        // (Static classes)
        //          for server use Network.initialized,
        //          clients use Network.connected
        //          (check if the connection is actually the server)
        public bool Create(string type, ushort instance, ushort member)
        {
            return Objects.Add(type, instance, member, this);
        }

        // (Static classes)
        //          for server use Network.terminating,
        //          clients use Network.disconnected
        //          (check if the connection is actually the server)
        public void Destroy()
        {
            Objects.Remove(identifier);
        }

        // these classes can only be edited by a server
        //      client only read from it
        public bool IsReadOnly => NetworkClient.Active;

        protected void Change(bool additive, Step action)
        {
            // we don't need to add to the queue when the server isn't running
            if (!NetworkServer.Active)
                return;

            if (!additive)
            {
                m_changes.Clear();
                m_steps = m_marker;
            }

            m_changes.Enqueue(new Change(action, null));

            if (additive)
            {
                m_steps++;
            }
        }

        protected void Change<I>(bool additive, Step action, I arg)
        {
            // we don't need to add to the queue when the server isn't running
            if (!NetworkServer.Active)
                return;

            if (!additive)
            {
                m_changes.Clear();
                m_steps = m_marker;
            }

            m_changes.Enqueue(new Change(action,
                (ref Writer writer) =>
                {
                    writer.Write(arg);
                }));

            if (additive)
            {
                m_steps++;
            }
        }

        protected void Change<I, T>(bool additive, Step action, I arg1, T arg2)
        {
            // we don't need to add to the queue when the server isn't running
            if (!NetworkServer.Active)
                return;

            if (!additive)
            {
                m_changes.Clear();
                m_steps = m_marker;
            }

            m_changes.Enqueue(new Change(action,
                (ref Writer writer) =>
                {
                    writer.Write(arg1);
                    writer.Write(arg2);
                }));

            if (additive)
            {
                m_steps++;
            }
        }

        // for new clients connections
        internal void Clone(ref Writer writer)
        {
            NetworkDebug.Slient($"Cloning Step({m_steps}) for Object({identifier})");
            writer.Write(m_steps);
            Serialize(ref writer);
        }

        internal void Apply(ref Reader reader)
        {
            m_steps = reader.ReadUInt();
            NetworkDebug.Slient($"Applying Step({m_steps}) for Object({identifier})");
            Deserialize(ref reader);

            Clean();
            Release();
            m_reconfiguring = false;
        }

        /// remove any previous step
        private void Clean()
        {
            HashSet<uint> steps = new HashSet<uint>();
            foreach (var step in m_pending)
            {
                if (step.Key <= m_steps)
                {
                    steps.Add(step.Key);
                }
            }
            foreach (var step in steps)
            {
                m_pending.Remove(step);
            }
        }

        // for clients already connected
        internal void Copy(ref Writer writer, bool listeners)
        {
            // we only write to the buffer when clients have this object
            if (listeners)
            {
                NetworkDebug.Slient($"Copying Step({m_marker}) to Step({m_marker + m_changes.Count - 1}) for Object({identifier})");
                writer.Write(m_marker);
                writer.Write(m_changes.Count);
                while (m_changes.Count > 0)
                {
                    Change state = m_changes.Dequeue();
                    writer.Write((byte)state.Operation);
                    state.Callback?.Invoke(ref writer);
                }
            }
            else
            {
                m_changes.Clear();
            }
            // the marker should still move on, no matter what
            m_marker = m_steps;
        }

        internal void Paste(ref Reader reader)
        {
            uint marker = reader.ReadUInt();
            int count = reader.ReadInt();

            // add to pending
            while (count > 0)
            {
                Step step = (Step)reader.Read();
                // the args would have been read (and still be somewhere memory) 
                // call the action when we get there
                Action action = Deserialize(step, ref reader);
                m_pending.Add(marker, action);
                NetworkDebug.Slient($"Step({marker}) pending for Object{identifier}");
                marker++;
                count--;
            }

            /*if it recieved a step before it's current step*/
            if (marker < m_steps && !m_reconfiguring)
            {
                /// then this unit is out of order
                /// request for a clone
                Network.Forward(Channels.Irregular, Objects.Find, (ref Writer writer) =>
                {
                    writer.Write(identifier);
                });
                m_reconfiguring = true;
            }

            if (!m_reconfiguring)
            {
                Release();
            }
        }

        private void Release()
        {
            // release the next steps that are pending
            while (m_pending.ContainsKey(m_steps))
            {
                NetworkDebug.Slient($"Releasing Step({m_steps}) for Object{identifier}");

                m_pending[m_steps]();
                m_pending.Remove(m_steps);
                m_steps++;
            }
        }

        protected abstract void Serialize(ref Writer writer);
        protected abstract void Deserialize(ref Reader reader);
        protected abstract Action Deserialize(Step step, ref Reader reader);
    }
}