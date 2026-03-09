using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace UotanToolbox.Common.Devices
{
    public class HdcTransport : IDeviceTransport
    {
        public TransportType Type => TransportType.Hdc;

        public async Task<IEnumerable<DeviceInfo>> ProbeAsync(CancellationToken cancel = default)
        {
            string output = await CallExternalProgram.HDC("list targets");
            var ids = StringHelper.HDCDevices(output);
            return ids.Select(id => new DeviceInfo(id, TransportType.Hdc));
        }

        public Task<string> RunAsync(DeviceInfo device, string command, CancellationToken cancel = default)
            => CallExternalProgram.HDC($"-t {device.Id} {command}");

        public Task<bool> ClaimAsync(DeviceInfo device) => Task.FromResult(true);
        public Task ReleaseAsync(DeviceInfo device) => Task.CompletedTask;
    }
}