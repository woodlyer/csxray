using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace CsXray.Models
{
    public class NodeConfig : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString("N");
        private string _protocol = "vmess";
        private string _address = "127.0.0.1";
        private int _port = 443;
        private string _security = "auto";
        private string _network = "tcp";
        private string _path = "";
        private string _host = "";
        private string _userId = "";
        private string _tls = "none";
        private string _publicKey = "";
        private string _shortId = "";
        private string _fingerprint = "chrome";
        private string _ps = "新节点";
        private int _delay = -2;
        private string _status = "";
        private bool _isActive = false;
        private bool _autoConnect = true;
        private string? _subscribeUrl;

        public string Id
        {
            get => _id;
            set => SetField(ref _id, value);
        }

        public string Protocol
        {
            get => _protocol;
            set => SetField(ref _protocol, value);
        }

        public string Address
        {
            get => _address;
            set => SetField(ref _address, value);
        }

        public int Port
        {
            get => _port;
            set => SetField(ref _port, value);
        }

        public string Security
        {
            get => _security;
            set => SetField(ref _security, value);
        }

        public string Network
        {
            get => _network;
            set => SetField(ref _network, value);
        }

        public string Path
        {
            get => _path;
            set => SetField(ref _path, value);
        }

        public string Host
        {
            get => _host;
            set => SetField(ref _host, value);
        }

        public string UserId
        {
            get => _userId;
            set => SetField(ref _userId, value);
        }

        public string Tls
        {
            get => _tls;
            set => SetField(ref _tls, value);
        }

        public string PublicKey
        {
            get => _publicKey;
            set => SetField(ref _publicKey, value);
        }

        public string ShortId
        {
            get => _shortId;
            set => SetField(ref _shortId, value);
        }

        public string Fingerprint
        {
            get => _fingerprint;
            set => SetField(ref _fingerprint, value);
        }

        public string Ps
        {
            get => _ps;
            set => SetField(ref _ps, value);
        }

        public int Delay
        {
            get => _delay;
            set
            {
                if (SetField(ref _delay, value))
                {
                    OnPropertyChanged(nameof(DelayText));
                }
            }
        }

        [JsonIgnore]
        public string DelayText
        {
            get
            {
                if (_delay == -2) return "-";
                if (_delay == -1) return "超时";
                return $"{_delay} ms";
            }
        }

        [JsonIgnore]
        public string Status
        {
            get => _status;
            set => SetField(ref _status, value);
        }

        [JsonIgnore]
        public bool IsActive
        {
            get => _isActive;
            set => SetField(ref _isActive, value);
        }

        public bool AutoConnect
        {
            get => _autoConnect;
            set => SetField(ref _autoConnect, value);
        }

        public string? SubscribeUrl
        {
            get => _subscribeUrl;
            set => SetField(ref _subscribeUrl, value);
        }

        public NodeConfig Clone()
        {
            return (NodeConfig)this.MemberwiseClone();
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
