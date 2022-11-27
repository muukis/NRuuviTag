using System.Collections.Generic;
using System.Linq;

namespace NRuuviTag.Rest {
    public class AverageSampleService {

        private readonly Dictionary<string?, List<RuuviTagSampleExtended>> _samples = new();

        public int SampleCount {
            get {
                lock (_samples) {
                    return _samples.Sum(sample => sample.Value.Count);
                }
            }
        }

        private List<RuuviTagSampleExtended>? GetAverage(List<RuuviTagSampleExtended>? samples) {
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
                    var seed = new RuuviTagSampleExtended
                    {
                        AccelerationX = 0,
                        AccelerationY = 0,
                        AccelerationZ = 0,
                        BatteryVoltage = 0,
                        Humidity = 0,
                        Pressure = 0,
                        SignalStrength = 0,
                        Temperature = 0,
                        TxPower = 0
                    };

                    var averageSample = deviceSamples.Aggregate(seed, (a, b) => {
                        a.AccelerationX += b.AccelerationX;
                        a.AccelerationY += b.AccelerationY;
                        a.AccelerationZ += b.AccelerationZ;
                        a.BatteryVoltage += b.BatteryVoltage;
                        a.Humidity += b.Humidity;
                        a.Pressure += b.Pressure;
                        a.SignalStrength += b.SignalStrength;
                        a.Temperature += b.Temperature;
                        a.TxPower += b.TxPower;
                        return a;
                    });

                    var deviceSamplesCount = deviceSamples.Count();
                    averageSample.AccelerationX /= deviceSamplesCount;
                    averageSample.AccelerationY /= deviceSamplesCount;
                    averageSample.AccelerationZ /= deviceSamplesCount;
                    averageSample.BatteryVoltage /= deviceSamplesCount;
                    averageSample.Humidity /= deviceSamplesCount;
                    averageSample.Pressure /= deviceSamplesCount;
                    averageSample.SignalStrength /= deviceSamplesCount;
                    averageSample.Temperature /= deviceSamplesCount;
                    averageSample.TxPower /= deviceSamplesCount;

                    var lastSample = deviceSamples.OrderByDescending(sample => sample.Timestamp).Last();
                    averageSample.DeviceId = lastSample.DeviceId;
                    averageSample.DisplayName = lastSample.DisplayName;
                    averageSample.DataFormat = lastSample.DataFormat;
                    averageSample.MacAddress = lastSample.MacAddress;
                    averageSample.MeasurementSequence = lastSample.MeasurementSequence;
                    averageSample.MovementCounter = lastSample.MovementCounter;
                    averageSample.Timestamp = lastSample.Timestamp;

                    return averageSample;
                })
                .ToList();

            return averageSamples;
        }

        private void EnsureAdd(RuuviTagSampleExtended sample) {
            if (!_samples.ContainsKey(sample.MacAddress)) {
                _samples.Add(sample.MacAddress, new List<RuuviTagSampleExtended>());
            }

            _samples[sample.MacAddress].Add(sample);
        }

        public void Add(RuuviTagSampleExtended sample) {
            lock (_samples) {
                EnsureAdd(sample);
            }
        }

        public void AddRange(IEnumerable<RuuviTagSampleExtended> samples) {
            lock (_samples) {
                foreach (var sample in samples) {
                    EnsureAdd(sample);
                }
            }
        }

        public List<RuuviTagSampleExtended> GetAverageAll(bool purge = false) {
            lock (_samples) {
                var averageSamples = _samples.Values
                    .SelectMany(GetAverage)
                    .Where(avgSample => avgSample != null)
                    .ToList();

                if (purge) {
                    _samples.Clear();
                }

                return averageSamples;
            }
        }

        public void Purge() {
            lock (_samples) {
                _samples.Clear();
            }
        }

    }
}
