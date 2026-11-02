using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CsXray.Models;
using CsXray.ViewModels;

namespace CsXray.Views
{
    public partial class MainWindow : Window
    {
        private MainViewModel Vm => (MainViewModel)DataContext;

        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            Vm.OnExit();
            base.OnClosing(e);
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ServerDataGrid.SelectedItem is NodeConfig node)
            {
                Vm.SetAsActive(node);
            }
        }

        private void BtnToggleService_Click(object sender, RoutedEventArgs e)
        {
            Vm.ToggleService();
        }

        private async void BtnFastest_Click(object sender, RoutedEventArgs e)
        {
            await Vm.TestAndConnectFastestAsync();
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            Vm.ImportFromClipboard();
        }

        private void BtnAddNode_Click(object sender, RoutedEventArgs e)
        {
            ShowNodeEdit(null);
        }

        private void MenuSetActive_Click(object sender, RoutedEventArgs e)
        {
            if (ServerDataGrid.SelectedItem is NodeConfig node)
            {
                Vm.SetAsActive(node);
            }
        }

        private void MenuEditNode_Click(object sender, RoutedEventArgs e)
        {
            if (ServerDataGrid.SelectedItem is NodeConfig node)
            {
                ShowNodeEdit(node);
            }
            else
            {
                MessageBox.Show("请先选择一个要修改的服务器节点！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void MenuAddNode_Click(object sender, RoutedEventArgs e)
        {
            ShowNodeEdit(null);
        }

        private void ShowNodeEdit(NodeConfig? node)
        {
            var win = new NodeEditWindow(node) { Owner = this };
            if (win.ShowDialog() == true)
            {
                if (node == null)
                {
                    // Add new node
                    Vm.Nodes.Add(win.Node);
                    Vm.AppendLog($"[添加] 成功添加服务器节点: {win.Node.Ps} ({win.Node.Address}:{win.Node.Port})");
                }
                else
                {
                    // Updated existing node
                    int idx = Vm.Nodes.IndexOf(node);
                    if (idx >= 0)
                    {
                        Vm.Nodes[idx] = win.Node;
                        Vm.SelectedNode = win.Node;
                        if (Vm.ActiveNode?.Id == win.Node.Id)
                        {
                            Vm.ActiveNode = win.Node;
                            if (Vm.IsRunning) Vm.StartService(win.Node);
                        }
                    }
                    Vm.AppendLog($"[修改] 成功更新服务器节点: {win.Node.Ps}");
                }
                Vm.SaveData();
            }
        }

        private void MenuCopyLink_Click(object sender, RoutedEventArgs e)
        {
            if (ServerDataGrid.SelectedItem is NodeConfig node)
            {
                Vm.CopyShareLink(node);
            }
        }

        private async void MenuPing_Click(object sender, RoutedEventArgs e)
        {
            if (ServerDataGrid.SelectedItem is NodeConfig node)
            {
                Vm.AppendLog($"[测速] 正在检测节点: {node.Ps} ({node.Address}:{node.Port})...");
                var ping = new Services.PingService();
                int ms = await ping.TestNodeLatencyAsync(node);
                if (ms > 0)
                {
                    Vm.AppendLog($"[测速] 节点: {node.Ps} 响应速度: {ms} ms");
                }
                else
                {
                    Vm.AppendLog($"[测速] 节点: {node.Ps} 连接超时/不可用！");
                }
            }
        }

        private void MenuMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (ServerDataGrid.SelectedItem is NodeConfig node) Vm.MoveUp(node);
        }

        private void MenuMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (ServerDataGrid.SelectedItem is NodeConfig node) Vm.MoveDown(node);
        }

        private void MenuMoveTop_Click(object sender, RoutedEventArgs e)
        {
            if (ServerDataGrid.SelectedItem is NodeConfig node) Vm.MoveToTop(node);
        }

        private void MenuMoveBottom_Click(object sender, RoutedEventArgs e)
        {
            if (ServerDataGrid.SelectedItem is NodeConfig node) Vm.MoveToBottom(node);
        }

        private void MenuSortBySpeed_Click(object sender, RoutedEventArgs e)
        {
            Vm.SortBySpeed();
        }

        private void MenuImport_Click(object sender, RoutedEventArgs e)
        {
            Vm.ImportFromClipboard();
        }

        private void MenuDeleteNode_Click(object sender, RoutedEventArgs e)
        {
            if (ServerDataGrid.SelectedItem is NodeConfig node)
            {
                if (MessageBox.Show($"确定要删除节点 [{node.Ps}] 吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    Vm.DeleteNode(node);
                }
            }
        }

        private void MenuClearNodes_Click(object sender, RoutedEventArgs e)
        {
            Vm.ClearNodes();
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            Vm.ClearLog();
        }

        private void TxtLog_TextChanged(object sender, TextChangedEventArgs e)
        {
            TxtLog.ScrollToEnd();
        }
    }
}
