using System.Collections.Generic;

namespace CsXray.Models
{
    public class AppSettings
    {
        public string ProxyMode { get; set; } = "pac"; // direct, proxy, pac
        public int SocksPort { get; set; } = 10808;
        public int HttpPort { get; set; } = 10809;
        public int PacPort { get; set; } = 10807;
        public string? ActiveNodeId { get; set; }
        public bool AutoTestOnStart { get; set; } = true;
        public List<NodeConfig> Nodes { get; set; } = new();
        public List<string> SubscribeUrls { get; set; } = new();
    }
}
