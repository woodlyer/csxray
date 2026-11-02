using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using CsXray.Models;

namespace CsXray.Services
{
    public static class LinkParser
    {
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        public static List<NodeConfig> ImportFromText(string text)
        {
            var results = new List<NodeConfig>();
            if (string.IsNullOrWhiteSpace(text)) return results;

            text = text.Trim();

            // Try Base64 decode whole content (common for raw subscription responses)
            string decodedWhole = TryBase64Decode(text);
            string contentToProcess = !string.IsNullOrWhiteSpace(decodedWhole) && decodedWhole.Contains("://")
                ? decodedWhole
                : text;

            // Try JSON array (WinXray or standard JSON format)
            if (contentToProcess.TrimStart().StartsWith("["))
            {
                try
                {
                    var jsonNodes = TryParseJsonArray(contentToProcess);
                    if (jsonNodes.Count > 0)
                    {
                        return jsonNodes;
                    }
                }
                catch { }
            }

            // Line by line parsing
            using var reader = new StringReader(contentToProcess);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var node = ParseLink(line);
                if (node != null)
                {
                    results.Add(node);
                }
            }

            return results;
        }

        public static async Task<List<NodeConfig>> FetchSubscriptionAsync(string url)
        {
            try
            {
                var content = await _httpClient.GetStringAsync(url);
                var nodes = ImportFromText(content);
                foreach (var n in nodes)
                {
                    n.SubscribeUrl = url;
                }
                return nodes;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FetchSubscription failed: {ex.Message}");
                return new List<NodeConfig>();
            }
        }

        public static NodeConfig? ParseLink(string link)
        {
            if (string.IsNullOrWhiteSpace(link)) return null;
            link = link.Trim();

            try
            {
                if (link.StartsWith("vmess://", StringComparison.OrdinalIgnoreCase))
                {
                    return ParseVmess(link);
                }
                if (link.StartsWith("vless://", StringComparison.OrdinalIgnoreCase))
                {
                    return ParseVless(link);
                }
                if (link.StartsWith("trojan://", StringComparison.OrdinalIgnoreCase))
                {
                    return ParseTrojan(link);
                }
                if (link.StartsWith("ss://", StringComparison.OrdinalIgnoreCase))
                {
                    return ParseShadowsocks(link);
                }
                if (link.StartsWith("hysteria2://", StringComparison.OrdinalIgnoreCase) ||
                    link.StartsWith("hy2://", StringComparison.OrdinalIgnoreCase))
                {
                    return ParseHysteria2(link);
                }
                if (link.StartsWith("tuic://", StringComparison.OrdinalIgnoreCase))
                {
                    return ParseTuic(link);
                }
            }
            catch { }

            return null;
        }

        private static NodeConfig? ParseVmess(string link)
        {
            string b64 = link.Substring("vmess://".Length).Trim();
            string json = TryBase64Decode(b64);
            if (string.IsNullOrWhiteSpace(json)) return null;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var node = new NodeConfig
            {
                Protocol = "vmess",
                Address = GetJsonString(root, "add") ?? "127.0.0.1",
                Port = GetJsonInt(root, "port", 443),
                UserId = GetJsonString(root, "id") ?? "",
                Security = GetJsonString(root, "scy") ?? "auto",
                Network = GetJsonString(root, "net") ?? "tcp",
                Path = GetJsonString(root, "path") ?? "",
                Host = GetJsonString(root, "host") ?? "",
                Tls = GetJsonString(root, "tls") ?? "none",
                Ps = GetJsonString(root, "ps") ?? "VMess 节点"
            };

            string? sni = GetJsonString(root, "sni");
            if (!string.IsNullOrEmpty(sni)) node.Host = sni;

            return node;
        }

        private static NodeConfig? ParseVless(string link)
        {
            var uri = new Uri(link);
            var query = HttpUtility.ParseQueryString(uri.Query);

            var node = new NodeConfig
            {
                Protocol = "vless",
                Address = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 443,
                UserId = uri.UserInfo,
                Security = "none",
                Network = query["type"] ?? "tcp",
                Path = query["path"] ?? "",
                Host = query["host"] ?? query["sni"] ?? "",
                Tls = query["security"] ?? "none",
                PublicKey = query["pbk"] ?? "",
                ShortId = query["sid"] ?? "",
                Fingerprint = query["fp"] ?? "chrome",
                Ps = !string.IsNullOrEmpty(uri.Fragment) ? Uri.UnescapeDataString(uri.Fragment.TrimStart('#')) : "VLESS 节点"
            };

            return node;
        }

