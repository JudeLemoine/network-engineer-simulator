using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ServiceHost : MonoBehaviour
{
    [Serializable]
    public class Service
    {
        public string name = "Service";
        public ServiceProtocol protocol = ServiceProtocol.Tcp;
        public int port = 80;
        [TextArea] public string banner = "";
        public bool enabled = true;
    }

    public enum ServiceProtocol { Tcp, Udp }

    [Header("Listening Services")]
    public List<Service> services = new List<Service>();

    public bool IsOpen(ServiceProtocol proto, int port, out Service svc)
    {
        foreach (var s in services)
        {
            if (s == null || !s.enabled) continue;
            if (s.protocol == proto && s.port == port)
            {
                svc = s;
                return true;
            }
        }
        svc = null;
        return false;
    }
}
