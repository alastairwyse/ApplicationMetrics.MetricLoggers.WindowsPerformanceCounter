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

using System;
using System.Collections.Generic;
using StandardAbstraction;
using FrameworkAbstraction;

namespace ApplicationMetrics.MetricLoggers.WindowsPerformanceCounter
{
    /// <summary>
    /// Writes metric and instrumentation events for an application to Windows performance counters.
    /// </summary>
    public class PerformanceCounterMetricLogger : MetricAggregateLogger, IDisposable
    {
        protected const string performanceCounterAggregateInstantaneousPostFix = "Instantaneous";
        protected const string performanceCounterAggregateBasePostFix = "Base";
        protected const int performanceCounterMaximumNameLength = 80;

        // Holds the type and an instance of all metrics that have been registered to be logged via public method RegisterMetric()
        protected Dictionary<Type, MetricBase> registeredMetrics;
        // Holds the names of all registered metrics and aggregates, and their corresponding performance counter
        protected Dictionary<string, IPerformanceCounter> registeredMetricsPerformanceCounters;
        protected string metricCategoryName;
        protected string metricCategoryDescription;
        protected ICounterCreationDataCollection counterCreationDataCollection;
        protected ICounterCreationDataFactory counterCreationDataFactory;
        protected IPerformanceCounterCategory performanceCounterCategory;
        protected IPerformanceCounterFactory performanceCounterFactory;

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.WindowsPerformanceCounter.PerformanceCounterMetricLogger class.
        /// </summary>
        /// <param name="metricCategoryName">The name of the performance counter category which the metric events should be logged under.</param>
        /// <param name="metricCategoryDescription">The description of the performance counter category which the metric events should be logged under.</param>
        /// <param name="bufferProcessingStrategy">Object which implements a processing strategy for the buffers (queues).</param>
        /// <param name="intervalMetricChecking">Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).  Note that this parameter only has an effect when running in 'non-interleaved' mode.</param>
        public PerformanceCounterMetricLogger(string metricCategoryName, string metricCategoryDescription, IBufferProcessingStrategy bufferProcessingStrategy, bool intervalMetricChecking)
            : base(bufferProcessingStrategy, intervalMetricChecking)
        {
            InitialiseProtectedMembers(metricCategoryName, metricCategoryDescription);
            counterCreationDataFactory = new CounterCreationDataFactory();
            performanceCounterCategory = new PerformanceCounterCategory();
            performanceCounterFactory = new PerformanceCounterFactory();
        }

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.WindowsPerformanceCounter.PerformanceCounterMetricLogger class.  Note this is an additional constructor to facilitate unit tests, and should not be used to instantiate the class under normal conditions.
        /// </summary>
        /// <param name="metricCategoryName">The name of the performance counter category which the metric events should be logged under.</param>
        /// <param name="metricCategoryDescription">The description of the performance counter category which the metric events should be logged under.</param>
        /// <param name="bufferProcessingStrategy">Object which implements a processing strategy for the buffers (queues).</param>
        /// <param name="intervalMetricChecking">Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).  Note that this parameter only has an effect when running in 'non-interleaved' mode.</param>
        /// <param name="counterCreationDataCollection">A test (mock) <see cref="ICounterCreationDataCollection"/> object.</param>
        /// <param name="counterCreationDataFactory">A test (mock) <see cref="ICounterCreationDataFactory"/> object.</param>
        /// <param name="performanceCounterCategory">A test (mock) <see cref="IPerformanceCounterCategory"/> object.</param>
        /// <param name="performanceCounterFactory">A test (mock) <see cref="IPerformanceCounterFactory"/> object.</param>
        /// <param name="dateTime">A test (mock) <see cref="StandardAbstraction.DateTime"/> object.</param>
        /// <param name="stopWatch">A test (mock) <see cref="Stopwatch"/> object.</param>
        /// <param name="guidProvider">A test (mock) <see cref="IGuidProvider"/> object.</param>
        public PerformanceCounterMetricLogger(string metricCategoryName, string metricCategoryDescription, IBufferProcessingStrategy bufferProcessingStrategy, bool intervalMetricChecking, ICounterCreationDataCollection counterCreationDataCollection, ICounterCreationDataFactory counterCreationDataFactory, IPerformanceCounterCategory performanceCounterCategory, IPerformanceCounterFactory performanceCounterFactory, IDateTime dateTime, IStopwatch stopWatch, IGuidProvider guidProvider)
            : base(bufferProcessingStrategy, intervalMetricChecking, dateTime, stopWatch, guidProvider)
        {
            InitialiseProtectedMembers(metricCategoryName, metricCategoryDescription);
            this.counterCreationDataCollection = counterCreationDataCollection;
            this.counterCreationDataFactory = counterCreationDataFactory;
            this.performanceCounterCategory = performanceCounterCategory;
            this.performanceCounterFactory = performanceCounterFactory;
        }

