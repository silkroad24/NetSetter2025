using System;

namespace NetSetter
{
    [Serializable]
    public class NetworkProfile
    {
        public string ProfileName { get; set; }
        public string IpAddress { get; set; }
        public string SubnetMask { get; set; }
        public string Gateway { get; set; }
        public string Dns1 { get; set; }
        public string Dns2 { get; set; }
    }
}
