using System.Collections.Generic;
using System.Linq;

namespace NRuuviTag.Rest {
    public class AverageSampleService {

        private readonly Dictionary<string?, List<RuuviTagSampleExtended>> _samples = new();

        public int SamplesCount {
            get {
                lock (_samples) {
                    return _samples.Sum(sample => sample.Value.Count);
                }
            }
        }

        private List<RuuviTagSampleExtended>? GetAverageSamples(List<RuuviTagSampleExtended>? samples) {
            switch (samples?.Count) {
                case null:
                case 0:
                    return null;
                case 1:
                    return samples;
            }

            var averageSamples = samples
                .GroupBy(sample => sample.MacAddress)
                .Select(deviceSamples => {
                    var lastSample = deviceSamples.OrderByDescending(sample => sample.Timestamp).Last();
                    return new RuuviTagSampleExtended
                    {
                        DeviceId = lastSample.DeviceId,
                        DisplayName = lastSample.DisplayName,
                        DataFormat = lastSample.DataFormat,
                        MacAddress = lastSample.MacAddress,
                        MeasurementSequence = lastSample.MeasurementSequence,
                        MovementCounter = lastSample.MovementCounter,
                        Timestamp = lastSample.Timestamp,
                        AccelerationX = deviceSamples.Average(sample => sample.AccelerationX),
                        AccelerationY = deviceSamples.Average(sample => sample.AccelerationY),
                        AccelerationZ = deviceSamples.Average(sample => sample.AccelerationZ),
                        BatteryVoltage = deviceSamples.Average(sample => sample.BatteryVoltage),
                        Humidity = deviceSamples.Average(sample => sample.Humidity),
                        Pressure = deviceSamples.Average(sample => sample.Pressure),
                        SignalStrength = deviceSamples.Average(sample => sample.SignalStrength),
                        Temperature = deviceSamples.Average(sample => sample.Temperature),
                        TxPower = deviceSamples.Average(sample => sample.TxPower)
                    };
                })
                .ToList();

            return averageSamples;
        }

        private void EnsureAddSample(RuuviTagSampleExtended sample) {
            if (!_samples.ContainsKey(sample.MacAddress)) {
                _samples.Add(sample.MacAddress, new List<RuuviTagSampleExtended>());
            }

            _samples[sample.MacAddress].Add(sample);
        }

        public void AddSample(RuuviTagSampleExtended sample) {
            lock (_samples) {
                EnsureAddSample(sample);
            }
        }

        public void AddSampleRange(IEnumerable<RuuviTagSampleExtended> samples) {
            lock (_samples) {
                foreach (var sample in samples) {
                    EnsureAddSample(sample);
                }
            }
        }

        public List<RuuviTagSampleExtended> GetAverageSamplesAll(bool purge = false) {
            lock (_samples) {
                var averageSamples = _samples.Values
                    .SelectMany(GetAverageSamples)
                    .Where(avgSample => avgSample != null)
                    .ToList();

                if (purge) {
                    _samples.Clear();
                }

                return averageSamples;
            }
        }

        public void PurgeSamplesAll() {
            lock (_samples) {
                _samples.Clear();
            }
        }

    }
}