        /// <summary>
        /// Registers the specified metric to be written to the Windows performance counters.
        /// </summary>
        /// <param name="metric">The metric to register.</param>
        public void RegisterMetric(MetricBase metric)
        {
            if (registeredMetrics.ContainsKey(metric.GetType()) == true)
            {
                throw new ArgumentException("Metric of type '" + metric.GetType().Name + "' has already been registered.", "metric");
            }
            registeredMetrics.Add(metric.GetType(), metric);
            RegisterPerformanceCounter(metric.Name);
        }

        /// <summary>
        /// Creates Windows performance counters for the registered metrics and defined aggregates on the local computer.
        /// </summary>
        public void CreatePerformanceCounters()
        {
            if (counterCreationDataCollection == null)
            {
                counterCreationDataCollection = new CounterCreationDataCollection();
            }

            // Create performance counters for metrics
            foreach (MetricBase currentMetric in registeredMetrics.Values)
            {
                ValidateMetricName(currentMetric);
                ICounterCreationData counterCreationData = counterCreationDataFactory.Create(currentMetric.Name, currentMetric.Description, System.Diagnostics.PerformanceCounterType.NumberOfItems64);
                counterCreationDataCollection.Add(counterCreationData);
            }

            // Create performance counters for various types of aggregates
            foreach (MetricAggregateContainer<CountMetric> currentAggregate in countOverTimeUnitAggregateDefinitions)
            {
                // Create the basic counter as type NumberOfItems64.  The value of the aggregate will be calculated by this class, and will show the average count of items per time unit of the entire duration of running instances of this class (i.e. from when Start() was called).
                ICounterCreationData counterCreationData = counterCreationDataFactory.Create(currentAggregate.Name, currentAggregate.Description, System.Diagnostics.PerformanceCounterType.NumberOfItems64);
                counterCreationDataCollection.Add(counterCreationData);
                // Create a second 'instantaneous' instance of the same metric aggregate 
                // If the time unit of the aggregate is second use the RateOfCountsPerSecond64 counter time, for other time units use the where the AverageCount64 type
                //   As per http://msdn.microsoft.com/en-us/library/system.diagnostics.performancecountertype%28v=vs.110%29.aspx AverageCount64 requires an accompanying base counter added directly after it
                if (currentAggregate.DenominatorTimeUnit == TimeUnit.Second)
                {
                    ICounterCreationData instantaneousCounterCreationData = counterCreationDataFactory.Create(CounterNameAppendInstantaneous(currentAggregate.Name), CounterDescriptionAppendInstantaneous(currentAggregate.Description), System.Diagnostics.PerformanceCounterType.RateOfCountsPerSecond64);
                    counterCreationDataCollection.Add(instantaneousCounterCreationData);
                }
                else
                {
                    ICounterCreationData instantaneousCounterCreationData = counterCreationDataFactory.Create(CounterNameAppendInstantaneous(currentAggregate.Name), CounterDescriptionAppendInstantaneous(currentAggregate.Description), System.Diagnostics.PerformanceCounterType.AverageCount64);
                    counterCreationDataCollection.Add(instantaneousCounterCreationData);
                    ICounterCreationData instantaneousBaseCounterCreationData = counterCreationDataFactory.Create(CounterNameAppendInstantaneousBase(currentAggregate.Name), CounterDescriptionAppendInstantaneousBase(currentAggregate.Description), System.Diagnostics.PerformanceCounterType.AverageBase);
                    counterCreationDataCollection.Add(instantaneousBaseCounterCreationData);
                }
            }

            foreach (MetricAggregateContainer<AmountMetric, CountMetric> currentAggregate in amountOverCountAggregateDefinitions)
            {
                ICounterCreationData counterCreationData = counterCreationDataFactory.Create(currentAggregate.Name, currentAggregate.Description, System.Diagnostics.PerformanceCounterType.NumberOfItems64);
                counterCreationDataCollection.Add(counterCreationData);
                // Create a second 'instantaneous' instance of the same metric aggregate 
                ICounterCreationData instantaneousCounterCreationData = counterCreationDataFactory.Create(CounterNameAppendInstantaneous(currentAggregate.Name), CounterDescriptionAppendInstantaneous(currentAggregate.Description), System.Diagnostics.PerformanceCounterType.AverageCount64);
                counterCreationDataCollection.Add(instantaneousCounterCreationData);
                ICounterCreationData instantaneousBaseCounterCreationData = counterCreationDataFactory.Create(CounterNameAppendInstantaneousBase(currentAggregate.Name), CounterDescriptionAppendInstantaneousBase(currentAggregate.Description), System.Diagnostics.PerformanceCounterType.AverageBase);
                counterCreationDataCollection.Add(instantaneousBaseCounterCreationData);
            }

            foreach (MetricAggregateContainer<AmountMetric> currentAggregate in amountOverTimeUnitAggregateDefinitions)
            {
                // Create the basic counter as type NumberOfItems64.  The value of the aggregate will be calculated by this class, and will show the average amount per time unit of the entire duration of running instances of this class (i.e. from when Start() was called).
                ICounterCreationData counterCreationData = counterCreationDataFactory.Create(currentAggregate.Name, currentAggregate.Description, System.Diagnostics.PerformanceCounterType.NumberOfItems64);
                counterCreationDataCollection.Add(counterCreationData);
                // Create a second 'instantaneous' instance of the same metric aggregate 
                // If the time unit of the aggregate is second use the RateOfCountsPerSecond64 counter time, for other time units use the where the AverageCount64 type
                //   As per http://msdn.microsoft.com/en-us/library/system.diagnostics.performancecountertype%28v=vs.110%29.aspx AverageCount64 requires an accompanying base counter added directly after it
                if (currentAggregate.DenominatorTimeUnit == TimeUnit.Second)
                {
                    ICounterCreationData instantaneousCounterCreationData = counterCreationDataFactory.Create(CounterNameAppendInstantaneous(currentAggregate.Name), CounterDescriptionAppendInstantaneous(currentAggregate.Description), System.Diagnostics.PerformanceCounterType.RateOfCountsPerSecond64);
                    counterCreationDataCollection.Add(instantaneousCounterCreationData);
                }
                else
                {
                    ICounterCreationData instantaneousCounterCreationData = counterCreationDataFactory.Create(CounterNameAppendInstantaneous(currentAggregate.Name), CounterDescriptionAppendInstantaneous(currentAggregate.Description), System.Diagnostics.PerformanceCounterType.AverageCount64);
                    counterCreationDataCollection.Add(instantaneousCounterCreationData);
                    ICounterCreationData instantaneousBaseCounterCreationData = counterCreationDataFactory.Create(CounterNameAppendInstantaneousBase(currentAggregate.Name), CounterDescriptionAppendInstantaneousBase(currentAggregate.Description), System.Diagnostics.PerformanceCounterType.AverageBase);
                    counterCreationDataCollection.Add(instantaneousBaseCounterCreationData);
                }
            }

            foreach (MetricAggregateContainer<AmountMetric, AmountMetric> currentAggregate in amountOverAmountAggregateDefinitions)
            {
                // Create counter as type RawFraction
                ICounterCreationData counterCreationData = counterCreationDataFactory.Create(currentAggregate.Name, currentAggregate.Description, System.Diagnostics.PerformanceCounterType.RawFraction);
                counterCreationDataCollection.Add(counterCreationData);
                ICounterCreationData baseCounterCreationData = counterCreationDataFactory.Create(CounterNameAppendBase(currentAggregate.Name), CounterDescriptionAppendBase(currentAggregate.Description), System.Diagnostics.PerformanceCounterType.RawBase);
                counterCreationDataCollection.Add(baseCounterCreationData);
            }

            foreach (MetricAggregateContainer<IntervalMetric, CountMetric> currentAggregate in intervalOverAmountAggregateDefinitions)
            {
                ICounterCreationData counterCreationData = counterCreationDataFactory.Create(currentAggregate.Name, currentAggregate.Description, System.Diagnostics.PerformanceCounterType.NumberOfItems64);
                counterCreationDataCollection.Add(counterCreationData);
                // Create a second 'instantaneous' instance of the same metric aggregate 
                ICounterCreationData instantaneousCounterCreationData = counterCreationDataFactory.Create(CounterNameAppendInstantaneous(currentAggregate.Name), CounterDescriptionAppendInstantaneous(currentAggregate.Description), System.Diagnostics.PerformanceCounterType.AverageCount64);
                counterCreationDataCollection.Add(instantaneousCounterCreationData);
                ICounterCreationData instantaneousBaseCounterCreationData = counterCreationDataFactory.Create(CounterNameAppendInstantaneousBase(currentAggregate.Name), CounterDescriptionAppendInstantaneousBase(currentAggregate.Description), System.Diagnostics.PerformanceCounterType.AverageBase);
                counterCreationDataCollection.Add(instantaneousBaseCounterCreationData);
            }

            foreach (MetricAggregateContainer<IntervalMetric> currentAggregate in intervalOverTotalRunTimeAggregateDefinitions)
            {
                ICounterCreationData counterCreationData = counterCreationDataFactory.Create(currentAggregate.Name, currentAggregate.Description, System.Diagnostics.PerformanceCounterType.RawFraction);
                counterCreationDataCollection.Add(counterCreationData);
                ICounterCreationData baseCounterCreationData = counterCreationDataFactory.Create(CounterNameAppendBase(currentAggregate.Name), CounterDescriptionAppendBase(currentAggregate.Description), System.Diagnostics.PerformanceCounterType.RawBase);
                counterCreationDataCollection.Add(baseCounterCreationData);
            }

            try
            {
                if (performanceCounterCategory.Exists(metricCategoryName) == true)
                {
                    performanceCounterCategory.Delete(metricCategoryName);
                }
                performanceCounterCategory.Create(metricCategoryName, metricCategoryDescription, System.Diagnostics.PerformanceCounterCategoryType.SingleInstance, counterCreationDataCollection);
            }
            catch (Exception e)
            {
                throw new Exception("Failed to create performance counter category.", e);
            }
        }