        private static NodeConfig? ParseTrojan(string link)
        {
            var uri = new Uri(link);
            var query = HttpUtility.ParseQueryString(uri.Query);

            return new NodeConfig
            {
                Protocol = "trojan",
                Address = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 443,
                UserId = uri.UserInfo,
                Security = "none",
                Network = query["type"] ?? "tcp",
                Path = query["path"] ?? "",
                Host = query["sni"] ?? query["host"] ?? "",
                Tls = "tls",
                Ps = !string.IsNullOrEmpty(uri.Fragment) ? Uri.UnescapeDataString(uri.Fragment.TrimStart('#')) : "Trojan 节点"
            };
        }

        private static NodeConfig? ParseShadowsocks(string link)
        {
            string content = link.Substring("ss://".Length);
            string ps = "SS 节点";

            int hashIdx = content.IndexOf('#');
            if (hashIdx >= 0)
            {
                ps = Uri.UnescapeDataString(content.Substring(hashIdx + 1));
                content = content.Substring(0, hashIdx);
            }

            int atIdx = content.IndexOf('@');
            string method = "aes-128-gcm";
            string password = "";
            string host = "";
            int port = 8388;

            if (atIdx >= 0)
            {
                string userinfoPart = content.Substring(0, atIdx);
                string hostPortPart = content.Substring(atIdx + 1);

                string decodedUser = TryBase64Decode(userinfoPart);
                if (string.IsNullOrEmpty(decodedUser)) decodedUser = userinfoPart;

                var userTokens = decodedUser.Split(':', 2);
                if (userTokens.Length == 2)
                {
                    method = userTokens[0];
                    password = userTokens[1];
                }

                var hpTokens = hostPortPart.Split(':');
                if (hpTokens.Length == 2 && int.TryParse(hpTokens[1], out int p))
                {
                    host = hpTokens[0];
                    port = p;
                }
            }
            else
            {
                string decoded = TryBase64Decode(content);
                if (!string.IsNullOrEmpty(decoded))
                {
                    var match = Regex.Match(decoded, @"^(?<method>[^:]+):(?<pass>[^@]+)@(?<host>[^:]+):(?<port>\d+)$");
                    if (match.Success)
                    {
                        method = match.Groups["method"].Value;
                        password = match.Groups["pass"].Value;
                        host = match.Groups["host"].Value;
                        port = int.Parse(match.Groups["port"].Value);
                    }
                }
            }

            if (string.IsNullOrEmpty(host)) return null;

            return new NodeConfig
            {
                Protocol = "shadowsocks",
                Address = host,
                Port = port,
                Security = method,
                UserId = password,
                Network = "tcp",
                Ps = ps
            };
        }

        private static NodeConfig? ParseHysteria2(string link)
        {
            string clean = link.StartsWith("hysteria2://", StringComparison.OrdinalIgnoreCase)
                ? link
                : "hysteria2://" + link.Substring("hy2://".Length);

            var uri = new Uri(clean);
            var query = HttpUtility.ParseQueryString(uri.Query);

            return new NodeConfig
            {
                Protocol = "hysteria2",
                Address = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 443,
                UserId = uri.UserInfo,
                Host = query["sni"] ?? "",
                Tls = "tls",
                Network = "quic",
                Ps = !string.IsNullOrEmpty(uri.Fragment) ? Uri.UnescapeDataString(uri.Fragment.TrimStart('#')) : "Hysteria 2 节点"
            };
        }

        private static NodeConfig? ParseTuic(string link)
        {
            var uri = new Uri(link);
            var query = HttpUtility.ParseQueryString(uri.Query);

            return new NodeConfig
            {
                Protocol = "tuic",
                Address = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 443,
                UserId = uri.UserInfo,
                Host = query["sni"] ?? "",
                Tls = "tls",
                Network = "quic",
                Ps = !string.IsNullOrEmpty(uri.Fragment) ? Uri.UnescapeDataString(uri.Fragment.TrimStart('#')) : "TUIC 节点"
            };
        }

