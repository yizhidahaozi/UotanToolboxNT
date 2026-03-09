using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace UotanToolbox.Common.Devices
{
    public class AdbTransport : IDeviceTransport
    {
        public TransportType Type => TransportType.Adb;

        public async Task<IEnumerable<DeviceInfo>> ProbeAsync(CancellationToken cancel = default)
        {
            string output = await CallExternalProgram.ADB("devices");
            var ids = StringHelper.ADBDevices(output);
            return ids.Select(id => new DeviceInfo(id, TransportType.Adb));
        }

        public Task<string> RunAsync(DeviceInfo device, string command, CancellationToken cancel = default)
            => CallExternalProgram.ADB($"-s {device.Id} {command}");

        public Task<bool> ClaimAsync(DeviceInfo device)
        {
            // adb does not require explicit claim; placeholder for future locking
            return Task.FromResult(true);
        }

        public Task ReleaseAsync(DeviceInfo device)
        {
            // nothing to do
            return Task.CompletedTask;
        }
    }
}