        /// <summary>
        /// Creates any registered performance counters within Windows and starts a process to periodically write metrics and aggregates of metrics to the performance counters.
        /// </summary>
        public override void Start()
        {
            // Create performance counters for all registered metrics and aggregates
            List<string> registeredNames = new List<string>(registeredMetricsPerformanceCounters.Keys);
            foreach (string currentRegisteredName in registeredNames)
            {
                registeredMetricsPerformanceCounters[currentRegisteredName] = performanceCounterFactory.Create(metricCategoryName, currentRegisteredName, false);
            }

            base.Start();
        }

        #region Base Class Method Implementations

        public override void DefineMetricAggregate(CountMetric countMetric, TimeUnit timeUnit, string name, string description)
        {
            ValidateMetricAggregateName(name, performanceCounterAggregateInstantaneousPostFix + performanceCounterAggregateBasePostFix);
            base.DefineMetricAggregate(countMetric, timeUnit, name, description);
            RegisterPerformanceCounter(name);
            RegisterPerformanceCounter(CounterNameAppendInstantaneous(name));
            // If the time unit of the aggregate is not second, the instantaneous counter is of type AverageCount64 which requires a base counter...
            if (timeUnit != TimeUnit.Second)
            {
                RegisterPerformanceCounter(CounterNameAppendInstantaneousBase(name));
            }
        }

