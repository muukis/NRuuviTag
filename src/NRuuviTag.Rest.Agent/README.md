# NRuuviTag.Rest.Agent

An agent to [NRuuviTag](https://github.com/wazzamatazz/NRuuviTag) application created for interacting with RuuviTag IoT sensors from [Ruuvi](https://www.ruuvi.com/). This agent will collect samples from all sensors for a specified amount of time and then it calculates the average sample value per sensor. The average data is then encapsulated to an array of JSON sample objects and sent to a REST API endpoint URL using HTTP request POST method.


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


Tip of the day: You are not bound to [Azure](https://azure.microsoft.com/). Create your own API and database where to save data to. Then use [Grafana](https://grafana.com/) to display you data. For a *simple example* of a REST API check out this [Azure DevOps project](https://dev.azure.com/muukis/Testi) which was written for testing purposes.

![Grafana example](https://raw.githubusercontent.com/muukis/NRuuviTag/main/src/NRuuviTag.Rest.Agent/grafana.temperature.png)

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
