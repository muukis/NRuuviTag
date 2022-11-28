using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using RestSharp;
using Timer = System.Timers.Timer;

namespace NRuuviTag.Rest {

    /// <summary>
    /// Observes measurements emitted by an <see cref="IRuuviTagListener"/> and publishes them to 
    /// an Azure Event Hub.
    /// </summary>
    public class RestAgent : RuuviTagPublisher {

        /// <summary>
        /// A delegate that retrieves the device information for a sample based on the MAC address 
        /// of the device.
        /// </summary>
        private readonly Func<string, Device?>? _getDeviceInfo;

        /// <summary>
        /// The endpoint URL.
        /// </summary>
        private readonly string _endpointUrl;

        /// <summary>
        /// Trust all SSL certificates.
        /// </summary>
        private readonly bool _trustSsl;

        /// <summary>
        /// Time in seconds to collect samples before calculating average. Works only when value is set higher than zero.
        /// </summary>
        private readonly int _averageInterval;

        /// <summary>
        /// JSON serializer options for serializing message payloads.
        /// </summary>
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions() {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            // If and properties on a sample are set to null, we won't include them in the
            // serialized object we send to the event hub.
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };


        /// <summary>
        /// Creates a new <see cref="RestAgent"/> object.
        /// </summary>
        /// <param name="listener">
        ///   The <see cref="IRuuviTagListener"/> to observe for sensor readings.
        /// </param>
        /// <param name="options">
        ///   Agent options.
        /// </param>
        /// <param name="logger">
        ///   The logger for the agent.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="listener"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="options"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ValidationException">
        ///   <paramref name="options"/> fails validation.
        /// </exception>
        public RestAgent(IRuuviTagListener listener, RestAgentOptions options, ILogger<RestAgent>? logger = null) 
            : base(listener, options?.SampleRate ?? 0, BuildFilterDelegate(options!), logger) { 
            if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }

            Validator.ValidateObject(options, new ValidationContext(options), true);

            _endpointUrl = options.EndpointUrl;
            _trustSsl = options.TrustSsl;
            _averageInterval = options.AverageInterval;
            _getDeviceInfo = options.GetDeviceInfo;
        }


        /// <summary>
        /// Builds a filter delegate that can restrict listening to broadcasts from only known 
        /// devices if required.
        /// </summary>
        /// <param name="options">
        ///   The options.
        /// </param>
        /// <returns>
        ///   The filter delegate.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   <paramref name="options"/> is <see langword="null"/>.
        /// </exception>
        private static Func<string, bool> BuildFilterDelegate(RestAgentOptions options) {
            if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }

            if (!options.KnownDevicesOnly) {
                return addr => true;
            }

            var getDeviceInfo = options.GetDeviceInfo;
            return getDeviceInfo == null
                ? addr => false
                : addr => getDeviceInfo.Invoke(addr) != null;
        }


        protected override async Task RunAsync(IAsyncEnumerable<RuuviTagSample> samples, CancellationToken cancellationToken) {
            Logger.LogInformation(Resources.LogMessage_RestClientStarting);

            var options = new RestClientOptions {
                ThrowOnAnyError = true,
                MaxTimeout = 10000
            };

            if (_trustSsl) {
                options.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            }

            var avgService = new AverageSampleService();
            using var client = new RestClient(options);

            using var timer = new Timer {
                AutoReset = true, Enabled = false, Interval = Math.Max(_averageInterval, 1) * 1000
            };
            timer.Elapsed += async (_, _) => await PublishBatch(true).ConfigureAwait(false);

            async Task PublishBatch(bool timerElapsedEvent) {
                try {
                    if (avgService.SamplesCount == 0) {
                        return;
                    }

                    if (timerElapsedEvent) {
                        timer.Stop();
                    }

                    var avgSamples = avgService.GetAverageSamplesAll(true);

                    var request = new RestRequest(_endpointUrl, Method.Post);
                    request.AddJsonBody(avgSamples.ToArray());

                    var response = await client.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

                    if (response.StatusCode != HttpStatusCode.OK) {
                        throw new HttpRequestException(
                            $"Invalid endpoint response: {(int) response.StatusCode} ({response.StatusCode})");
                    }
                }
                catch (Exception e) {
                    Logger.LogError(e, Resources.LogMessage_RestPublishError);
                }
                finally {
                    if (timerElapsedEvent) {
                        timer.Start();
                    }
                }
            }

            try {
                timer.Start();
                await foreach (var sample in samples.ConfigureAwait(false)) {
                    var knownDevice = _getDeviceInfo?.Invoke(sample.MacAddress!);
                    avgService.AddSample(RuuviTagSampleExtended.Create(sample, knownDevice?.DeviceId, knownDevice?.DisplayName));
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            catch (OperationCanceledException) {
                timer.Stop();
                await PublishBatch(false).ConfigureAwait(false);
            }
            finally {
                timer.Stop();
                Logger.LogInformation(Resources.LogMessage_RestClientStopped);
            }
        }

    }
}