        public override void DefineMetricAggregate(AmountMetric amountMetric, CountMetric countMetric, string name, string description)
        {
            ValidateMetricAggregateName(name, performanceCounterAggregateInstantaneousPostFix + performanceCounterAggregateBasePostFix);
            base.DefineMetricAggregate(amountMetric, countMetric, name, description);
            RegisterPerformanceCounter(name);
            RegisterPerformanceCounter(CounterNameAppendInstantaneous(name));
            RegisterPerformanceCounter(CounterNameAppendInstantaneousBase(name));
        }

        public override void DefineMetricAggregate(AmountMetric amountMetric, TimeUnit timeUnit, string name, string description)
        {
            ValidateMetricAggregateName(name, performanceCounterAggregateInstantaneousPostFix + performanceCounterAggregateBasePostFix);
            base.DefineMetricAggregate(amountMetric, timeUnit, name, description);
            RegisterPerformanceCounter(name);
            RegisterPerformanceCounter(CounterNameAppendInstantaneous(name));
            // If the time unit of the aggregate is not second, the instantaneous counter is of type AverageCount64 which requires a base counter...
            if (timeUnit != TimeUnit.Second)
            {
                RegisterPerformanceCounter(CounterNameAppendInstantaneousBase(name));
            }
        }

        public override void DefineMetricAggregate(AmountMetric numeratorAmountMetric, AmountMetric denominatorAmountMetric, string name, string description)
        {
            ValidateMetricAggregateName(name, performanceCounterAggregateBasePostFix);
            base.DefineMetricAggregate(numeratorAmountMetric, denominatorAmountMetric, name, description);
            RegisterPerformanceCounter(name);
            RegisterPerformanceCounter(CounterNameAppendBase(name));
        }

