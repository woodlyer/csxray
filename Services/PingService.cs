using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CsXray.Models;

namespace CsXray.Services
{
    public class PingService
    {
        public async Task<int> TestNodeLatencyAsync(NodeConfig node, int timeoutMs = 2500)
        {
            if (string.IsNullOrWhiteSpace(node.Address) || node.Port <= 0)
            {
                node.Delay = -1;
                return -1;
            }

            var sw = Stopwatch.StartNew();
            using var cts = new CancellationTokenSource(timeoutMs);

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(node.Address, node.Port, cts.Token);
                sw.Stop();
                int ms = (int)sw.ElapsedMilliseconds;
                node.Delay = ms;
                return ms;
            }
            catch
            {
                sw.Stop();
                node.Delay = -1;
                return -1;
            }
        }

        public async Task TestAllNodesAsync(IEnumerable<NodeConfig> nodes, Action<NodeConfig>? onNodeTested = null)
        {
            using var semaphore = new SemaphoreSlim(10, 10); // max 10 concurrent connections
            var tasks = new List<Task>();

            foreach (var node in nodes)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        await TestNodeLatencyAsync(node);
                        onNodeTested?.Invoke(node);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
        }
    }
}
