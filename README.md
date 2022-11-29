# NRuuviTag

A collection of .NET libraries to simplify interacting with RuuviTag IoT sensors from [Ruuvi](https://www.ruuvi.com/).

The repository contains a [core library](/src/NRuuviTag.Core) that defines common types, and listener implementations that observe the Bluetooth LE advertisements emitted by RuuviTag devices. Samples received from RuuviTags can be automatically published to an [MQTT server](#publishing-samples-to-mqtt) or to an [Azure Event Hub](#publishing-samples-to-azure-event-hubs).

The repository contains the following listener implementations:

- [Windows](/src/NRuuviTag.Listener.Windows) (using the Windows 10 SDK)
- [Linux](/src/NRuuviTag.Listener.Linux) (using [DotNet-BlueZ](https://github.com/hashtagchris/DotNet-BlueZ) to receive advertisements from BlueZ's D-Bus APIs)

The `nruuvitag` [command-line tool](#command-line-application) can be used to as a turnkey solution to start receiving and publishing RuuviTag sensor data to an MQTT server or Azure Event Hub.


# Example Usage

> See the [samples](/samples) folder for more detailed examples of usage.

Usage is very straightforward. For example, to listen via the Windows 10 SDK using the [NRuuviTag.Listener.Windows](https://www.nuget.org/packages/NRuuviTag.Listener.Windows) NuGet package ([source](/src/NRuuviTag.Listener.Windows)):

```csharp
var client = new WindowsSdkListener();

await foreach (var sample in client.ListenAsync(cancellationToken)) {
    // sample is a RuuviTagSample object.
}
```

To listen via BlueZ on Linux using the [NRuuviTag.Listener.Linux](https://www.nuget.org/packages/NRuuviTag.Listener.Linux) NuGet package ([source](/src/NRuuviTag.Listener.Linux)):

```csharp
var client = new BlueZListener("hci0");

await foreach (var sample in client.ListenAsync(cancellationToken)) {
    // sample is a RuuviTagSample object.
}
```

To only observe specific RuuviTag devices using MAC address filtering:

```csharp
bool CanProcessMessage(string macAddress) {
    return string.Equals(macAddress, "AB:CD:EF:01:23:45");
}

await foreach (var sample in client.ListenAsync(CanProcessMessage, cancellationToken)) {
    // sample is a RuuviTagSample object.
}
```


# Publishing Samples to MQTT

The [NRuuviTag.Mqtt.Agent](https://www.nuget.org/packages/NRuuviTag.Mqtt.Agent) NuGet package ([source](/src/NRuuviTag.Mqtt.Agent)) can be used to observe RuuviTag broadcasts and forward the samples to an MQTT server:

```csharp
public async Task RunMqttAgent(
  IRuuviTagListener listener,
  ILoggerFactory? loggerFactory = null,
  CancellationToken cancellationToken = default
) {
  var agentOptions = new MqttAgentOptions() {
    Hostname = "my-mqtt-service.local:1883",
    ClientId = "MY_CLIENT_ID"
  };
  var agent = new MqttAgent(listener, agentOptions, new MqttFactory(), loggerFactory?.CreateLogger<MqttAgent>());
  await agent.RunAsync(cancellationToken);
}
```


# Publishing Samples to Azure Event Hubs

The [NRuuviTag.AzureEventHubs.Agent](https://www.nuget.org/packages/NRuuviTag.AzureEventHubs.Agent) NuGet package ([source](/src/NRuuviTag.AzureEventHubs.Agent)) can be used to observe RuuviTag broadcasts and forward the samples to an Azure Event Hub:

```csharp
public async Task AzureEventHubAgent(
  IRuuviTagListener listener,
  ILoggerFactory? loggerFactory = null,
  CancellationToken cancellationToken = default
) {
  var agentOptions = new AzureEventHubAgentOptions() {
    ConnectionString = "Endpoint=sb://MY_NAMESPACE.servicebus.windows.net/;SharedAccessKeyName=MY_KEY_NAME;SharedAccessKey=MY_KEY",
    EventHubName = "MY_EVENT_HUB"
  };
  var agent = new AzureEventHubAgent(listener, agentOptions, loggerFactory?.CreateLogger<AzureEventHubAgent>());
  await agent.RunAsync(cancellationToken);
}
```


# Publishing Samples to a REST API endpoint URL

The [NRuuviTag.Rest.Agent](https://www.nuget.org/packages/NRuuviTag.Rest.Agent) NuGet package ([source](/src/NRuuviTag.Rest.Agent)) can be used to observe RuuviTag broadcasts and forward the samples to a REST API endpoint URL:

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

![Grafana example](https://raw.githubusercontent.com/muukis/NRuuviTag/main/src/NRuuviTag.Rest.Agent/grafana.temperature.png)

The agent will POST the endpoint a JSON payload containing an array of [RuuviTagSampleExtended](/src/NRuuviTag.Core/RuuviTagSampleExtended.cs) objects. Example:
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

`nruuvitag` is a command-line tool for [Windows](/src/NRuuviTag.Cli.Windows) and [Linux](/src/NRuuviTag.Cli.Linux) that can scan for nearby RuuviTags, and publish device readings to the console, or to an MQTT server or Azure Event Hub.

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
# Publish readings from known devices to an MQTT server

nruuvitag publish mqtt my-mqtt-service.local:1883 --client-id "MY_CLIENT_ID" --topic "{clientId}/my-ruuvi-tags/{deviceId}" --known-devices
```

```
# Publish readings from nearby devices to an Azure Event Hub in batches of up to 100 samples

nruuvitag publish az "MY_CONNECTION_STRING" "MY_EVENT_HUB" --batch-size-limit 100
```

```
# Publish readings from nearby devices to a REST API endpoint URL in a single calculated average of samples per device

nruuvitag publish rest "MY_API_ENDPOINT_URL" --average-interval 600 --known-devices --trust-ssl
```


# Linux Service

The command-line application can be run as a Linux service using systemd. See [here](/docs/LinuxSystemdService.md) for details.


# Building the Solution

The repository uses [Cake](https://cakebuild.net/) for cross-platform build automation. The build script allows for metadata such as a build counter to be specified when called by a continuous integration system such as TeamCity.

A build can be run from the command line using the [build.ps1](/build.ps1) PowerShell script or the [build.sh](/build.sh) Bash script. For documentation about the available build script parameters, see [build.cake](/build.cake).

