using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NRuuviTag.Rest;
using Spectre.Console.Cli;

namespace NRuuviTag.Cli.Commands {

    /// <summary>
    /// <see cref="CommandApp"/> command for listening to RuuviTag broadcasts without forwarding 
    /// them to an MQTT broker.
    /// </summary>
    public class PublishRestCommand : AsyncCommand<PublishRestCommandSettings> {

        /// <summary>
        /// The <see cref="IRuuviTagListener"/> to listen to broadcasts with.
        /// </summary>
        private readonly IRuuviTagListener _listener;

        /// <summary>
        /// The known RuuviTag devices.
        /// </summary>
        private readonly IOptionsMonitor<DeviceCollection> _devices;

        /// <summary>
        /// The <see cref="IHostApplicationLifetime"/> for the .NET host application.
        /// </summary>
        private readonly IHostApplicationLifetime _appLifetime;

        /// <summary>
        /// The <see cref="ILoggerFactory"/> for the application.
        /// </summary>
        private readonly ILoggerFactory _loggerFactory;


        /// <summary>
        /// Creates a new <see cref="PublishMqttCommand"/> object.
        /// </summary>
        /// <param name="listener">
        ///   The <see cref="IRuuviTagListener"/> to listen to broadcasts with.
        /// </param>
        /// <param name="devices">
        ///   The known RuuviTag devices.
        /// </param>
        /// <param name="appLifetime">
        ///   The <see cref="IHostApplicationLifetime"/> for the .NET host application.
        /// </param>
        /// <param name="loggerFactory">
        ///   The <see cref="ILoggerFactory"/> for the application.
        /// </param>
        public PublishRestCommand(
            IRuuviTagListener listener,
            IOptionsMonitor<DeviceCollection> devices,
            IHostApplicationLifetime appLifetime,
            ILoggerFactory loggerFactory
        ) {
            _listener = listener;
            _devices = devices;
            _loggerFactory = loggerFactory;
            _appLifetime = appLifetime;
        }


        /// <inheritdoc/>
        public override async Task<int> ExecuteAsync(CommandContext context, PublishRestCommandSettings settings) {
            if (!_appLifetime.ApplicationStarted.IsCancellationRequested) {
                try { await Task.Delay(-1, _appLifetime.ApplicationStarted).ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }

            IEnumerable<Device> devices = Array.Empty<Device>();

            void UpdateDevices(DeviceCollection? devicesFromConfig) {
                lock (this) {
                    devices = devicesFromConfig?.GetDevices() ?? Array.Empty<Device>();
                }
            }

            UpdateDevices(_devices.CurrentValue);

            var agentOptions = new RestAgentOptions() {
                EndpointUrl = settings.EndpointUrl!,
                SampleRate = settings.SampleRate,
                KnownDevicesOnly = settings.KnownDevicesOnly,
                TrustSsl = settings.TrustSsl,
                AverageInterval = settings.AverageInterval,
                GetDeviceInfo = addr => {
                    lock (this) {
                        return devices.FirstOrDefault(x => string.Equals(addr, x.MacAddress, StringComparison.OrdinalIgnoreCase));
                    }
                }
            };

            var agent = new RestAgent(_listener, agentOptions, _loggerFactory.CreateLogger<RestAgent>());

            using (_devices.OnChange(newDevices => UpdateDevices(newDevices)))
            using (var ctSource = CancellationTokenSource.CreateLinkedTokenSource(_appLifetime.ApplicationStopped, _appLifetime.ApplicationStopping)) {
                try {
                    await agent.RunAsync(ctSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
            }

            return 0;
        }
    }


    /// <summary>
    /// Settings for <see cref="PublishRestCommand"/>.
    /// </summary>
    public class PublishRestCommandSettings : CommandSettings {

        [CommandArgument(0, "<API_ENDPOINT_URL>")]
        [Description("Specifies the API endpoint URL the device samples are posted.")]
        public string? EndpointUrl { get; set; }

        [CommandOption("--sample-rate <INTERVAL>")]
        [Description("Limits the RuuviTag sample rate to the specified number of seconds. Only the most-recent reading for each RuuviTag device will be included in the next API endpoint batch publish. If not specified, all observed samples will be send to the API endpoint.")]
        public int SampleRate { get; set; }

        [CommandOption("--average-interval <INTERVAL>")]
        [DefaultValue(60)]
        [Description("Specifies time in seconds to collect samples, before calculating an average value and then will be published to the API endpoint.")]
        public int AverageInterval { get; set; }

        [CommandOption("--trust-ssl")]
        [Description("Specifies if to always trust endpoint SSL certificates.")]
        public bool TrustSsl { get; set; }

        [CommandOption("--known-devices")]
        [Description("Specifies if only samples from pre-registered devices should be observed.")]
        public bool KnownDevicesOnly { get; set; }

    }

}
