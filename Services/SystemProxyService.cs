using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace CsXray.Services
{
    public class SystemProxyService
    {
        private HttpListener? _pacListener;
        private bool _isPacRunning = false;
        private const string RegistrySubKey = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

        public void SetMode(string mode, int httpPort = 10809, int socksPort = 10808, int pacPort = 10807)
        {
            switch (mode.ToLowerInvariant())
            {
                case "proxy":
                case "global":
                    StopPacServer();
                    SetRegistryProxy(true, $"127.0.0.1:{httpPort}", null);
                    break;

                case "pac":
                    StartPacServer(pacPort, socksPort, httpPort);
                    SetRegistryProxy(false, null, $"http://127.0.0.1:{pacPort}/pac.txt");
                    break;

                case "direct":
                default:
                    StopPacServer();
                    SetRegistryProxy(false, null, null);
                    break;
            }

            NotifySettingsChanged();
        }

        public void ResetOnExit()
        {
            try
            {
                StopPacServer();
                SetRegistryProxy(false, null, null);
                NotifySettingsChanged();
            }
            catch { }
        }

        private void SetRegistryProxy(bool enable, string? proxyServer, string? autoConfigUrl)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistrySubKey, true);
                if (key != null)
                {
                    key.SetValue("ProxyEnable", enable ? 1 : 0, RegistryValueKind.DWord);

                    if (!string.IsNullOrEmpty(proxyServer))
                    {
                        key.SetValue("ProxyServer", proxyServer, RegistryValueKind.String);
                    }

                    if (!string.IsNullOrEmpty(autoConfigUrl))
                    {
                        key.SetValue("AutoConfigURL", autoConfigUrl, RegistryValueKind.String);
                    }
                    else
                    {
                        key.DeleteValue("AutoConfigURL", false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetRegistryProxy error: {ex.Message}");
            }
        }

        private void StartPacServer(int pacPort, int socksPort, int httpPort)
        {
            if (_isPacRunning) return;

            try
            {
                _pacListener = new HttpListener();
                _pacListener.Prefixes.Add($"http://127.0.0.1:{pacPort}/");
                _pacListener.Start();
                _isPacRunning = true;

                Task.Run(() => HandlePacRequests(socksPort, httpPort));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StartPacServer error: {ex.Message}");
            }
        }

        private async Task HandlePacRequests(int socksPort, int httpPort)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string pacPath = Path.Combine(baseDir, "pac.txt");
            string defaultPac = @"function FindProxyForURL(url, host) { return ""__PROXY__""; }";

            while (_isPacRunning && _pacListener != null && _pacListener.IsListening)
            {
                try
                {
                    var ctx = await _pacListener.GetContextAsync();
                    var req = ctx.Request;
                    var res = ctx.Response;

                    string pacTemplate = File.Exists(pacPath) ? File.ReadAllText(pacPath) : defaultPac;
                    string proxyReplacement = $"SOCKS5 127.0.0.1:{socksPort}; SOCKS 127.0.0.1:{socksPort}; PROXY 127.0.0.1:{httpPort}; DIRECT;";
                    string pacContent = pacTemplate.Replace("__PROXY__", proxyReplacement);

                    byte[] buffer = Encoding.UTF8.GetBytes(pacContent);
                    res.ContentType = "application/x-ns-proxy-autoconfig; charset=utf-8";
                    res.ContentLength64 = buffer.Length;
                    await res.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    res.OutputStream.Close();
                }
                catch
                {
                    if (!_isPacRunning) break;
                }
            }
        }

        private void StopPacServer()
        {
            _isPacRunning = false;
            try
            {
                if (_pacListener != null)
                {
                    _pacListener.Stop();
                    _pacListener.Close();
                    _pacListener = null;
                }
            }
            catch { }
        }

        private void NotifySettingsChanged()
        {
            InternetSetOption(IntPtr.Zero, 39 /* INTERNET_OPTION_SETTINGS_CHANGED */, IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, 37 /* INTERNET_OPTION_REFRESH */, IntPtr.Zero, 0);
        }

        [DllImport("wininet.dll", SetLastError = true)]
        private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);
    }
}
