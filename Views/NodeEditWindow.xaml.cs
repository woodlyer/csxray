using System;
using System.Windows;
using System.Windows.Controls;
using CsXray.Models;

namespace CsXray.Views
{
    public partial class NodeEditWindow : Window
    {
        public NodeConfig Node { get; private set; }
        private readonly bool _isNew;

        public NodeEditWindow(NodeConfig? node = null)
        {
            InitializeComponent();

            _isNew = node == null;
            Node = node?.Clone() ?? new NodeConfig
            {
                Protocol = "vmess",
                Address = "127.0.0.1",
                Port = 443,
                UserId = Guid.NewGuid().ToString("D"),
                Ps = "新代理服务器"
            };

            Title = _isNew ? "winXray - 增加代理服务器节点" : "winXray - 修改代理服务器节点";
            TxtHeader.Text = _isNew ? "增加代理服务器节点" : "修改代理服务器节点";
            BtnSave.Content = _isNew ? "新 增" : "保 存";

            LoadNodeToUi();
        }

        private void LoadNodeToUi()
        {
            SetComboText(CmbProtocol, Node.Protocol);
            TxtPs.Text = Node.Ps;
            TxtAddress.Text = Node.Address;
            TxtPort.Text = Node.Port.ToString();
            TxtUserId.Text = Node.UserId;
            SetComboText(CmbNetwork, Node.Network);
            SetComboText(CmbSecurity, Node.Security);
            SetComboText(CmbTls, Node.Tls);
            TxtHost.Text = Node.Host;
            TxtPath.Text = Node.Path;
            TxtPublicKey.Text = Node.PublicKey;
            TxtShortId.Text = Node.ShortId;

            UpdateUiVisibility();
        }

        private void SetComboText(ComboBox cmb, string text)
        {
            foreach (ComboBoxItem item in cmb.Items)
            {
                if (string.Equals(item.Content?.ToString(), text, StringComparison.OrdinalIgnoreCase))
                {
                    cmb.SelectedItem = item;
                    return;
                }
            }
            if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
        }

        private string GetComboText(ComboBox cmb)
        {
            return (cmb.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        }

        private void UpdateUiVisibility()
        {
            string tls = GetComboText(CmbTls);
            PnlReality.Visibility = tls == "reality" ? Visibility.Visible : Visibility.Collapsed;

            string proto = GetComboText(CmbProtocol);
            if (proto == "shadowsocks" || proto == "trojan")
            {
                LblUserId.Text = "密码 (Password):";
            }
            else if (proto == "socks" || proto == "http")
            {
                LblUserId.Text = "用户:密码:";
            }
            else
            {
                LblUserId.Text = "UUID / 密码:";
            }
        }

        private void CmbProtocol_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateUiVisibility();
        }

        private void CmbTls_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateUiVisibility();
        }

        private void BtnGenGuid_Click(object sender, RoutedEventArgs e)
        {
            string proto = GetComboText(CmbProtocol);
            if (proto == "shadowsocks" || proto == "trojan" || proto == "hysteria2")
            {
                TxtUserId.Text = Guid.NewGuid().ToString("N").Substring(0, 16);
            }
            else
            {
                TxtUserId.Text = Guid.NewGuid().ToString("D");
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string address = TxtAddress.Text.Trim();
            if (string.IsNullOrEmpty(address))
            {
                MessageBox.Show("服务器地址不能为空！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtAddress.Focus();
                return;
            }

            if (!int.TryParse(TxtPort.Text.Trim(), out int port) || port <= 0 || port > 65535)
            {
                MessageBox.Show("端口必须为 1 ~ 65535 之间的有效数字！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtPort.Focus();
                return;
            }

            Node.Protocol = GetComboText(CmbProtocol);
            Node.Ps = string.IsNullOrWhiteSpace(TxtPs.Text) ? "未命名节点" : TxtPs.Text.Trim();
            Node.Address = address;
            Node.Port = port;
            Node.UserId = TxtUserId.Text.Trim();
            Node.Network = GetComboText(CmbNetwork);
            Node.Security = GetComboText(CmbSecurity);
            Node.Tls = GetComboText(CmbTls);
            Node.Host = TxtHost.Text.Trim();
            Node.Path = TxtPath.Text.Trim();
            Node.PublicKey = TxtPublicKey.Text.Trim();
            Node.ShortId = TxtShortId.Text.Trim();

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
