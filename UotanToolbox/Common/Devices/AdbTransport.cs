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
            var result = new List<DeviceInfo>();
            var lines = output.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (line.StartsWith("List of devices attached"))
                {
                    continue;
                }

                var parts = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    continue;
                }

                var id = parts[0];
                var state = parts[1];
                var properties = new Dictionary<string, string>
                {
                    ["State"] = state
                };

                result.Add(new DeviceInfo(id, TransportType.Adb, properties));
            }

            return result;
        }

        public Task<string> RunAsync(DeviceInfo device, string command, CancellationToken cancel = default)
        {
            string args = command.TrimStart().StartsWith("-s ", System.StringComparison.Ordinal)
                ? command
                : $"-s {device.Id} {command}";
            return CallExternalProgram.ADB(args);
        }

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