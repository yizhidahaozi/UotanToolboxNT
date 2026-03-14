using System;
using System.Collections.Generic;
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
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                if (line.StartsWith("List of devices attached"))
                {
                    continue;
                }

                // Ignore adb daemon startup diagnostics, e.g.:
                // "* daemon not running; starting now at tcp:5037"
                // "* daemon started successfully"
                if (trimmed.StartsWith("*", StringComparison.Ordinal))
                {
                    continue;
                }

                var parts = trimmed.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
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

        public Task<string> RunAsync(DeviceInfo device, string command, CancellationToken cancel = default, Action<string>? outputCallback = null)
        {
            string args = command.TrimStart().StartsWith("-s ", System.StringComparison.Ordinal)
                ? command
                : $"-s {device.Id} {command}";
            return CallExternalProgram.ADB(args, outputCallback);
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