        public override void DefineMetricAggregate(IntervalMetric intervalMetric, CountMetric countMetric, string name, string description)
        {
            ValidateMetricAggregateName(name, performanceCounterAggregateInstantaneousPostFix + performanceCounterAggregateBasePostFix);
            base.DefineMetricAggregate(intervalMetric, countMetric, name, description);
            RegisterPerformanceCounter(name);
            RegisterPerformanceCounter(CounterNameAppendInstantaneous(name));
            RegisterPerformanceCounter(CounterNameAppendInstantaneousBase(name));
        }

        public override void DefineMetricAggregate(IntervalMetric intervalMetric, string name, string description)
        {
            ValidateMetricAggregateName(name, performanceCounterAggregateBasePostFix);
            base.DefineMetricAggregate(intervalMetric, name, description);
            RegisterPerformanceCounter(name);
            RegisterPerformanceCounter(CounterNameAppendBase(name));
        }

        /// <summary>
        /// Logs the total of a count metric to a performance counter.
        /// </summary>
        /// <param name="countMetric">The count metric to log.</param>
        /// <param name="value">The total.</param>
        protected override void LogCountMetricTotal(CountMetric countMetric, long value)
        {
            if (registeredMetricsPerformanceCounters.ContainsKey(countMetric.Name) == true)
            {
                registeredMetricsPerformanceCounters[countMetric.Name].RawValue = value;
            }
        }

        /// <summary>
        /// Logs the total of an amount metric to a performance counter.
        /// </summary>
        /// <param name="amountMetric">The amount metric to log.</param>
        /// <param name="value">The total.</param>
        protected override void LogAmountMetricTotal(AmountMetric amountMetric, long value)
        {
            if (registeredMetricsPerformanceCounters.ContainsKey(amountMetric.Name) == true)
            {
                registeredMetricsPerformanceCounters[amountMetric.Name].RawValue = value;
            }
        }

        /// <summary>
        /// Logs the most recent value of a status metric to a performance counter.
        /// </summary>
        /// <param name="statusMetric">The status metric to log.</param>
        /// <param name="value">The value.</param>
        protected override void LogStatusMetricValue(StatusMetric statusMetric, long value)
        {
            if (registeredMetricsPerformanceCounters.ContainsKey(statusMetric.Name) == true)
            {
                registeredMetricsPerformanceCounters[statusMetric.Name].RawValue = value;
            }
        }

        /// <summary>
        /// Logs the total of an interval metric to a performance counter.
        /// </summary>
        /// <param name="intervalMetric">The interval metric to log.</param>
        /// <param name="value">The total.</param>
        protected override void LogIntervalMetricTotal(IntervalMetric intervalMetric, long value)
        {
            if (registeredMetricsPerformanceCounters.ContainsKey(intervalMetric.Name) == true)
            {
                registeredMetricsPerformanceCounters[intervalMetric.Name].RawValue = value;
            }
        }

        /// <summary>
        /// Logs a metric aggregate representing the number of occurrences of a count metric within the specified time unit to a performance counter.
        /// </summary>
        /// <param name="metricAggregate">The metric aggregate to log.</param>
        /// <param name="totalInstances">The number of occurrences of the count metric.</param>
        /// <param name="totalElapsedTimeUnits">The total elapsed time units.</param>
        protected override void LogCountOverTimeUnitAggregate(MetricAggregateContainer<CountMetric> metricAggregate, long totalInstances, long totalElapsedTimeUnits)
        {
            // If no time units have elapsed, do not log (to prevent divide by 0 error below)
            if (totalElapsedTimeUnits != 0)
            {
                registeredMetricsPerformanceCounters[metricAggregate.Name].RawValue = Convert.ToInt64(Convert.ToDouble(totalInstances) / totalElapsedTimeUnits);
                registeredMetricsPerformanceCounters[CounterNameAppendInstantaneous(metricAggregate.Name)].RawValue = totalInstances;
                // If the time unit of the aggregate is not second, the instantaneous counter is of type AverageCount64 which requires a base counter...
                if (metricAggregate.DenominatorTimeUnit != TimeUnit.Second)
                {
                    registeredMetricsPerformanceCounters[CounterNameAppendInstantaneousBase(metricAggregate.Name)].RawValue = totalElapsedTimeUnits;
                }
            }
        }

