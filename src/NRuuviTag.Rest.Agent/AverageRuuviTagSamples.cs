using System.Collections.Generic;
using System.Linq;

namespace NRuuviTag.Rest {
    public class AverageRuuviTagSamples {

        private readonly Dictionary<string?, List<RuuviTagSampleExtended>> _samples = new();

        public void Push(RuuviTagSampleExtended sample) {
            lock (_samples) {
                _samples[sample.DeviceId] ??= new List<RuuviTagSampleExtended>();
                _samples[sample.DeviceId].Add(sample);
            }
        }

        public RuuviTagSampleExtended? GetAverage(string? deviceId) {
            lock (_samples) {
                _samples[deviceId] ??= new List<RuuviTagSampleExtended>();
                var averageList = _samples[deviceId];
                return GetAverage(averageList);
            }
        }

        private RuuviTagSampleExtended? GetAverage(List<RuuviTagSampleExtended> samples) {
            switch (samples.Count)
            {
                case 0:
                    return null;
                case 1:
                    return samples.Single();
            }

            var averageSample = samples.Aggregate((a, b) => {
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

            averageSample.AccelerationX /= samples.Count;
            averageSample.AccelerationY /= samples.Count;
            averageSample.AccelerationZ /= samples.Count;
            averageSample.BatteryVoltage /= samples.Count;
            averageSample.Humidity /= samples.Count;
            averageSample.Pressure /= samples.Count;
            averageSample.SignalStrength /= samples.Count;
            averageSample.Temperature /= samples.Count;
            averageSample.TxPower /= samples.Count;

            var lastSample = samples.Last();

            averageSample.DeviceId = lastSample.DeviceId;
            averageSample.DisplayName = lastSample.DisplayName;
            averageSample.DataFormat = lastSample.DataFormat;
            averageSample.MacAddress = lastSample.MacAddress;
            averageSample.MeasurementSequence = lastSample.MeasurementSequence;
            averageSample.MovementCounter = lastSample.MovementCounter;
            averageSample.Timestamp = lastSample.Timestamp;

            return averageSample;
        }

        public void Clear(string? deviceId) {
            lock (_samples) {
                _samples.Remove(deviceId);
            }
        }

        public List<RuuviTagSampleExtended> GetAverageAll() {
            lock (_samples) {
                return _samples.Values
                    .Select(GetAverage)
                    .Where(avgSample => avgSample != null)
                    .ToList()!;
            }
        }

        public void ClearAll() {
            lock (_samples) {
                _samples.Clear();
            }
        }

    }
}
