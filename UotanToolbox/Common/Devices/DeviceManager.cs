using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace UotanToolbox.Common.Devices
{
    public class DeviceEventArgs : EventArgs
    {
        public DeviceInfo Device { get; }
        public DeviceEventArgs(DeviceInfo device) => Device = device;
    }

    public class DeviceManager : IDisposable
    {
        private readonly IList<IDeviceTransport> _transports;
        private readonly Dictionary<string, DeviceInfo> _cache = new();
        private bool _disposed;

        public DeviceManager(IEnumerable<IDeviceTransport> transports)
        {
            _transports = transports.ToList();
        }

        public IReadOnlyCollection<DeviceInfo> Devices => _cache.Values;

        public event EventHandler<DeviceEventArgs> DeviceAdded;
        public event EventHandler<DeviceEventArgs> DeviceRemoved;
        /// <summary>
        /// Raised when a previously-seen device is discovered again with changed details.
        /// </summary>
        public event EventHandler<DeviceEventArgs> DeviceUpdated;
        /// <summary>
        /// Raised after a scan operation completes (added and removed events have already fired).
        /// </summary>
        public event EventHandler ScanCompleted;

        public async Task ScanAsync(CancellationToken cancel = default)
        {
            var seen = new HashSet<string>();
            foreach (var tr in _transports)
            {
                var list = await tr.ProbeAsync(cancel);
                foreach (var d in list)
                {
                    seen.Add(d.Id);
                    if (!_cache.ContainsKey(d.Id))
                    {
                        _cache[d.Id] = d;
                        DeviceAdded?.Invoke(this, new DeviceEventArgs(d));
                    }
                    else
                    {
                        // existing device, check if any details changed
                        var old = _cache[d.Id];
                        if (!old.Equals(d))
                        {
                            _cache[d.Id] = d;
                            DeviceUpdated?.Invoke(this, new DeviceEventArgs(d));
                        }
                    }
                }
            }

            var removed = _cache.Keys.Except(seen).ToList();
            foreach (var id in removed)
            {
                var di = _cache[id];
                _cache.Remove(id);
                DeviceRemoved?.Invoke(this, new DeviceEventArgs(di));
            }
            // notify that the scan finished
            ScanCompleted?.Invoke(this, EventArgs.Empty);
        }

        public async Task<string> ExecuteAsync(DeviceInfo device, string cmd) =>
            await _transports.First(t => t.Type == device.Transport).RunAsync(device, cmd);

        public Task<bool> ClaimAsync(DeviceInfo d) =>
            _transports.First(t => t.Type == d.Transport).ClaimAsync(d);

        public Task ReleaseAsync(DeviceInfo d) =>
            _transports.First(t => t.Type == d.Transport).ReleaseAsync(d);

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var tr in _transports)
                {
                    if (tr is IDisposable d) d.Dispose();
                }
                _disposed = true;
            }
        }
    }
}