        /// <summary>
        /// Logs a metric aggregate representing the total of an amount metric per occurrence of a count metric to a performance counter.
        /// </summary>
        /// <param name="metricAggregate">The metric aggregate to log.</param>
        /// <param name="totalAmount">The total of the amount metric.</param>
        /// <param name="totalInstances">The number of occurrences of the count metric.</param>
        protected override void LogAmountOverCountAggregate(MetricAggregateContainer<AmountMetric, CountMetric> metricAggregate, long totalAmount, long totalInstances)
        {
            if (totalInstances != 0)
            {
                registeredMetricsPerformanceCounters[metricAggregate.Name].RawValue = Convert.ToInt64(Convert.ToDouble(totalAmount) / totalInstances);
                registeredMetricsPerformanceCounters[CounterNameAppendInstantaneous(metricAggregate.Name)].RawValue = totalAmount;
                registeredMetricsPerformanceCounters[CounterNameAppendInstantaneousBase(metricAggregate.Name)].RawValue = totalInstances;
            }
        }

        /// <summary>
        /// Logs a metric aggregate respresenting the total of an amount metric within the specified time unit to a performance counter.
        /// </summary>
        /// <param name="metricAggregate">The metric aggregate to log.</param>
        /// <param name="totalAmount">The total of the amount metric.</param>
        /// <param name="totalElapsedTimeUnits">The total elapsed time units.</param>
        protected override void LogAmountOverTimeUnitAggregate(MetricAggregateContainer<AmountMetric> metricAggregate, long totalAmount, long totalElapsedTimeUnits)
        {
            if (totalElapsedTimeUnits != 0)
            {
                registeredMetricsPerformanceCounters[metricAggregate.Name].RawValue = Convert.ToInt64(Convert.ToDouble(totalAmount) / totalElapsedTimeUnits);
                registeredMetricsPerformanceCounters[CounterNameAppendInstantaneous(metricAggregate.Name)].RawValue = totalAmount;
                // If the time unit of the aggregate is not second, the instantaneous counter is of type AverageCount64 which requires a base counter...
                if (metricAggregate.DenominatorTimeUnit != TimeUnit.Second)
                {
                    registeredMetricsPerformanceCounters[CounterNameAppendInstantaneousBase(metricAggregate.Name)].RawValue = totalElapsedTimeUnits;
                }
            }
        }

        /// <summary>
        /// Logs a metric aggregate respresenting the total of an amount metric divided by the total of another amount metric to a performance counter.
        /// </summary>
        /// <param name="metricAggregate">The metric aggregate to log.</param>
        /// <param name="numeratorTotal">The total of the numerator amount metric.</param>
        /// <param name="denominatorTotal">The total of the denominator amount metric.</param>
        protected override void LogAmountOverAmountAggregate(MetricAggregateContainer<AmountMetric, AmountMetric> metricAggregate, long numeratorTotal, long denominatorTotal)
        {
            if (denominatorTotal != 0)
            {
                registeredMetricsPerformanceCounters[metricAggregate.Name].RawValue = numeratorTotal;
                registeredMetricsPerformanceCounters[CounterNameAppendBase(metricAggregate.Name)].RawValue = denominatorTotal;
            }
        }

        /// <summary>
        /// Logs a metric aggregate respresenting the total of an interval metric per occurrence of a count metric to a performance counter.
        /// </summary>
        /// <param name="metricAggregate">The metric aggregate to log.</param>
        /// <param name="totalInterval">The total of the interval metric.</param>
        /// <param name="totalInstances">The number of occurrences of the count metric.</param>
        protected override void LogIntervalOverCountAggregate(MetricAggregateContainer<IntervalMetric, CountMetric> metricAggregate, long totalInterval, long totalInstances)
        {
            if (totalInstances != 0)
            {
                registeredMetricsPerformanceCounters[metricAggregate.Name].RawValue = Convert.ToInt64(Convert.ToDouble(totalInterval) / totalInstances);
                registeredMetricsPerformanceCounters[CounterNameAppendInstantaneous(metricAggregate.Name)].RawValue = totalInterval;
                registeredMetricsPerformanceCounters[CounterNameAppendInstantaneousBase(metricAggregate.Name)].RawValue = totalInstances;
            }
        }

