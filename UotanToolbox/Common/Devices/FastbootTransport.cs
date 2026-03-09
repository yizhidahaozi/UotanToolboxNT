using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace UotanToolbox.Common.Devices
{
    public class FastbootTransport : IDeviceTransport
    {
        public TransportType Type => TransportType.Fastboot;

        public async Task<IEnumerable<DeviceInfo>> ProbeAsync(CancellationToken cancel = default)
        {
            string output = await CallExternalProgram.Fastboot("devices");
            var ids = StringHelper.FastbootDevices(output);
            return ids.Select(id => new DeviceInfo(id, TransportType.Fastboot));
        }

        public Task<string> RunAsync(DeviceInfo device, string command, CancellationToken cancel = default)
            => CallExternalProgram.Fastboot($"-s {device.Id} {command}");

        public Task<bool> ClaimAsync(DeviceInfo device)
        {
            // placeholder
            return Task.FromResult(true);
        }

        public Task ReleaseAsync(DeviceInfo device) => Task.CompletedTask;
    }
}