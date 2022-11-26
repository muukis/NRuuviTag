using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using RestSharp;

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
        /// Maximum event data batch size before the batch will be published to the event hub.
        /// </summary>
        private readonly int _maximumBatchSize;

        /// <summary>
        /// Maximum event data batch age (in seconds) before it will be published to the event hub.
        /// </summary>
        private readonly int _maximumBatchAge;

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
            _maximumBatchSize = options.MaximumBatchSize;
            _maximumBatchAge = options.MaximumBatchAge;
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

            var stopwatch = Stopwatch.StartNew();
            var batch = new List<RuuviTagSampleExtended>();
            var currentBatchStartedAt = TimeSpan.Zero;

            using var client = new RestClient(options);

            async Task PublishBatch() {
                try {
                    var request = new RestRequest(_endpointUrl, Method.Post);
                    request.AddJsonBody(batch!.ToArray());

                    var response = await client!.ExecuteAsync(request).ConfigureAwait(false);

                    if (response.StatusCode != HttpStatusCode.OK) {
                        throw new HttpRequestException($"Invalid endpoint response: {(int) response.StatusCode} ({response.StatusCode})");
                    }

                    batch.Clear();
                }
                catch (Exception e) {
                    Logger.LogError(e, Resources.LogMessage_RestPublishError);
                }
            }

            try {
                await foreach (var item in samples.ConfigureAwait(false)) {
                    var knownDevice = _getDeviceInfo?.Invoke(item.MacAddress!);
                    batch.Add(RuuviTagSampleExtended.Create(item, knownDevice?.DeviceId, knownDevice?.DisplayName));

                    cancellationToken.ThrowIfCancellationRequested();

                    if (batch.Count == 1) {
                        // Start of new batch
                        currentBatchStartedAt = stopwatch.Elapsed;
                    }

                    if (batch.Count < _maximumBatchSize && (stopwatch.Elapsed - currentBatchStartedAt).TotalSeconds < _maximumBatchAge) {
                        continue;
                    }

                    await PublishBatch().ConfigureAwait(false);

                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            catch (OperationCanceledException) {
                if (batch.Count > 0) {
                    await PublishBatch().ConfigureAwait(false);
                }
            }
            finally {
                Logger.LogInformation(Resources.LogMessage_RestClientStopped);
            }
        }

    }
}