        /// <summary>
        /// Logs a metric aggregate representing the total of an interval metric as a fraction of the total runtime of the logger to a performance counter.
        /// </summary>
        /// <param name="metricAggregate">The metric aggregate to log.</param>
        /// <param name="totalInterval">The total of the interval metric.</param>
        /// <param name="totalRunTime">The total run time of the logger since starting in milliseonds.</param>
        protected override void LogIntervalOverTotalRunTimeAggregate(MetricAggregateContainer<IntervalMetric> metricAggregate, long totalInterval, long totalRunTime)
        {
            if (totalRunTime > 0)
            {
                registeredMetricsPerformanceCounters[metricAggregate.Name].RawValue = totalInterval;
                registeredMetricsPerformanceCounters[CounterNameAppendBase(metricAggregate.Name)].RawValue = totalRunTime;
            }
        }

        #endregion

        # region Private/Protected Methods

        /// <summary>
        /// Initialises protected and private members of the class.
        /// </summary>
        ///<param name="metricCategoryName">The name of the performance counter category which the metric events should be logged under.</param>
        ///<param name="metricCategoryDescription">The description of the performance counter category which the metric events should be logged under.</param>
        protected void InitialiseProtectedMembers(string metricCategoryName, string metricCategoryDescription)
        {
            if (metricCategoryName.Trim() != "")
            {
                this.metricCategoryName = metricCategoryName;
            }
            else
            {
                throw new ArgumentException("Argument 'metricCategoryName' cannot be blank.", "metricCategoryName");
            }

            if (metricCategoryDescription.Trim() != "")
            {
                this.metricCategoryDescription = metricCategoryDescription;
            }
            else
            {
                throw new ArgumentException("Argument 'metricCategoryDescription' cannot be blank.", "metricCategoryDescription");
            }

            registeredMetrics = new Dictionary<Type, MetricBase>();
            registeredMetricsPerformanceCounters = new Dictionary<string, IPerformanceCounter>();
        }

        /// <summary>
        /// Appends a postfix to the specified counter name to indicate the instantaneous version of the counter.
        /// </summary>
        /// <param name="counterName">The name of the counter.</param>
        /// <returns>The appended string.</returns>
        protected string CounterNameAppendInstantaneous(string counterName)
        {
            return counterName + performanceCounterAggregateInstantaneousPostFix;
        }

        /// <summary>
        /// Appends a postfix to the specified counter description to indicate the instantaneous version of the counter.
        /// </summary>
        /// <param name="counterDescription">The description of the counter.</param>
        /// <returns>The appended string.</returns>
        protected string CounterDescriptionAppendInstantaneous(string counterDescription)
        {
            return counterDescription + " (" + performanceCounterAggregateInstantaneousPostFix.ToLower() + " counter)";
        }

        /// <summary>
        /// Appends a postfix to the specified counter name to indicate the accompanying base counter associated with the counter.
        /// </summary>
        /// <param name="counterName">The name of the counter.</param>
        /// <returns>The appended string.</returns>
        protected string CounterNameAppendBase(string counterName)
        {
            return counterName + performanceCounterAggregateBasePostFix;
        }

        /// <summary>
        /// Appends a postfix to the specified counter description to indicate the accompanying base counter associated with the counter.
        /// </summary>
        /// <param name="counterDescription">The description of the counter.</param>
        /// <returns>The appended string.</returns>
        protected string CounterDescriptionAppendBase(string counterDescription)
        {
            return counterDescription + " (" + performanceCounterAggregateBasePostFix.ToLower() + " counter)";
        }

        /// <summary>
        /// Appends a postfix to the specified counter name to indicate the accompanying base counter associated with the instantaneous version of the counter.
        /// </summary>
        /// <param name="counterName">The name of the counter.</param>
        /// <returns>The appended string.</returns>
        protected string CounterNameAppendInstantaneousBase(string counterName)
        {
            return counterName + performanceCounterAggregateInstantaneousPostFix + performanceCounterAggregateBasePostFix;
        }

