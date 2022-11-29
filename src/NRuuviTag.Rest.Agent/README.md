# NRuuviTag.Rest.Agent

An agent to [NRuuviTag](https://github.com/wazzamatazz/NRuuviTag) application created for interacting with RuuviTag IoT sensors from [Ruuvi](https://www.ruuvi.com/). This agent will collect samples from all sensors for a specified amount of time and then it calculates the average sample value per sensor. The average data is then encapsulated as an array of JSON sample objects and sent to an API endpoint URL using HTTP request POST method.


# Publishing Samples to an API REST endpoint URL

The NRuuviTag.Rest.Agent ([source](https://github.com/muukis/NRuuviTag/tree/main/src/NRuuviTag.Rest.Agent)) can be used to observe RuuviTag broadcasts and forward the samples to an API REST endpoint URL:

```csharp
public async Task RestAgent(
  IRuuviTagListener listener,
  ILoggerFactory? loggerFactory = null,
  CancellationToken cancellationToken = default
) {
  var agentOptions = new RestAgentOptions() {
    EndpointUrl = "https://MY_FUNCTIONAPP.azurewbsites.net",
    AverageInterval = 600 // Send average value every 10 minutes
  };
  var agent = new RestAgent(listener, agentOptions, loggerFactory?.CreateLogger<RestAgent>());
  await agent.RunAsync(cancellationToken);
}
```


Tip of the day: You are not bound to [Azure](https://azure.microsoft.com/). Create your own API and database where to save data to. Then use [Grafana](https://grafana.com/) to display you data.

![Grafana example](https://github.com/muukis/NRuuviTag/tree/main/src/NRuuviTag.Rest.Agent/grafana.temperature.png)

The agent will POST the endpoint a JSON payload containing an array of [RuuviTagSampleExtended](https://github.com/muukis/NRuuviTag/tree/main/src/NRuuviTag.Core/RuuviTagSampleExtended.cs) objects. Example:
```js
[
  {
    "deviceId": "string",
    "displayName": "string",
    "timestamp": "2022-11-28T08:26:08.781Z",
    "signalStrength": 0,
    "dataFormat": 0,
    "temperature": 0,
    "humidity": 0,
    "pressure": 0,
    "accelerationX": 0,
    "accelerationY": 0,
    "accelerationZ": 0,
    "batteryVoltage": 0,
    "txPower": 0,
    "movementCounter": 0,
    "measurementSequence": 0,
    "macAddress": "string"
  }
]
```


# Command-Line Application

`nruuvitag` is a command-line tool for [Windows](https://github.com/muukis/NRuuviTag/tree/main/src/NRuuviTag.Cli.Windows) and [Linux](https://github.com/muukis/NRuuviTag/tree/main/src/NRuuviTag.Cli.Linux) that can scan for nearby RuuviTags, and publish device readings to the console, or to an MQTT server or Azure Event Hub.

> Add `--help` to any command to view help.

Examples:

```
# Scan for nearby devices

nruuvitag devices scan
```

```
# Write sensor readings from all nearby devices to the console

nruuvitag publish console
```

```
# Add a device to the known devices list

nruuvitag devices add "AB:CD:EF:01:23:45" --id "bedroom-1" --name "Master Bedroom"
```

```
# Publish readings from nearby devices to a REST API endpoint URL in a single calculated average of samples per device

nruuvitag publish rest "MY_API_ENDPOINT_URL" --average-interval 600 --known-devices --trust-ssl
```


# Linux Service

The command-line application can be run as a Linux service using systemd. See [here](https://github.com/muukis/NRuuviTag/tree/main/docs/LinuxSystemdService.md) for details.
