/*
 * Copyright 2021 Alastair Wyse (https://github.com/alastairwyse/ApplicationMetrics.MetricLoggers.WindowsPerformanceCounter/)
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace ApplicationMetrics.MetricLoggers.WindowsPerformanceCounter.UnitTests
{
    /// <summary>
    /// A sample status metric for testing implementations of interface IMetricLogger.
    /// </summary>
    class TestAvailableMemoryMetric : StatusMetric
    {
        public TestAvailableMemoryMetric(long availableBytes)
        {
            base.name = "AvailableMemory";
            base.description = "A single instance of this metric represents the amount of free memory in the system at a given time.";
            base.value = availableBytes;
        }
    }
}