        /// <summary>
        /// Appends a postfix to the specified counter description to indicate the accompanying base counter associated with the instantaneous version of the counter.
        /// </summary>
        /// <param name="counterDescription">The description of the counter.</param>
        /// <returns>The appended string.</returns>
        protected string CounterDescriptionAppendInstantaneousBase(string counterDescription)
        {
            return counterDescription + " (" + performanceCounterAggregateInstantaneousPostFix.ToLower() + " " + performanceCounterAggregateBasePostFix.ToLower() + " counter)";
        }

        /// <summary>
        /// Adds the specified name to the dictionary of the names of all registered metrics and aggregates, and their corresponding performance counters.
        /// </summary>
        /// <param name="name">The name of the metric or metric aggregate.</param>
        protected void RegisterPerformanceCounter(string name)
        {
            if (registeredMetricsPerformanceCounters.ContainsKey(name) == true)
            {
                throw new Exception("Metric or metric aggregate with name '" + name + "' has already been registered.");
            }
            else
            {
                // Initially the performance counter is set to null, but is created when the Start() method is called
                registeredMetricsPerformanceCounters.Add(name, null);
            }
        }

        /// <summary>
        /// Validates that the name of the specified metric to ensure it can be registered with Windows performance counters.
        /// </summary>
        /// <param name="metric">The metric to validate the name of.</param>
        protected void ValidateMetricName(MetricBase metric)
        {
            if (metric.Name.Length == 0)
            {
                throw new Exception("The 'Name' property of metric " + metric.GetType().FullName + " is blank.");
            }
            if (metric.Name.Length > performanceCounterMaximumNameLength)
            {
                throw new Exception("The 'Name' property of metric " + metric.GetType().FullName + " exceeds the " + performanceCounterMaximumNameLength.ToString() + " character limit imposed by Windows performance counters.");
            }
            if (metric.Name.Length != metric.Name.Trim().Length)
            {
                throw new Exception("The 'Name' property of metric " + metric.GetType().FullName + " cannot contain leading or trailing whitespace.");
            }
            if (metric.Name.Contains("\"") == true)
            {
                throw new Exception("The 'Name' property of metric " + metric.GetType().FullName + " cannot contain the '\"' character.");
            }
            foreach (char currentChar in metric.Name)
            {
                if (char.IsControl(currentChar))
                {
                    throw new Exception("The 'Name' property of metric " + metric.GetType().FullName + " cannot contain control characters.");
                }
            }
        }

        /// <summary>
        /// Validates that the name of the specified metric aggregate to ensure it can be registered with Windows performance counters.
        /// </summary>
        /// <param name="name">The name of the metric aggregate to validate.</param>
        /// <param name="postfix">The postfix that is added to the metric aggregate name to denote the instantaneous and/or base counter for that metric.</param>
        protected void ValidateMetricAggregateName(string name, string postfix)
        {
            if ((name.Length + postfix.Length) > performanceCounterMaximumNameLength)
            {
                throw new ArgumentException("Argument 'name' cannot exceed " + (performanceCounterMaximumNameLength - postfix.Length) + " characters.", "name");
            }
            if (name.Length != name.Trim().Length)
            {
                throw new ArgumentException("Argument 'name' cannot contain leading or trailing whitespace.", "name");
            }
            if (name.Contains("\"") == true)
            {
                throw new ArgumentException("Argument 'name' cannot contain the '\"' character.", "name");
            }
            foreach (char currentChar in name)
            {
                if (char.IsControl(currentChar))
                {
                    throw new ArgumentException("Argument 'name' cannot contain control characters.", "name");
                }
            }
        }

        #endregion

        #region Finalize / Dispose Methods

        /// <summary>
        /// Provides a method to free unmanaged resources used by this class.
        /// </summary>
        /// <param name="disposing">Whether the method is being called as part of an explicit Dispose routine, and hence whether managed resources should also be freed.</param>
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                try
                {
                    if (disposing)
                    {
                        // Free other state (managed objects).
                        foreach (IPerformanceCounter currentPerformanceCounter in registeredMetricsPerformanceCounters.Values)
                        {
                            if (currentPerformanceCounter != null)
                            {
                                currentPerformanceCounter.Dispose();
                            }
                        }
                    }
                    // Free your own state (unmanaged objects).

                    // Set large fields to null.
                }
                finally
                {
                    base.Dispose(disposing);
                }
            }
        }

        #endregion
    }
}
