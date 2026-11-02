# CsXray - 基于 C# (.NET 9 / WPF) 实现的通用代理客户端

CsXray 是 WinXray 的完整 C# 重构版本，基于现代 **.NET 9 + WPF (MVVM)** 架构开发，彻底告别旧版脚本语言的底层兼容性问题。

---

## ✨ 核心特性

1. **协议支持全面**：
   - 完整支持 **VMess、VLESS、Trojan、Shadowsocks、Hysteria 2、TUIC、Socks5、HTTP**。
   - 完整支持 **Reality**（公钥 pbk、ShortId sid、指纹 fp）与标准 **TLS**。
   - 传输层支持 **TCP、WebSocket、gRPC、HTTPUpgrade、SplitHTTP、H2、QUIC**。

2. **便捷分享与导入导出**：
   - 支持主流分享格式：`vmess://`、`vless://`、`trojan://`、`ss://`、`hysteria2://`、`tuic://`。
   - 剪贴板批量导入：支持多行分享链接自动剔除杂质导入，支持标准 JSON 数组导入，支持单个 HTTP/HTTPS 订阅源自动下载拉取。
   - 一键生成并复制当前节点的分享链接。

3. **内核进程托管（防残留）**：
   - 采用 Windows 核心 **JobObject（作业对象）** 机制管理 `xray.exe`，保证主程序退出或崩溃时，系统内核 100% 自动清理终止后台代理核心，绝不残留僵尸进程。
   - 异步重定向标准输出与错误输出，在主界面下方控制台实时查看 Xray 运行日志。

4. **系统代理模式切换**：
   - **不使用代理 (Direct)**：清除系统代理设置。
   - **全局代理 (Proxy)**：通过 WinINet API 无需重启即时生效，路由设置国内 IP/域名直连，海外经由代理。
   - **PAC代理 (PAC)**：内置轻量本地 PAC HTTP 服务，提供智能分流规则。
   - **安全退出机制**：程序退出时自动重置系统代理，绝不会出现关闭后网页打不开的情况。

5. **智能测速与并发检测**：
   - 采用并发非阻塞异步 TCP 握手测速（`TcpClient.ConnectAsync` + `Task.WhenAll`），秒级完成所有服务器延迟检测。
   - 提供「检测并连接最快服务器」功能，测速后自动将最快节点设为活动节点并平滑重启核心。

6. **右键菜单与节点管理**：
   - **增加节点**：弹出标准新增窗口，自带 UUID/密码生成器。
   - **修改节点**：精准读取并编辑已有节点配置。
   - **列表排序**：支持上移、下移、置顶、置底及按响应速度智能排序。
   - **双击激活**：列表行双击即刻切换至目标服务器。

---

## 📁 目录结构

```text
D:\tmp\csxray\
├── Models/                     # 数据模型
│   ├── NodeConfig.cs           # 节点实体（含 INotifyPropertyChanged 与格式转换）
│   └── AppSettings.cs          # 程序配置模型（端口、代理模式、节点列表等）
├── Services/                   # 核心服务层
│   ├── ConfigGenerator.cs      # Xray JSON 配置自动组装生成器
│   ├── CoreProcessManager.cs   # Xray 进程管理器（含 JobObject 防残留机制）
│   ├── SystemProxyService.cs   # WinINet 系统代理控制与本地 PAC 服务
│   ├── LinkParser.cs           # 分享链接 / 订阅解析与导出
│   ├── PingService.cs          # 高并发异步 Tcping 测速服务
│   └── StorageService.cs       # 本地配置保存与读取 (config/settings.json)
├── ViewModels/                 # MVVM 业务模型
│   └── MainViewModel.cs        # 界面双向绑定、状态流转与命令逻辑
├── Views/                      # WPF 视图层
│   ├── MainWindow.xaml         # 主界面（顶部控制条、服务器列表、控制台日志、状态栏）
│   ├── MainWindow.xaml.cs
│   ├── NodeEditWindow.xaml     # 节点增加 / 修改弹窗
│   └── NodeEditWindow.xaml.cs
├── xray-core/                  # Xray 核心运行目录 (存放官方发布的 xray.exe, geoip.dat, geosite.dat 等二进制)
├── pac.txt                     # 本地 PAC 规则模板
└── publish/                    # 单文件便携版发布输出目录 (CsXray.exe)
```

> **提示**：二进制核心文件（`xray.exe` 及相关 `.dat` 资源）已加入 `.gitignore` 避免污染源码仓库。首次运行前只需将 Xray-core 官方发布的二进制解压到 `xray-core/` 目录即可。

---

## 🚀 编译与运行

### 1. 调试运行
```bash
cd D:\tmp\csxray
dotnet run
```

### 2. 生成发布包 (单文件便携版)
```bash
cd D:\tmp\csxray
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false -o ./publish
```
输出的可执行文件位于 `D:\tmp\csxray\publish\CsXray.exe`，直接双击运行即可。
