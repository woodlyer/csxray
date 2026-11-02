using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CsXray.Models;
using CsXray.Services;

namespace CsXray.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly StorageService _storageService;
        private readonly CoreProcessManager _coreManager;
        private readonly SystemProxyService _proxyService;
        private readonly PingService _pingService;
        private readonly Dispatcher _dispatcher;

        private AppSettings _settings;
        private NodeConfig? _selectedNode;
        private NodeConfig? _activeNode;
        private string _proxyMode = "pac";
        private bool _isRunning = false;
        private readonly StringBuilder _logBuilder = new();
        private string _logText = "";
        private bool _isTestingSpeed = false;

        public ObservableCollection<NodeConfig> Nodes { get; } = new();

        public NodeConfig? SelectedNode
        {
            get => _selectedNode;
            set => SetField(ref _selectedNode, value);
        }

        public NodeConfig? ActiveNode
        {
            get => _activeNode;
            set
            {
                if (_activeNode != null) _activeNode.IsActive = false;
                _activeNode = value;
                if (_activeNode != null) _activeNode.IsActive = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public string ProxyMode
        {
            get => _proxyMode;
            set
            {
                if (SetField(ref _proxyMode, value))
                {
                    _settings.ProxyMode = value;
                    _proxyService.SetMode(value, _settings.HttpPort, _settings.SocksPort, _settings.PacPort);
                    _storageService.Save(_settings);
                    OnPropertyChanged(nameof(IsDirectMode));
                    OnPropertyChanged(nameof(IsProxyMode));
                    OnPropertyChanged(nameof(IsPacMode));
                }
            }
        }

        public bool IsDirectMode
        {
            get => _proxyMode == "direct";
            set { if (value) ProxyMode = "direct"; }
        }

        public bool IsProxyMode
        {
            get => _proxyMode == "proxy";
            set { if (value) ProxyMode = "proxy"; }
        }

        public bool IsPacMode
        {
            get => _proxyMode == "pac";
            set { if (value) ProxyMode = "pac"; }
        }

        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                if (SetField(ref _isRunning, value))
                {
                    OnPropertyChanged(nameof(ToggleServiceButtonText));
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public string ToggleServiceButtonText => _isRunning ? "停止服务" : "启动服务";

        public string StatusText
        {
            get
            {
                if (!_isRunning) return "服务未启动";
                if (_activeNode != null) return $"活动服务器: {_activeNode.Ps} ({_activeNode.Address}:{_activeNode.Port})";
                return "服务运行中 (未选定服务器)";
            }
        }

        public string LogText
        {
            get => _logText;
            set => SetField(ref _logText, value);
        }

        public bool IsTestingSpeed
        {
            get => _isTestingSpeed;
            set => SetField(ref _isTestingSpeed, value);
        }

        public MainViewModel()
        {
            _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            _storageService = new StorageService();
            _coreManager = new CoreProcessManager();
            _proxyService = new SystemProxyService();
            _pingService = new PingService();

            _coreManager.LogReceived += AppendLog;
            _coreManager.StatusChanged += running =>
            {
                _dispatcher.Invoke(() => IsRunning = running);
            };

            _settings = _storageService.Load();
            _proxyMode = _settings.ProxyMode;

            foreach (var node in _settings.Nodes)
            {
                Nodes.Add(node);
            }

            if (!string.IsNullOrEmpty(_settings.ActiveNodeId))
            {
                var found = Nodes.FirstOrDefault(n => n.Id == _settings.ActiveNodeId);
                if (found != null)
                {
                    ActiveNode = found;
                }
            }

            if (ActiveNode == null && Nodes.Count > 0)
            {
                ActiveNode = Nodes[0];
            }
        }

        public void AppendLog(string message)
        {
            _dispatcher.Invoke(() =>
            {
                string time = DateTime.Now.ToString("HH:mm:ss");
                _logBuilder.AppendLine($"[{time}] {message}");
                if (_logBuilder.Length > 50000)
                {
                    _logBuilder.Remove(0, 10000);
                }
                LogText = _logBuilder.ToString();
            });
        }

        public void ClearLog()
        {
            _logBuilder.Clear();
            LogText = "";
        }

        public void ToggleService()
        {
            if (_isRunning)
            {
                _coreManager.Stop();
            }
            else
            {
                if (ActiveNode == null)
                {
                    if (Nodes.Count > 0) ActiveNode = Nodes[0];
                    else
                    {
                        MessageBox.Show("请先添加或导入代理服务器节点！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }
                StartService(ActiveNode!);
            }
        }

        public void StartService(NodeConfig node)
        {
            ActiveNode = node;
            _settings.ActiveNodeId = node.Id;
            SaveData();

            bool ok = _coreManager.Start(node, _settings);
            if (ok)
            {
                _proxyService.SetMode(_proxyMode, _settings.HttpPort, _settings.SocksPort, _settings.PacPort);
            }
        }

        public void SetAsActive(NodeConfig node)
        {
            StartService(node);
        }

        public async Task TestAllSpeedsAsync()
        {
            if (IsTestingSpeed) return;
            IsTestingSpeed = true;
            AppendLog("[测速] 开始并发检测所有服务器延迟...");

            try
            {
                await _pingService.TestAllNodesAsync(Nodes);
                AppendLog("[测速] 所有服务器延迟检测完成！");
            }
            finally
            {
                IsTestingSpeed = false;
            }
        }

        public async Task TestAndConnectFastestAsync()
        {
            if (Nodes.Count == 0)
            {
                MessageBox.Show("当前列表无服务器节点，无法检测！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            AppendLog("[自动选择] 开始检测所有节点并切换到最快服务器...");
            await TestAllSpeedsAsync();

            var fastest = Nodes
                .Where(n => n.AutoConnect && n.Delay > 0)
                .OrderBy(n => n.Delay)
                .FirstOrDefault();

            if (fastest != null)
            {
                AppendLog($"[自动选择] 最快节点: {fastest.Ps} (延迟: {fastest.Delay}ms)，正在自动连接...");
                StartService(fastest);
            }
            else
            {
                AppendLog("[自动选择] 未检测到任何可用服务器！");
            }
        }

        public void ImportFromClipboard()
        {
            try
            {
                string text = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(text))
                {
                    MessageBox.Show("剪贴板内容为空！请先复制节点分享链接或订阅地址。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                text = text.Trim();

                // If user pasted a single HTTP/HTTPS subscription URL
                if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    AppendLog($"[订阅] 检测到订阅链接: {text}，正在下载更新...");
                    Task.Run(async () =>
                    {
                        var fetched = await LinkParser.FetchSubscriptionAsync(text);
                        _dispatcher.Invoke(() =>
                        {
                            if (fetched.Count > 0)
                            {
                                foreach (var f in fetched) Nodes.Add(f);
                                if (!_settings.SubscribeUrls.Contains(text)) _settings.SubscribeUrls.Add(text);
                                SaveData();
                                AppendLog($"[订阅] 成功导入 {fetched.Count} 个服务器节点！");
                                MessageBox.Show($"已成功从订阅导入 {fetched.Count} 个服务器！", "导入成功", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else
                            {
                                AppendLog("[订阅] 无法获取订阅内容或未解析出有效节点！");
                                MessageBox.Show("无法从订阅链接解析出有效节点，请检查网络或链接是否有效。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        });
                    });
                    return;
                }

                var nodes = LinkParser.ImportFromText(text);
                if (nodes.Count > 0)
                {
                    foreach (var n in nodes) Nodes.Add(n);
                    SaveData();
                    AppendLog($"[导入] 成功导入 {nodes.Count} 个服务器节点！");
                    MessageBox.Show($"已成功导入 {nodes.Count} 个节点！", "导入成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("剪贴板中未识别出有效节点链接或订阅配置！\n支持: vmess://, vless://, trojan://, ss://, hysteria2://, tuic:// 及通用 JSON 数组。", "未识别", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void CopyShareLink(NodeConfig node)
        {
            try
            {
                string link = LinkParser.ExportLink(node);
                Clipboard.SetText(link);
                AppendLog($"[复制] 已复制节点链接: {link}");
                MessageBox.Show("节点分享链接已复制到剪贴板！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"生成分享链接失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void DeleteNode(NodeConfig node)
        {
            int idx = Nodes.IndexOf(node);
            Nodes.Remove(node);
            if (ActiveNode == node)
            {
                if (Nodes.Count > 0)
                {
                    var next = Nodes[Math.Min(idx, Nodes.Count - 1)];
                    if (_isRunning) StartService(next);
                    else ActiveNode = next;
                }
                else
                {
                    ActiveNode = null;
                    if (_isRunning) _coreManager.Stop();
                }
            }
            SaveData();
        }

        public void ClearNodes()
        {
            if (MessageBox.Show("确定要清空所有服务器列表吗？", "警告", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Nodes.Clear();
                ActiveNode = null;
                if (_isRunning) _coreManager.Stop();
                SaveData();
            }
        }

        public void MoveUp(NodeConfig node)
        {
            int idx = Nodes.IndexOf(node);
            if (idx > 0)
            {
                Nodes.Move(idx, idx - 1);
                SelectedNode = node;
                SaveData();
            }
        }

        public void MoveDown(NodeConfig node)
        {
            int idx = Nodes.IndexOf(node);
            if (idx >= 0 && idx < Nodes.Count - 1)
            {
                Nodes.Move(idx, idx + 1);
                SelectedNode = node;
                SaveData();
            }
        }

        public void MoveToTop(NodeConfig node)
        {
            int idx = Nodes.IndexOf(node);
            if (idx > 0)
            {
                Nodes.Move(idx, 0);
                SelectedNode = node;
                SaveData();
            }
        }

        public void MoveToBottom(NodeConfig node)
        {
            int idx = Nodes.IndexOf(node);
            if (idx >= 0 && idx < Nodes.Count - 1)
            {
                Nodes.Move(idx, Nodes.Count - 1);
                SelectedNode = node;
                SaveData();
            }
        }

        public void SortBySpeed()
        {
            var sorted = Nodes
                .OrderBy(n => n.Delay <= 0 ? int.MaxValue : n.Delay)
                .ToList();

            Nodes.Clear();
            foreach (var s in sorted) Nodes.Add(s);
            SaveData();
        }

        public void SaveData()
        {
            _settings.Nodes = Nodes.ToList();
            _settings.ActiveNodeId = ActiveNode?.Id;
            _storageService.Save(_settings);
        }

        public void OnExit()
        {
            _coreManager.Stop();
            _proxyService.ResetOnExit();
            SaveData();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
