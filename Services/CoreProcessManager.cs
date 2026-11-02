using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using CsXray.Models;

namespace CsXray.Services
{
    public class CoreProcessManager
    {
        private Process? _process;
        private IntPtr _jobHandle = IntPtr.Zero;
        private readonly object _lock = new();

        public event Action<string>? LogReceived;
        public event Action<bool>? StatusChanged;

        public bool IsRunning
        {
            get
            {
                lock (_lock)
                {
                    return _process != null && !_process.HasExited;
                }
            }
        }

        public CoreProcessManager()
        {
            InitJobObject();
        }

        private void InitJobObject()
        {
            try
            {
                _jobHandle = CreateJobObject(IntPtr.Zero, null);
                var info = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                {
                    LimitFlags = 0x2000 // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                };
                var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
                {
                    BasicLimitInformation = info
                };

                int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
                try
                {
                    Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);
                    SetInformationJobObject(_jobHandle, 9 /* JobObjectExtendedLimitInformation */, extendedInfoPtr, (uint)length);
                }
                finally
                {
                    Marshal.FreeHGlobal(extendedInfoPtr);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"JobObject init failed: {ex.Message}");
            }
        }

        public bool Start(NodeConfig node, AppSettings settings)
        {
            lock (_lock)
            {
                Stop();

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string coreDir = Path.Combine(baseDir, "xray-core");
                string exePath = Path.Combine(coreDir, "xray.exe");

                if (!File.Exists(exePath))
                {
                    LogReceived?.Invoke($"[错误] 未找到核心程序: {exePath}");
                    return false;
                }

                try
                {
                    string jsonConfig = ConfigGenerator.Generate(node, settings);
                    string configPath = Path.Combine(coreDir, "config.json");
                    File.WriteAllText(configPath, jsonConfig, Encoding.UTF8);

                    var psi = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = "run -c config.json",
                        WorkingDirectory = coreDir,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    _process = new Process { StartInfo = psi, EnableRaisingEvents = true };

                    _process.OutputDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data)) LogReceived?.Invoke(e.Data);
                    };

                    _process.ErrorDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data)) LogReceived?.Invoke(e.Data);
                    };

                    _process.Exited += (s, e) =>
                    {
                        LogReceived?.Invoke("[提示] Xray 进程已退出");
                        StatusChanged?.Invoke(false);
                    };

                    _process.Start();

                    if (_jobHandle != IntPtr.Zero)
                    {
                        AssignProcessToJobObject(_jobHandle, _process.Handle);
                    }

                    _process.BeginOutputReadLine();
                    _process.BeginErrorReadLine();

                    LogReceived?.Invoke($"[启动] 成功启动 Xray 核心 (节点: {node.Ps} - {node.Address}:{node.Port})");
                    StatusChanged?.Invoke(true);
                    return true;
                }
                catch (Exception ex)
                {
                    LogReceived?.Invoke($"[异常] 启动核心失败: {ex.Message}");
                    StatusChanged?.Invoke(false);
                    return false;
                }
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (_process != null)
                {
                    try
                    {
                        if (!_process.HasExited)
                        {
                            _process.Kill(true);
                            _process.WaitForExit(1000);
                        }
                    }
                    catch { }
                    finally
                    {
                        _process.Dispose();
                        _process = null;
                    }
                    LogReceived?.Invoke("[停止] Xray 服务已停止");
                    StatusChanged?.Invoke(false);
                }
            }
        }

        public bool Restart(NodeConfig node, AppSettings settings)
        {
            return Start(node, settings);
        }

        #region P/Invoke JobObject
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

        [DllImport("kernel32.dll")]
        private static extern bool SetInformationJobObject(IntPtr hJob, int JobObjectInfoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryLimit;
            public UIntPtr PeakJobMemoryLimit;
        }
        #endregion
    }
}
