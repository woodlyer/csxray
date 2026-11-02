using System;
using System.Collections.Generic;
using System.Text.Json;
using CsXray.Models;

namespace CsXray.Services
{
    public static class ConfigGenerator
    {
        public static string Generate(NodeConfig node, AppSettings settings)
        {
            var config = new Dictionary<string, object>();

            // 1. Inbounds
            var inbounds = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["tag"] = "socks",
                    ["port"] = settings.SocksPort,
                    ["listen"] = "127.0.0.1",
                    ["protocol"] = "socks",
                    ["sniffing"] = new Dictionary<string, object>
                    {
                        ["enabled"] = true,
                        ["destOverride"] = new[] { "http", "tls" },
                        ["routeOnly"] = true
                    },
                    ["settings"] = new Dictionary<string, object>
                    {
                        ["auth"] = "noauth",
                        ["udp"] = true
                    }
                },
                new Dictionary<string, object>
                {
                    ["tag"] = "http",
                    ["port"] = settings.HttpPort,
                    ["listen"] = "127.0.0.1",
                    ["protocol"] = "http",
                    ["sniffing"] = new Dictionary<string, object>
                    {
                        ["enabled"] = true,
                        ["destOverride"] = new[] { "http", "tls" },
                        ["routeOnly"] = true
                    },
                    ["settings"] = new Dictionary<string, object>()
                }
            };
            config["inbounds"] = inbounds;

            // 2. Outbounds
            var outbounds = new List<object>();

            var proxyOutbound = new Dictionary<string, object>
            {
                ["tag"] = "proxy"
            };

            string proto = node.Protocol.ToLowerInvariant();
            switch (proto)
            {
                case "vmess":
                    proxyOutbound["protocol"] = "vmess";
                    proxyOutbound["settings"] = new Dictionary<string, object>
                    {
                        ["vnext"] = new[]
                        {
                            new Dictionary<string, object>
                            {
                                ["address"] = node.Address,
                                ["port"] = node.Port,
                                ["users"] = new[]
                                {
                                    new Dictionary<string, object>
                                    {
                                        ["id"] = node.UserId,
                                        ["alterId"] = 0,
                                        ["security"] = string.IsNullOrEmpty(node.Security) ? "auto" : node.Security,
                                        ["level"] = 8
                                    }
                                }
                            }
                        }
                    };
                    break;

                case "vless":
                    proxyOutbound["protocol"] = "vless";
                    proxyOutbound["settings"] = new Dictionary<string, object>
                    {
                        ["vnext"] = new[]
                        {
                            new Dictionary<string, object>
                            {
                                ["address"] = node.Address,
                                ["port"] = node.Port,
                                ["users"] = new[]
                                {
                                    new Dictionary<string, object>
                                    {
                                        ["id"] = node.UserId,
                                        ["encryption"] = "none",
                                        ["level"] = 8
                                    }
                                }
                            }
                        }
                    };
                    break;

                case "trojan":
                    proxyOutbound["protocol"] = "trojan";
                    proxyOutbound["settings"] = new Dictionary<string, object>
                    {
                        ["servers"] = new[]
                        {
                            new Dictionary<string, object>
                            {
                                ["address"] = node.Address,
                                ["port"] = node.Port,
                                ["password"] = node.UserId,
                                ["level"] = 8
                            }
                        }
                    };
                    break;

                case "shadowsocks":
                    proxyOutbound["protocol"] = "shadowsocks";
                    proxyOutbound["settings"] = new Dictionary<string, object>
                    {
                        ["servers"] = new[]
                        {
                            new Dictionary<string, object>
                            {
                                ["address"] = node.Address,
                                ["port"] = node.Port,
                                ["method"] = string.IsNullOrEmpty(node.Security) ? "aes-128-gcm" : node.Security,
                                ["password"] = node.UserId,
                                ["level"] = 8
                            }
                        }
                    };
                    break;

                case "socks":
                    proxyOutbound["protocol"] = "socks";
                    proxyOutbound["settings"] = new Dictionary<string, object>
                    {
                        ["servers"] = new[]
                        {
                            new Dictionary<string, object>
                            {
                                ["address"] = node.Address,
                                ["port"] = node.Port
                            }
                        }
                    };
                    break;

                case "http":
                    proxyOutbound["protocol"] = "http";
                    proxyOutbound["settings"] = new Dictionary<string, object>
                    {
                        ["servers"] = new[]
                        {
                            new Dictionary<string, object>
                            {
                                ["address"] = node.Address,
                                ["port"] = node.Port
                            }
                        }
                    };
                    break;

                default:
                    proxyOutbound["protocol"] = "socks";
                    proxyOutbound["settings"] = new Dictionary<string, object>
                    {
                        ["servers"] = new[]
                        {
                            new Dictionary<string, object>
                            {
                                ["address"] = node.Address,
                                ["port"] = node.Port
                            }
                        }
                    };
                    break;
            }

