ApplicationMetrics.MetricLoggers.WindowsPerformanceCounter
---
An implementation of an [ApplicationMetrics](https://github.com/alastairwyse/ApplicationMetrics) [metric logger](https://github.com/alastairwyse/ApplicationMetrics/blob/master/ApplicationMetrics.MetricLoggers/IMetricAggregateLogger.cs) which writes metrics and instrumentation information to Windows performance counters.

#### Links
The documentation below was written for version 1.* of ApplicationMetrics.  Minor implementation details may have changed in this project, however the basic principles and use cases documented are still valid.  Note also that this documentation demonstrates the older ['non-interleaved'](https://github.com/alastairwyse/ApplicationMetrics#interleaved-interval-metrics) method of logging interval metrics.

Full documentation for the project...<br />
[http://www.alastairwyse.net/methodinvocationremoting/application-metrics.html](http://www.alastairwyse.net/methodinvocationremoting/application-metrics.html)

A detailed sample implementation...<br />
[http://www.alastairwyse.net/methodinvocationremoting/sample-application-5.html](http://www.alastairwyse.net/methodinvocationremoting/sample-application-5.html)

#### Release History

<table>
  <tr>
    <td><b>Version</b></td>
    <td><b>Changes</b></td>
  </tr>
  <tr>
    <td valign="top">5.0.0</td>
    <td>
      Updated for compatibility with ApplicationMetrics version 6.6.0.<br />
      Updated to .NET 6.0.
    </td>
  </tr>
  <tr>
    <td valign="top">4.1.0</td>
    <td>
      Updated for compatibility with ApplicationMetrics version 5.1.0.
    </td>
  </tr>
  <tr>
    <td valign="top">4.0.0</td>
    <td>
      Updated for compatibility with ApplicationMetrics version 5.0.0.
    </td>
  </tr>
  <tr>
    <td valign="top">3.0.0</td>
    <td>
      Updated for compatibility with ApplicationMetrics version 4.0.0.
    </td>
  </tr>
  <tr>
    <td valign="top">2.0.0</td>
    <td>
      Migrated from ApplicationMetrics and upgraded to .NET Core.
    </td>
  </tr>
</table>