        public static string ExportLink(NodeConfig node)
        {
            switch (node.Protocol.ToLowerInvariant())
            {
                case "vmess":
                    var vmessObj = new Dictionary<string, object>
                    {
                        ["v"] = "2",
                        ["ps"] = node.Ps,
                        ["add"] = node.Address,
                        ["port"] = node.Port,
                        ["id"] = node.UserId,
                        ["aid"] = "0",
                        ["scy"] = string.IsNullOrEmpty(node.Security) ? "auto" : node.Security,
                        ["net"] = node.Network,
                        ["type"] = "none",
                        ["host"] = node.Host,
                        ["path"] = node.Path,
                        ["tls"] = node.Tls,
                        ["sni"] = node.Host
                    };
                    string vmessJson = JsonSerializer.Serialize(vmessObj);
                    return "vmess://" + Convert.ToBase64String(Encoding.UTF8.GetBytes(vmessJson));

                case "vless":
                    var qVless = new List<string>
                    {
                        $"type={node.Network}",
                        $"security={node.Tls}"
                    };
                    if (!string.IsNullOrEmpty(node.Path)) qVless.Add($"path={Uri.EscapeDataString(node.Path)}");
                    if (!string.IsNullOrEmpty(node.Host)) qVless.Add($"sni={node.Host}");
                    if (!string.IsNullOrEmpty(node.PublicKey)) qVless.Add($"pbk={node.PublicKey}");
                    if (!string.IsNullOrEmpty(node.ShortId)) qVless.Add($"sid={node.ShortId}");
                    if (!string.IsNullOrEmpty(node.Fingerprint)) qVless.Add($"fp={node.Fingerprint}");
                    return $"vless://{node.UserId}@{node.Address}:{node.Port}?{string.Join("&", qVless)}#{Uri.EscapeDataString(node.Ps)}";

                case "trojan":
                    var qTrojan = new List<string>
                    {
                        $"type={node.Network}",
                        $"security={node.Tls}"
                    };
                    if (!string.IsNullOrEmpty(node.Host)) qTrojan.Add($"sni={node.Host}");
                    return $"trojan://{node.UserId}@{node.Address}:{node.Port}?{string.Join("&", qTrojan)}#{Uri.EscapeDataString(node.Ps)}";

                case "shadowsocks":
                    string userinfo = $"{node.Security}:{node.UserId}";
                    string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(userinfo));
                    return $"ss://{b64}@{node.Address}:{node.Port}#{Uri.EscapeDataString(node.Ps)}";

                case "hysteria2":
                case "hy2":
                    return $"hysteria2://{node.UserId}@{node.Address}:{node.Port}?sni={node.Host}#{Uri.EscapeDataString(node.Ps)}";

                case "tuic":
                    return $"tuic://{node.UserId}@{node.Address}:{node.Port}?sni={node.Host}#{Uri.EscapeDataString(node.Ps)}";

                default:
                    return $"{node.Protocol}://{node.Address}:{node.Port}#{Uri.EscapeDataString(node.Ps)}";
            }
        }

        private static List<NodeConfig> TryParseJsonArray(string json)
        {
            var list = new List<NodeConfig>();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return list;

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var n = new NodeConfig
                {
                    Protocol = GetJsonString(item, "protocol") ?? "vmess",
                    Address = GetJsonString(item, "address") ?? "127.0.0.1",
                    Port = GetJsonInt(item, "port", 443),
                    UserId = GetJsonString(item, "id") ?? GetJsonString(item, "uuid") ?? GetJsonString(item, "password") ?? "",
                    Security = GetJsonString(item, "security") ?? "auto",
                    Network = GetJsonString(item, "network") ?? "tcp",
                    Path = GetJsonString(item, "path") ?? "",
                    Host = GetJsonString(item, "host") ?? GetJsonString(item, "sni") ?? "",
                    Tls = GetJsonString(item, "tls") ?? "none",
                    Ps = GetJsonString(item, "ps") ?? "节点"
                };
                list.Add(n);
            }
            return list;
        }

        private static string TryBase64Decode(string b64)
        {
            try
            {
                b64 = b64.Trim().Replace('-', '+').Replace('_', '/');
                int mod = b64.Length % 4;
                if (mod > 0) b64 += new string('=', 4 - mod);
                byte[] bytes = Convert.FromBase64String(b64);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return "";
            }
        }

        private static string? GetJsonString(JsonElement el, string prop)
        {
            if (el.TryGetProperty(prop, out var v))
            {
                if (v.ValueKind == JsonValueKind.String) return v.GetString();
                return v.ToString();
            }
            return null;
        }

        private static int GetJsonInt(JsonElement el, string prop, int defaultVal)
        {
            if (el.TryGetProperty(prop, out var v))
            {
                if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n)) return n;
                if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out int s)) return s;
            }
            return defaultVal;
        }
    }
}