            // StreamSettings
            var stream = new Dictionary<string, object>
            {
                ["network"] = string.IsNullOrEmpty(node.Network) ? "tcp" : node.Network.ToLowerInvariant()
            };

            string tlsMode = string.IsNullOrEmpty(node.Tls) ? "none" : node.Tls.ToLowerInvariant();
            if (tlsMode == "tls")
            {
                stream["security"] = "tls";
                var tlsSettings = new Dictionary<string, object>
                {
                    ["allowInsecure"] = true
                };
                if (!string.IsNullOrEmpty(node.Host))
                {
                    tlsSettings["serverName"] = node.Host;
                }
                stream["tlsSettings"] = tlsSettings;
            }
            else if (tlsMode == "reality")
            {
                stream["security"] = "reality";
                var realitySettings = new Dictionary<string, object>
                {
                    ["show"] = false,
                    ["fingerprint"] = string.IsNullOrEmpty(node.Fingerprint) ? "chrome" : node.Fingerprint,
                    ["serverName"] = string.IsNullOrEmpty(node.Host) ? node.Address : node.Host,
                    ["publicKey"] = node.PublicKey,
                    ["shortId"] = node.ShortId,
                    ["spiderX"] = ""
                };
                stream["realitySettings"] = realitySettings;
            }
            else
            {
                stream["security"] = "none";
            }

            // Transport details
            if (node.Network.Equals("ws", StringComparison.OrdinalIgnoreCase))
            {
                var ws = new Dictionary<string, object>
                {
                    ["path"] = string.IsNullOrEmpty(node.Path) ? "/" : node.Path
                };
                if (!string.IsNullOrEmpty(node.Host))
                {
                    ws["headers"] = new Dictionary<string, string> { ["Host"] = node.Host };
                }
                stream["wsSettings"] = ws;
            }
            else if (node.Network.Equals("grpc", StringComparison.OrdinalIgnoreCase))
            {
                stream["grpcSettings"] = new Dictionary<string, object>
                {
                    ["serviceName"] = node.Path ?? "",
                    ["multiMode"] = true
                };
            }
            else if (node.Network.Equals("httpupgrade", StringComparison.OrdinalIgnoreCase))
            {
                var hu = new Dictionary<string, object>
                {
                    ["path"] = string.IsNullOrEmpty(node.Path) ? "/" : node.Path
                };
                if (!string.IsNullOrEmpty(node.Host))
                {
                    hu["host"] = node.Host;
                }
                stream["httpupgradeSettings"] = hu;
            }

            proxyOutbound["streamSettings"] = stream;
            outbounds.Add(proxyOutbound);

            // Direct outbound
            outbounds.Add(new Dictionary<string, object>
            {
                ["tag"] = "direct",
                ["protocol"] = "freedom",
                ["settings"] = new Dictionary<string, object>()
            });

            // Block outbound
            outbounds.Add(new Dictionary<string, object>
            {
                ["tag"] = "block",
                ["protocol"] = "blackhole",
                ["settings"] = new Dictionary<string, object>
                {
                    ["response"] = new Dictionary<string, string> { ["type"] = "http" }
                }
            });

            config["outbounds"] = outbounds;

            // 3. Routing
            config["routing"] = new Dictionary<string, object>
            {
                ["domainStrategy"] = "IPIfNonMatch",
                ["rules"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["type"] = "field",
                        ["outboundTag"] = "direct",
                        ["domain"] = new[] { "geosite:cn" }
                    },
                    new Dictionary<string, object>
                    {
                        ["type"] = "field",
                        ["outboundTag"] = "direct",
                        ["ip"] = new[] { "geoip:cn", "geoip:private" }
                    }
                }
            };

            // 4. Log
            config["log"] = new Dictionary<string, string>
            {
                ["loglevel"] = "warning"
            };

            return JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
