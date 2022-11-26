using System;
using System.ComponentModel.DataAnnotations;

namespace NRuuviTag.Rest {
    public class RestAgentOptions {

        /// <summary>
        /// The endpoint URL.
        /// </summary>
        [Required]
        public string EndpointUrl { get; set; } = default!;

        /// <summary>
        /// When <see langword="true"/>, only samples from known devices will be published. See 
        /// remarks for details.
        /// </summary>
        /// <remarks>
        ///   When <see cref="KnownDevicesOnly"/> is enabled, a sample will be discarded if 
        ///   <see cref="GetDeviceInfo"/> is <see langword="null"/>, or if it returns <see langword="null"/> 
        ///   for a given sample.
        /// </remarks>
        public bool KnownDevicesOnly { get; set; }

        /// <summary>
        /// When <see langword="true"/>, trust all SSL certificates.
        /// </summary>
        public bool TrustSsl { get; set; }

        /// <summary>
        /// The fastest rate (in seconds) that values will be sampled at for each observed device. 
        /// Less than zero means that all observed values are immediately passed to the <see cref="RestAgent"/> 
        /// for processing.
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        /// A callback that is used to retrieve the device information to use for a given 
        /// MAC address.
        /// </summary>
        public Func<string, Device?>? GetDeviceInfo { get; set; }

    }
}
