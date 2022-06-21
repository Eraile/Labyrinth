﻿using UnityEngine;

using System;
using System.Collections.Generic;

namespace Labyrinth.Runtime
{
    using Bolt;

    // processed mainly on the server 
    // main network manager and for relevance
    [RequireComponent(typeof(World)), AddComponentMenu("Labyrinth/Central")]
    public sealed class Central : MonoBehaviour
    {
        internal static Central n_instance;

        [SerializeField] private int[] m_networkedScenes = new int[0];

        private void Awake()
        {
            if (n_instance)
            {
                Debug.LogError("There should only be 1 Central inbetween all loaded scenes");
                Destroy(gameObject);
                return;
            }
            n_instance = this;
        }

        private void OnDestroy()
        {
            n_instance = null;
        }

        internal bool NetworkScene(int identity)
        {
            for (int i = 0; i < m_networkedScenes.Length; i++)
            {
                if (m_networkedScenes[i] == identity)
                {
                    return true;
                }
            }
            return false;
        }

        // *** redo ****
        public static bool Relevant(int authority, Vector3 point, Relevance relevancy)
        {
            switch (relevancy)
            {
                case Relevance.None:
                    return true;
                case Relevance.General:
                    return Relevant(authority, point, Relevance.Observers) || Relevant(authority, point, Relevance.Sectors);
            }

            if (Observer.n_observers.ContainsKey(authority))
            {
                foreach (var observer in Observer.n_observers[authority])
                {
                    switch (relevancy)
                    {
                        case Relevance.Sectors:
                            foreach (var station in Sector.n_sectors)
                            {
                                if (station.Overlap(observer.transform.position, point))
                                {
                                    return true;
                                }
                            }
                            break;
                        case Relevance.Observers:
                            return observer.Contains(point);
                    }
                }
            }

            return false;
        }

        public static void Relavant(Vector3 point, Relevance relevancy, Action<int> callback, Func<int, bool> filter = null)
        {
            foreach (var connection in Observer.n_observers)
            {
                if (filter?.Invoke(connection.Key) ?? true)
                {
                    if (Relevant(connection.Key, point, relevancy))
                    {
                        callback(connection.Key);
                    }
                }
            }
        }

        [RuntimeInitializeOnLoadMethod]
        private static void Initialize()
        {
            Network.connected.AddListener(Connection);
            Network.disconnected.AddListener(Disconnection);
        }

        private static void Connection(int socket, int connection)
        {
            Debug.Log($"Host({connection}) connected");
        }

        private static void Disconnection(int socket, int connection)
        {
            Debug.Log($"Host({connection}) disconnected");
        }
    }
}