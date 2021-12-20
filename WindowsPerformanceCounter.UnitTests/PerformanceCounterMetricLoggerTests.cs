/*
 * Copyright 2120 Alastair Wyse (https://github.com/alastairwyse/ApplicationMetrics.MetricLoggers.WindowsPerformanceCounter/)
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

#pragma warning disable 1591

using System;
using System.Threading;
using NUnit.Framework;
using NSubstitute;
using StandardAbstraction;
using FrameworkAbstraction;


namespace ApplicationMetrics.MetricLoggers.WindowsPerformanceCounter.UnitTests
{
    /// <summary>
    /// Unit tests for class ApplicationMetrics..MetricLoggers.WindowsPerformanceCounter.PerformanceCounterMetricLogger.
    /// </summary>
    /// <remarks>This class also implicity tests the functionality in abstract classes MetricLoggerStorer and MetricAggregateLogger.  Functionality from MetricLoggerStorer and MetricAggregateLogger (e.g. storing of totals of metrics, and calulation of aggregates) does not need to be tested in other classes deriving from MetricAggregateLogger.</remarks>
    class PerformanceCounterMetricLoggerTests
    {
        // Some of these tests use Thread.Sleep() statements to synchronise activity between the main thread and buffer processing worker thread, and hence results could be non-deterministic depending on system thread scheduling and performance.
        // Decided to do this, as making things fully deterministic would involve adding more test-only thread synchronising mechanisms (in addition to the existing WorkerThreadBufferProcessorBase.loopIterationCompleteSignal property), which would mean more redundtant statements executing during normal runtime.
        // I think the current implementation strikes a balance between having fully deterministic tests, and not interfering too much with normal runtime operation.

        private string testMetricCategoryName = "TestCategory";
        private string testMetricCategoryDescription = "Description of Test Category"; private ICounterCreationDataCollection mockCounterCreationDataCollection;
        private ICounterCreationDataFactory mockCounterCreationDataFactory;
        private IPerformanceCounterCategory mockPerformanceCounterCategory;
        private IPerformanceCounterFactory mockPerformanceCounterFactory;
        private IPerformanceCounter mockPerformanceCounter;
        private IDateTime mockDateTime;
        private IStopwatch mockStopWatch;
        private ICounterCreationData mockCounterCreationData;
        private ManualResetEvent workerThreadLoopIterationCompleteSignal;
        private LoopingWorkerThreadBufferProcessor bufferProcessor;
        private PerformanceCounterMetricLogger testPerformanceCounterMetricLogger;

        [SetUp]
        protected void SetUp()
        {
            mockCounterCreationDataCollection = Substitute.For<ICounterCreationDataCollection>();
            mockCounterCreationDataFactory = Substitute.For<ICounterCreationDataFactory>();
            mockPerformanceCounterCategory = Substitute.For<IPerformanceCounterCategory>();
            mockPerformanceCounterFactory = Substitute.For<IPerformanceCounterFactory>();
            mockPerformanceCounter = Substitute.For<IPerformanceCounter>();
            mockDateTime = Substitute.For<IDateTime>();
            mockStopWatch = Substitute.For<IStopwatch>();
            mockCounterCreationData = Substitute.For<ICounterCreationData>();
            workerThreadLoopIterationCompleteSignal = new ManualResetEvent(false);
            bufferProcessor = new LoopingWorkerThreadBufferProcessor(10, workerThreadLoopIterationCompleteSignal, 1);
            testPerformanceCounterMetricLogger = new PerformanceCounterMetricLogger(testMetricCategoryName, testMetricCategoryDescription, bufferProcessor, true, mockCounterCreationDataCollection, mockCounterCreationDataFactory, mockPerformanceCounterCategory, mockPerformanceCounterFactory, mockDateTime, mockStopWatch);
        }

        [TearDown]
        protected void TearDown()
        {
            testPerformanceCounterMetricLogger.Dispose();
            bufferProcessor.Dispose();
            workerThreadLoopIterationCompleteSignal.Dispose();
        }

        [Test]
        public void Constructor_InvalidMetricCategoryNameArgument()
        {
            ArgumentException e = Assert.Throws<ArgumentException>(delegate
            {
                testPerformanceCounterMetricLogger = new PerformanceCounterMetricLogger(" ", testMetricCategoryDescription, bufferProcessor, true);
            });

            Assert.That(e.Message, Does.StartWith("Argument 'metricCategoryName' cannot be blank."));
            Assert.AreEqual("metricCategoryName", e.ParamName);
        }

        [Test]
        public void Constructor_InvalidMetricCategoryDescriptionArgument()
        {
            ArgumentException e = Assert.Throws<ArgumentException>(delegate
            {
                testPerformanceCounterMetricLogger = new PerformanceCounterMetricLogger(testMetricCategoryName, " ", bufferProcessor, true);
            });

            Assert.That(e.Message, Does.StartWith("Argument 'metricCategoryDescription' cannot be blank."));
            Assert.AreEqual("metricCategoryDescription", e.ParamName);
        }

        [Test]
        public void DefineMetricAggregate_InvalidName()
        {
            // Tests exception when classes deriving from MetricAggregateContainerBase are constructed with a blank name
            ArgumentException e = Assert.Throws<ArgumentException>(delegate
            {
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageReceivedMetric(), TimeUnit.Second, "", "The number of messages received per second.");
            });

            Assert.That(e.Message, Does.StartWith("Argument 'name' cannot be blank."));
            Assert.AreEqual("name", e.ParamName);
        }

        [Test]
        public void DefineMetricAggregate_InvalidDescription()
        {
            // Tests exception when classes deriving from MetricAggregateContainerBase are constructed with a blank description
            ArgumentException e = Assert.Throws<ArgumentException>(delegate
            {
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageReceivedMetric(), TimeUnit.Second, "MessagesReceivedPerSecond", " ");
            });

            Assert.That(e.Message, Does.StartWith("Argument 'description' cannot be blank."));
            Assert.AreEqual("description", e.ParamName);
        }

        [Test]
        public void DefineMetricAggregate_DuplicateName()
        {
            Exception e = Assert.Throws<Exception>(delegate
            {
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageReceivedMetric(), TimeUnit.Second, "MessagesReceivedPerSecond", "The number of messages received per second.");
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageReceivedMetric(), TimeUnit.Second, "MessagesReceivedPerSecond", "The number of messages received per second.");
            });

            Assert.That(e.Message, Does.StartWith("Metric aggregate with name 'MessagesReceivedPerSecond' has already been defined."));

            e = Assert.Throws<Exception>(delegate
            {
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), new TestMessageReceivedMetric(), "DuplicateAggregateName", "Duplicate metric aggregate name.");
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageReceivedMetric(), TimeUnit.Second, "DuplicateAggregateName", "Duplicate metric aggregate name.");
            });

            Assert.That(e.Message, Does.StartWith("Metric aggregate with name 'DuplicateAggregateName' has already been defined."));

            e = Assert.Throws<Exception>(delegate
            {
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageReceivedMetric(), TimeUnit.Second, "DuplicateAggregateName", "Duplicate metric aggregate name.");
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), new TestMessageReceivedMetric(), "DuplicateAggregateName", "Duplicate metric aggregate name.");
            });

            Assert.That(e.Message, Does.StartWith("Metric aggregate with name 'DuplicateAggregateName' has already been defined."));

            e = Assert.Throws<Exception>(delegate
            {
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), TimeUnit.Second, "DuplicateAggregateName", "Duplicate metric aggregate name.");
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageReceivedMetric(), TimeUnit.Second, "DuplicateAggregateName", "Duplicate metric aggregate name.");
            });

            Assert.That(e.Message, Does.StartWith("Metric aggregate with name 'DuplicateAggregateName' has already been defined."));

            e = Assert.Throws<Exception>(delegate
            {
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageReceivedMetric(), TimeUnit.Second, "DuplicateAggregateName", "Duplicate metric aggregate name.");
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), TimeUnit.Second, "DuplicateAggregateName", "Duplicate metric aggregate name.");
            });

            Assert.That(e.Message, Does.StartWith("Metric aggregate with name 'DuplicateAggregateName' has already been defined."));

            e = Assert.Throws<Exception>(delegate
            {
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), TimeUnit.Second, "DuplicateAggregateName", "Duplicate metric aggregate name.");
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), new TestDiskBytesReadMetric(), "DuplicateAggregateName", "Duplicate metric aggregate name.");
            });

            Assert.That(e.Message, Does.StartWith("Metric aggregate with name 'DuplicateAggregateName' has already been defined."));

            e = Assert.Throws<Exception>(delegate
            {
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), new TestDiskBytesReadMetric(), "DuplicateAggregateName", "Duplicate metric aggregate name.");
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), TimeUnit.Second, "DuplicateAggregateName", "Duplicate metric aggregate name.");
            });

            Assert.That(e.Message, Does.StartWith("Metric aggregate with name 'DuplicateAggregateName' has already been defined."));

            e = Assert.Throws<Exception>(delegate
            {
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), TimeUnit.Second, "DuplicateAggregateName", "Duplicate metric aggregate name.");
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestDiskReadTimeMetric(), new TestDiskReadOperationMetric(), "DuplicateAggregateName", "Duplicate metric aggregate name.");
            });

            Assert.That(e.Message, Does.StartWith("Metric aggregate with name 'DuplicateAggregateName' has already been defined."));

            e = Assert.Throws<Exception>(delegate
            {
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestDiskReadTimeMetric(), new TestDiskReadOperationMetric(), "DuplicateAggregateName", "Duplicate metric aggregate name.");
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), TimeUnit.Second, "DuplicateAggregateName", "Duplicate metric aggregate name.");
            });

            Assert.That(e.Message, Does.StartWith("Metric aggregate with name 'DuplicateAggregateName' has already been defined."));
        }

        [Test]
        public void CreatePerformanceCounters_PerformanceCounterCategoryExistsMethodThrowsException()
        {
            mockCounterCreationDataFactory.Create("MessagesReceivedPerSecond", "The number of messages received per second", System.Diagnostics.PerformanceCounterType.NumberOfItems64).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataCollection.Add(mockCounterCreationData).Returns<Int32>(1, 2);
            mockCounterCreationDataFactory.Create("MessagesReceivedPerSecondInstantaneous", "The number of messages received per second (instantaneous counter)", System.Diagnostics.PerformanceCounterType.RateOfCountsPerSecond64).Returns<ICounterCreationData>(mockCounterCreationData);
            mockPerformanceCounterCategory.When(performanceCounterCategory => performanceCounterCategory.Exists(testMetricCategoryName)).Throw(new UnauthorizedAccessException("Test inner exception"));

            var e = Assert.Throws<Exception>(delegate
            {
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageReceivedMetric(), TimeUnit.Second, "MessagesReceivedPerSecond", "The number of messages received per second");
                testPerformanceCounterMetricLogger.CreatePerformanceCounters();
            });

            mockCounterCreationDataFactory.Received(1).Create("MessagesReceivedPerSecond", "The number of messages received per second", System.Diagnostics.PerformanceCounterType.NumberOfItems64);
            mockCounterCreationDataCollection.Received(2).Add(mockCounterCreationData);
            mockCounterCreationDataFactory.Received(1).Create("MessagesReceivedPerSecondInstantaneous", "The number of messages received per second (instantaneous counter)", System.Diagnostics.PerformanceCounterType.RateOfCountsPerSecond64);
            mockPerformanceCounterCategory.Received(1).Exists(testMetricCategoryName);
            Assert.That(e.Message, Does.StartWith("Failed to create performance counter category."));
            Assert.IsInstanceOf(typeof(UnauthorizedAccessException), e.InnerException);
            Assert.That(e.InnerException.Message, Does.StartWith("Test inner exception"));
        }

        [Test]
        public void CreatePerformanceCounters_PerformanceCounterCategoryCreateMethodThrowsException()
        {
            mockCounterCreationDataFactory.Create("MessagesReceivedPerSecond", "The number of messages received per second", System.Diagnostics.PerformanceCounterType.NumberOfItems64).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataCollection.Add(mockCounterCreationData).Returns<Int32>(1, 2);
            mockCounterCreationDataFactory.Create("MessagesReceivedPerSecondInstantaneous", "The number of messages received per second (instantaneous counter)", System.Diagnostics.PerformanceCounterType.RateOfCountsPerSecond64).Returns<ICounterCreationData>(mockCounterCreationData);
            mockPerformanceCounterCategory.Exists(testMetricCategoryName).Returns<Boolean>(true);
            mockPerformanceCounterCategory.When(performanceCounterCategory => performanceCounterCategory.Create(testMetricCategoryName, testMetricCategoryDescription, System.Diagnostics.PerformanceCounterCategoryType.SingleInstance, mockCounterCreationDataCollection)).Throw(new System.ComponentModel.Win32Exception("Test inner exception"));

            Exception e = Assert.Throws<Exception>(delegate
            {
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageReceivedMetric(), TimeUnit.Second, "MessagesReceivedPerSecond", "The number of messages received per second");
                testPerformanceCounterMetricLogger.CreatePerformanceCounters();
            });

            mockCounterCreationDataFactory.Received(1).Create("MessagesReceivedPerSecond", "The number of messages received per second", System.Diagnostics.PerformanceCounterType.NumberOfItems64);
            mockCounterCreationDataCollection.Received(2).Add(mockCounterCreationData);
            mockCounterCreationDataFactory.Received(1).Create("MessagesReceivedPerSecondInstantaneous", "The number of messages received per second (instantaneous counter)", System.Diagnostics.PerformanceCounterType.RateOfCountsPerSecond64);
            mockPerformanceCounterCategory.Received(1).Delete(testMetricCategoryName);
            mockPerformanceCounterCategory.Received(1).Exists(testMetricCategoryName);
            Assert.That(e.Message, Does.StartWith("Failed to create performance counter category."));
            Assert.IsInstanceOf(typeof(System.ComponentModel.Win32Exception), e.InnerException);
            Assert.That(e.InnerException.Message, Does.StartWith("Test inner exception"));
        }
        [Test]
        public void CreatePerformanceCounters_InvalidMetricName()
        {
            Exception e = Assert.Throws<Exception>(delegate
            {
                testPerformanceCounterMetricLogger.RegisterMetric(new BlankNameMetric());
                testPerformanceCounterMetricLogger.CreatePerformanceCounters();
            });

            Assert.That(e.Message, Does.StartWith("The 'Name' property of metric " + new BlankNameMetric().GetType().FullName + " is blank."));

            e = Assert.Throws<Exception>(delegate
            {
                testPerformanceCounterMetricLogger = new PerformanceCounterMetricLogger(testMetricCategoryName, testMetricCategoryDescription, bufferProcessor, true, mockCounterCreationDataCollection, mockCounterCreationDataFactory, mockPerformanceCounterCategory, mockPerformanceCounterFactory, mockDateTime, mockStopWatch);
                testPerformanceCounterMetricLogger.RegisterMetric(new LongNameMetric());
                testPerformanceCounterMetricLogger.CreatePerformanceCounters();
            });

            Assert.That(e.Message, Does.StartWith("The 'Name' property of metric " + new LongNameMetric().GetType().FullName + " exceeds the 80 character limit imposed by Windows performance counters."));

            e = Assert.Throws<Exception>(delegate
            {
                testPerformanceCounterMetricLogger = new PerformanceCounterMetricLogger(testMetricCategoryName, testMetricCategoryDescription, bufferProcessor, true, mockCounterCreationDataCollection, mockCounterCreationDataFactory, mockPerformanceCounterCategory, mockPerformanceCounterFactory, mockDateTime, mockStopWatch);
                testPerformanceCounterMetricLogger.RegisterMetric(new WhitespaceNameMetric());
                testPerformanceCounterMetricLogger.CreatePerformanceCounters();
            });

            Assert.That(e.Message, Does.StartWith("The 'Name' property of metric " + new WhitespaceNameMetric().GetType().FullName + " cannot contain leading or trailing whitespace."));

            e = Assert.Throws<Exception>(delegate
            {
                testPerformanceCounterMetricLogger = new PerformanceCounterMetricLogger(testMetricCategoryName, testMetricCategoryDescription, bufferProcessor, true, mockCounterCreationDataCollection, mockCounterCreationDataFactory, mockPerformanceCounterCategory, mockPerformanceCounterFactory, mockDateTime, mockStopWatch);
                testPerformanceCounterMetricLogger.RegisterMetric(new DoubleQuoteNameMetric());
                testPerformanceCounterMetricLogger.CreatePerformanceCounters();
            });

            Assert.That(e.Message, Does.StartWith("The 'Name' property of metric " + new DoubleQuoteNameMetric().GetType().FullName + " cannot contain the '\"' character."));

            e = Assert.Throws<Exception>(delegate
            {
                testPerformanceCounterMetricLogger = new PerformanceCounterMetricLogger(testMetricCategoryName, testMetricCategoryDescription, bufferProcessor, true, mockCounterCreationDataCollection, mockCounterCreationDataFactory, mockPerformanceCounterCategory, mockPerformanceCounterFactory, mockDateTime, mockStopWatch);
                testPerformanceCounterMetricLogger.RegisterMetric(new ControlCharacterNameMetric());
                testPerformanceCounterMetricLogger.CreatePerformanceCounters();
            });

            Assert.That(e.Message, Does.StartWith("The 'Name' property of metric " + new ControlCharacterNameMetric().GetType().FullName + " cannot contain control characters."));
        }

        [Test]
        public void CreatePerformanceCounters_InvalidMetricAggregateName()
        {
            ArgumentException e = Assert.Throws<ArgumentException>(delegate
            {
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageReceivedMetric(), TimeUnit.Second, "1234567890123456789012345678901234567890123456789012345678901234", "Test metric");
            });

            Assert.That(e.Message, Does.StartWith("Argument 'name' cannot exceed 63 characters."));
            Assert.AreEqual("name", e.ParamName);

            e = Assert.Throws<ArgumentException>(delegate
            {
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), new TestMessageReceivedMetric(), " WhitespaceName ", "Test metric");
            });

            Assert.That(e.Message, Does.StartWith("Argument 'name' cannot contain leading or trailing whitespace."));
            Assert.AreEqual("name", e.ParamName);

            e = Assert.Throws<ArgumentException>(delegate
            {
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), TimeUnit.Second, "DoubleQuote\"Name", "Test metric");
            });

            Assert.That(e.Message, Does.StartWith("Argument 'name' cannot contain the '\"' character."));
            Assert.AreEqual("name", e.ParamName);

            e = Assert.Throws<ArgumentException>(delegate
            {
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), new TestDiskBytesReadMetric(), "12345678901234567890123456789012345678901234567890123456789012345678901234567", "Test metric");
            });

            Assert.That(e.Message, Does.StartWith("Argument 'name' cannot exceed 76 characters."));
            Assert.AreEqual("name", e.ParamName);

            e = Assert.Throws<ArgumentException>(delegate
            {
                char controlCharacter = (char)0x02;
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestDiskReadTimeMetric(), new TestMessageReceivedMetric(), "ControlCharacter" + controlCharacter.ToString() + "Name", "Test metric");
            });

            Assert.That(e.Message, Does.StartWith("Argument 'name' cannot contain control characters."));
            Assert.AreEqual("name", e.ParamName);

            e = Assert.Throws<ArgumentException>(delegate
            {
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestDiskReadTimeMetric(), "12345678901234567890123456789012345678901234567890123456789012345678901234567", "Test metric");
            });

            Assert.That(e.Message, Does.StartWith("Argument 'name' cannot exceed 76 characters."));
            Assert.AreEqual("name", e.ParamName);
        }

        [Test]
        public void CreatePerformanceCounters_CountMetric()
        {
            mockCounterCreationDataFactory.Create(new TestMessageReceivedMetric().Name, new TestMessageReceivedMetric().Description, System.Diagnostics.PerformanceCounterType.NumberOfItems64).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataCollection.Add(mockCounterCreationData).Returns<Int32>(1);
            mockPerformanceCounterCategory.Exists(testMetricCategoryName).Returns<Boolean>(true);

            testPerformanceCounterMetricLogger.RegisterMetric(new TestMessageReceivedMetric());
            testPerformanceCounterMetricLogger.CreatePerformanceCounters();

            mockCounterCreationDataFactory.Received(1).Create(new TestMessageReceivedMetric().Name, new TestMessageReceivedMetric().Description, System.Diagnostics.PerformanceCounterType.NumberOfItems64);
            mockCounterCreationDataCollection.Received(1).Add(mockCounterCreationData);
            mockPerformanceCounterCategory.Received(1).Exists(testMetricCategoryName);
            mockPerformanceCounterCategory.Received(1).Delete(testMetricCategoryName);
            mockPerformanceCounterCategory.Received(1).Create(testMetricCategoryName, testMetricCategoryDescription, System.Diagnostics.PerformanceCounterCategoryType.SingleInstance, mockCounterCreationDataCollection);
        }

        [Test]
        public void CreatePerformanceCounters_CountOverTimeUnitMetricAggregate()
        {
            mockCounterCreationDataFactory.Create("MessagesReceivedPerSecond", "The number of messages received per second", System.Diagnostics.PerformanceCounterType.NumberOfItems64).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataCollection.Add(mockCounterCreationData).Returns<Int32>(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11);
            mockCounterCreationDataFactory.Create("MessagesReceivedPerSecondInstantaneous", "The number of messages received per second (instantaneous counter)", System.Diagnostics.PerformanceCounterType.RateOfCountsPerSecond64).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataFactory.Create("MessagesReceivedPerMinute", "The number of messages received per minute", System.Diagnostics.PerformanceCounterType.NumberOfItems64).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataFactory.Create("MessagesReceivedPerMinuteInstantaneous", "The number of messages received per minute (instantaneous counter)", System.Diagnostics.PerformanceCounterType.AverageCount64).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataFactory.Create("MessagesReceivedPerMinuteInstantaneousBase", "The number of messages received per minute (instantaneous base counter)", System.Diagnostics.PerformanceCounterType.AverageBase).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataFactory.Create("MessagesReceivedPerHour", "The number of messages received per hour", System.Diagnostics.PerformanceCounterType.NumberOfItems64).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataFactory.Create("MessagesReceivedPerHourInstantaneous", "The number of messages received per hour (instantaneous counter)", System.Diagnostics.PerformanceCounterType.AverageCount64).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataFactory.Create("MessagesReceivedPerHourInstantaneousBase", "The number of messages received per hour (instantaneous base counter)", System.Diagnostics.PerformanceCounterType.AverageBase).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataFactory.Create("MessagesReceivedPerDay", "The number of messages received per day", System.Diagnostics.PerformanceCounterType.NumberOfItems64).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataFactory.Create("MessagesReceivedPerDayInstantaneous", "The number of messages received per day (instantaneous counter)", System.Diagnostics.PerformanceCounterType.AverageCount64).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataFactory.Create("MessagesReceivedPerDayInstantaneousBase", "The number of messages received per day (instantaneous base counter)", System.Diagnostics.PerformanceCounterType.AverageBase).Returns<ICounterCreationData>(mockCounterCreationData);
            mockPerformanceCounterCategory.Exists(testMetricCategoryName).Returns<Boolean>(true);
            
            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageReceivedMetric(), TimeUnit.Second, "MessagesReceivedPerSecond", "The number of messages received per second");
            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageReceivedMetric(), TimeUnit.Minute, "MessagesReceivedPerMinute", "The number of messages received per minute");
            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageReceivedMetric(), TimeUnit.Hour, "MessagesReceivedPerHour", "The number of messages received per hour");
            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageReceivedMetric(), TimeUnit.Day, "MessagesReceivedPerDay", "The number of messages received per day");
            testPerformanceCounterMetricLogger.CreatePerformanceCounters();

            mockCounterCreationDataFactory.Received(1).Create("MessagesReceivedPerSecond", "The number of messages received per second", System.Diagnostics.PerformanceCounterType.NumberOfItems64);
            mockCounterCreationDataCollection.Received(11).Add(mockCounterCreationData);
            mockCounterCreationDataFactory.Received(1).Create("MessagesReceivedPerSecondInstantaneous", "The number of messages received per second (instantaneous counter)", System.Diagnostics.PerformanceCounterType.RateOfCountsPerSecond64);
            mockCounterCreationDataFactory.Received(1).Create("MessagesReceivedPerMinute", "The number of messages received per minute", System.Diagnostics.PerformanceCounterType.NumberOfItems64);
            mockCounterCreationDataFactory.Received(1).Create("MessagesReceivedPerMinuteInstantaneous", "The number of messages received per minute (instantaneous counter)", System.Diagnostics.PerformanceCounterType.AverageCount64);
            mockCounterCreationDataFactory.Received(1).Create("MessagesReceivedPerMinuteInstantaneousBase", "The number of messages received per minute (instantaneous base counter)", System.Diagnostics.PerformanceCounterType.AverageBase);
            mockCounterCreationDataFactory.Received(1).Create("MessagesReceivedPerHour", "The number of messages received per hour", System.Diagnostics.PerformanceCounterType.NumberOfItems64);
            mockCounterCreationDataFactory.Received(1).Create("MessagesReceivedPerHourInstantaneous", "The number of messages received per hour (instantaneous counter)", System.Diagnostics.PerformanceCounterType.AverageCount64);
            mockCounterCreationDataFactory.Received(1).Create("MessagesReceivedPerHourInstantaneousBase", "The number of messages received per hour (instantaneous base counter)", System.Diagnostics.PerformanceCounterType.AverageBase);
            mockCounterCreationDataFactory.Received(1).Create("MessagesReceivedPerDay", "The number of messages received per day", System.Diagnostics.PerformanceCounterType.NumberOfItems64);
            mockCounterCreationDataFactory.Received(1).Create("MessagesReceivedPerDayInstantaneous", "The number of messages received per day (instantaneous counter)", System.Diagnostics.PerformanceCounterType.AverageCount64);
            mockCounterCreationDataFactory.Received(1).Create("MessagesReceivedPerDayInstantaneousBase", "The number of messages received per day (instantaneous base counter)", System.Diagnostics.PerformanceCounterType.AverageBase);
            mockPerformanceCounterCategory.Received(1).Exists(testMetricCategoryName);
            mockPerformanceCounterCategory.Received(1).Delete(testMetricCategoryName);
            mockPerformanceCounterCategory.Received(1).Create(testMetricCategoryName, testMetricCategoryDescription, System.Diagnostics.PerformanceCounterCategoryType.SingleInstance, mockCounterCreationDataCollection);
        }
        
        [Test]
        public void CreatePerformanceCounters_AmountMetric()
        {
            mockCounterCreationDataFactory.Create(new TestMessageBytesReceivedMetric().Name, new TestMessageBytesReceivedMetric().Description, System.Diagnostics.PerformanceCounterType.NumberOfItems64).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataCollection.Add(mockCounterCreationData).Returns<Int32>(1);
            mockPerformanceCounterCategory.Exists(testMetricCategoryName).Returns<Boolean>(true);

            testPerformanceCounterMetricLogger.RegisterMetric(new TestMessageBytesReceivedMetric());
            testPerformanceCounterMetricLogger.CreatePerformanceCounters();

            mockCounterCreationDataFactory.Received(1).Create(new TestMessageBytesReceivedMetric().Name, new TestMessageBytesReceivedMetric().Description, System.Diagnostics.PerformanceCounterType.NumberOfItems64);
            mockCounterCreationDataCollection.Received(1).Add(mockCounterCreationData);
            mockPerformanceCounterCategory.Received(1).Exists(testMetricCategoryName);
            mockPerformanceCounterCategory.Received(1).Delete(testMetricCategoryName);
            mockPerformanceCounterCategory.Received(1).Create(testMetricCategoryName, testMetricCategoryDescription, System.Diagnostics.PerformanceCounterCategoryType.SingleInstance, mockCounterCreationDataCollection);
        }
        
        [Test]
        public void CreatePerformanceCounters_AmountOverCountMetricAggregate()
        {

            mockCounterCreationDataFactory.Create("BytesReceivedPerMessage", "The number of bytes received per message", System.Diagnostics.PerformanceCounterType.NumberOfItems64).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataCollection.Add(mockCounterCreationData).Returns<Int32>(1, 2, 3);
            mockCounterCreationDataFactory.Create("BytesReceivedPerMessageInstantaneous", "The number of bytes received per message (instantaneous counter)", System.Diagnostics.PerformanceCounterType.AverageCount64).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataFactory.Create("BytesReceivedPerMessageInstantaneousBase", "The number of bytes received per message (instantaneous base counter)", System.Diagnostics.PerformanceCounterType.AverageBase).Returns<ICounterCreationData>(mockCounterCreationData);
            mockPerformanceCounterCategory.Exists(testMetricCategoryName).Returns<Boolean>(true);

            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), new TestMessageReceivedMetric(), "BytesReceivedPerMessage", "The number of bytes received per message");
            testPerformanceCounterMetricLogger.CreatePerformanceCounters();

            mockCounterCreationDataFactory.Create("BytesReceivedPerMessage", "The number of bytes received per message", System.Diagnostics.PerformanceCounterType.NumberOfItems64);
            mockCounterCreationDataCollection.Add(mockCounterCreationData);
            mockCounterCreationDataFactory.Create("BytesReceivedPerMessageInstantaneous", "The number of bytes received per message (instantaneous counter)", System.Diagnostics.PerformanceCounterType.AverageCount64);
            mockCounterCreationDataFactory.Create("BytesReceivedPerMessageInstantaneousBase", "The number of bytes received per message (instantaneous base counter)", System.Diagnostics.PerformanceCounterType.AverageBase);
            mockPerformanceCounterCategory.Exists(testMetricCategoryName); 
            mockPerformanceCounterCategory.Received(1).Delete(testMetricCategoryName);
            mockPerformanceCounterCategory.Received(1).Create(testMetricCategoryName, testMetricCategoryDescription, System.Diagnostics.PerformanceCounterCategoryType.SingleInstance, mockCounterCreationDataCollection);
        }
        
        [Test]
        public void CreatePerformanceCounters_AmountOverTimeUnitMetricAggregate()
        {
            mockCounterCreationDataFactory.Create("MessageBytesReceivedPerSecond", "The number of message bytes received per second", System.Diagnostics.PerformanceCounterType.NumberOfItems64).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataCollection.Add(mockCounterCreationData).Returns<Int32>(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11); 
            mockCounterCreationDataFactory.Create("MessageBytesReceivedPerSecondInstantaneous", "The number of message bytes received per second (instantaneous counter)", System.Diagnostics.PerformanceCounterType.RateOfCountsPerSecond64).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataFactory.Create("MessageBytesReceivedPerMinute", "The number of message bytes received per minute", System.Diagnostics.PerformanceCounterType.NumberOfItems64).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataFactory.Create("MessageBytesReceivedPerMinuteInstantaneous", "The number of message bytes received per minute (instantaneous counter)", System.Diagnostics.PerformanceCounterType.AverageCount64).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataFactory.Create("MessageBytesReceivedPerMinuteInstantaneousBase", "The number of message bytes received per minute (instantaneous base counter)", System.Diagnostics.PerformanceCounterType.AverageBase).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataFactory.Create("MessageBytesReceivedPerHour", "The number of message bytes received per hour", System.Diagnostics.PerformanceCounterType.NumberOfItems64).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataFactory.Create("MessageBytesReceivedPerHourInstantaneous", "The number of message bytes received per hour (instantaneous counter)", System.Diagnostics.PerformanceCounterType.AverageCount64).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataFactory.Create("MessageBytesReceivedPerHourInstantaneousBase", "The number of message bytes received per hour (instantaneous base counter)", System.Diagnostics.PerformanceCounterType.AverageBase).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataFactory.Create("MessageBytesReceivedPerDay", "The number of message bytes received per day", System.Diagnostics.PerformanceCounterType.NumberOfItems64).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataFactory.Create("MessageBytesReceivedPerDayInstantaneous", "The number of message bytes received per day (instantaneous counter)", System.Diagnostics.PerformanceCounterType.AverageCount64).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataFactory.Create("MessageBytesReceivedPerDayInstantaneousBase", "The number of message bytes received per day (instantaneous base counter)", System.Diagnostics.PerformanceCounterType.AverageBase).Returns<ICounterCreationData>(mockCounterCreationData);
            mockPerformanceCounterCategory.Exists(testMetricCategoryName).Returns<Boolean>(true);

            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), TimeUnit.Second, "MessageBytesReceivedPerSecond", "The number of message bytes received per second");
            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), TimeUnit.Minute, "MessageBytesReceivedPerMinute", "The number of message bytes received per minute");
            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), TimeUnit.Hour, "MessageBytesReceivedPerHour", "The number of message bytes received per hour");
            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), TimeUnit.Day, "MessageBytesReceivedPerDay", "The number of message bytes received per day");
            testPerformanceCounterMetricLogger.CreatePerformanceCounters();

            mockCounterCreationDataFactory.Received(1).Create("MessageBytesReceivedPerSecond", "The number of message bytes received per second", System.Diagnostics.PerformanceCounterType.NumberOfItems64);
            mockCounterCreationDataCollection.Received(11).Add(mockCounterCreationData);
            mockCounterCreationDataFactory.Received(1).Create("MessageBytesReceivedPerSecondInstantaneous", "The number of message bytes received per second (instantaneous counter)", System.Diagnostics.PerformanceCounterType.RateOfCountsPerSecond64);
            mockCounterCreationDataFactory.Received(1).Create("MessageBytesReceivedPerMinute", "The number of message bytes received per minute", System.Diagnostics.PerformanceCounterType.NumberOfItems64);
            mockCounterCreationDataFactory.Received(1).Create("MessageBytesReceivedPerMinuteInstantaneous", "The number of message bytes received per minute (instantaneous counter)", System.Diagnostics.PerformanceCounterType.AverageCount64);
            mockCounterCreationDataFactory.Received(1).Create("MessageBytesReceivedPerMinuteInstantaneousBase", "The number of message bytes received per minute (instantaneous base counter)", System.Diagnostics.PerformanceCounterType.AverageBase);
            mockCounterCreationDataFactory.Received(1).Create("MessageBytesReceivedPerHour", "The number of message bytes received per hour", System.Diagnostics.PerformanceCounterType.NumberOfItems64);
            mockCounterCreationDataFactory.Received(1).Create("MessageBytesReceivedPerHourInstantaneous", "The number of message bytes received per hour (instantaneous counter)", System.Diagnostics.PerformanceCounterType.AverageCount64);
            mockCounterCreationDataFactory.Received(1).Create("MessageBytesReceivedPerHourInstantaneousBase", "The number of message bytes received per hour (instantaneous base counter)", System.Diagnostics.PerformanceCounterType.AverageBase);
            mockCounterCreationDataFactory.Received(1).Create("MessageBytesReceivedPerDay", "The number of message bytes received per day", System.Diagnostics.PerformanceCounterType.NumberOfItems64);
            mockCounterCreationDataFactory.Received(1).Create("MessageBytesReceivedPerDayInstantaneous", "The number of message bytes received per day (instantaneous counter)", System.Diagnostics.PerformanceCounterType.AverageCount64);
            mockCounterCreationDataFactory.Received(1).Create("MessageBytesReceivedPerDayInstantaneousBase", "The number of message bytes received per day (instantaneous base counter)", System.Diagnostics.PerformanceCounterType.AverageBase);
            mockPerformanceCounterCategory.Received(1).Exists(testMetricCategoryName);
            mockPerformanceCounterCategory.Received(1).Delete(testMetricCategoryName);
            mockPerformanceCounterCategory.Received(1).Create(testMetricCategoryName, testMetricCategoryDescription, System.Diagnostics.PerformanceCounterCategoryType.SingleInstance, mockCounterCreationDataCollection);
        }
        
        [Test]
        public void CreatePerformanceCounters_AmountOverAmountMetricAggregate()
        {
            mockCounterCreationDataFactory.Create("MessageBytesReceivedPerDiskBytesRead", "The number of message bytes received per disk bytes read", System.Diagnostics.PerformanceCounterType.RawFraction).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataCollection.Add(mockCounterCreationData).Returns<Int32>(1, 2);
            mockCounterCreationDataFactory.Create("MessageBytesReceivedPerDiskBytesReadBase", "The number of message bytes received per disk bytes read (base counter)", System.Diagnostics.PerformanceCounterType.RawBase).Returns<ICounterCreationData>(mockCounterCreationData);
            mockPerformanceCounterCategory.Exists(testMetricCategoryName).Returns<Boolean>(true);

            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), new TestDiskBytesReadMetric(), "MessageBytesReceivedPerDiskBytesRead", "The number of message bytes received per disk bytes read");
            testPerformanceCounterMetricLogger.CreatePerformanceCounters();

            mockCounterCreationDataFactory.Received(1).Create("MessageBytesReceivedPerDiskBytesRead", "The number of message bytes received per disk bytes read", System.Diagnostics.PerformanceCounterType.RawFraction);
            mockCounterCreationDataCollection.Received(2).Add(mockCounterCreationData);
            mockCounterCreationDataFactory.Received(1).Create("MessageBytesReceivedPerDiskBytesReadBase", "The number of message bytes received per disk bytes read (base counter)", System.Diagnostics.PerformanceCounterType.RawBase);
            mockPerformanceCounterCategory.Received(1).Exists(testMetricCategoryName);
            mockPerformanceCounterCategory.Received(1).Delete(testMetricCategoryName);
            mockPerformanceCounterCategory.Received(1).Create(testMetricCategoryName, testMetricCategoryDescription, System.Diagnostics.PerformanceCounterCategoryType.SingleInstance, mockCounterCreationDataCollection);
        }
        
        [Test]
        public void CreatePerformanceCounters_IntervalOverCountMetricAggregate()
        {
            mockCounterCreationDataFactory.Create("ProcessingTimePerMessage", "The average time to process each message", System.Diagnostics.PerformanceCounterType.NumberOfItems64).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataCollection.Add(mockCounterCreationData).Returns<Int32>(1, 2, 3);
            mockCounterCreationDataFactory.Create("ProcessingTimePerMessageInstantaneous", "The average time to process each message (instantaneous counter)", System.Diagnostics.PerformanceCounterType.AverageCount64).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataFactory.Create("ProcessingTimePerMessageInstantaneousBase", "The average time to process each message (instantaneous base counter)", System.Diagnostics.PerformanceCounterType.AverageBase).Returns<ICounterCreationData>(mockCounterCreationData);
            mockPerformanceCounterCategory.Exists(testMetricCategoryName).Returns<Boolean>(true);

            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageProcessingTimeMetric(), new TestMessageReceivedMetric(), "ProcessingTimePerMessage", "The average time to process each message");
            testPerformanceCounterMetricLogger.CreatePerformanceCounters();

            mockCounterCreationDataFactory.Received(1).Create("ProcessingTimePerMessage", "The average time to process each message", System.Diagnostics.PerformanceCounterType.NumberOfItems64);
            mockCounterCreationDataCollection.Received(3).Add(mockCounterCreationData);
            mockCounterCreationDataFactory.Received(1).Create("ProcessingTimePerMessageInstantaneous", "The average time to process each message (instantaneous counter)", System.Diagnostics.PerformanceCounterType.AverageCount64);
            mockCounterCreationDataFactory.Received(1).Create("ProcessingTimePerMessageInstantaneousBase", "The average time to process each message (instantaneous base counter)", System.Diagnostics.PerformanceCounterType.AverageBase);
            mockPerformanceCounterCategory.Received(1).Exists(testMetricCategoryName);
            mockPerformanceCounterCategory.Received(1).Delete(testMetricCategoryName);
            mockPerformanceCounterCategory.Received(1).Create(testMetricCategoryName, testMetricCategoryDescription, System.Diagnostics.PerformanceCounterCategoryType.SingleInstance, mockCounterCreationDataCollection);
        }

        [Test]
        public void CreatePerformanceCounters_IntervalOverTotalRunTimeMetricAggregate()
        {
            mockCounterCreationDataFactory.Create("MessageProcessingTimePercentage", "The amount of time spent processing messages as a percentage of total run time", System.Diagnostics.PerformanceCounterType.RawFraction).Returns<ICounterCreationData>(mockCounterCreationData);
            mockCounterCreationDataCollection.Add(mockCounterCreationData).Returns<Int32>(1, 2);
            mockCounterCreationDataFactory.Create("MessageProcessingTimePercentageBase", "The amount of time spent processing messages as a percentage of total run time (base counter)", System.Diagnostics.PerformanceCounterType.RawBase).Returns<ICounterCreationData>(mockCounterCreationData);
            mockPerformanceCounterCategory.Exists(testMetricCategoryName).Returns<Boolean>(true);

            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageProcessingTimeMetric(), "MessageProcessingTimePercentage", "The amount of time spent processing messages as a percentage of total run time");
            testPerformanceCounterMetricLogger.CreatePerformanceCounters();

            mockCounterCreationDataFactory.Received(1).Create("MessageProcessingTimePercentage", "The amount of time spent processing messages as a percentage of total run time", System.Diagnostics.PerformanceCounterType.RawFraction);
            mockCounterCreationDataCollection.Received(2).Add(mockCounterCreationData);
            mockCounterCreationDataFactory.Received(1).Create("MessageProcessingTimePercentageBase", "The amount of time spent processing messages as a percentage of total run time (base counter)", System.Diagnostics.PerformanceCounterType.RawBase);
            mockPerformanceCounterCategory.Received(1).Exists(testMetricCategoryName);
            mockPerformanceCounterCategory.Received(1).Delete(testMetricCategoryName);
            mockPerformanceCounterCategory.Received(1).Create(testMetricCategoryName, testMetricCategoryDescription, System.Diagnostics.PerformanceCounterCategoryType.SingleInstance, mockCounterCreationDataCollection);
        }

        [Test]
        public void RegisterMetric_AlreadyRegistered()
        {
            ArgumentException e = Assert.Throws<ArgumentException>(delegate
            {
                testPerformanceCounterMetricLogger.RegisterMetric(new TestMessageReceivedMetric());
                testPerformanceCounterMetricLogger.RegisterMetric(new TestMessageReceivedMetric());
            });

            Assert.That(e.Message, Does.StartWith("Metric of type '" + typeof(TestMessageReceivedMetric).Name + "' has already been registered."));
            Assert.AreEqual("metric", e.ParamName);
        }

        [Test]
        public void RegisterMetric_AggregateWithSameNameAlreadyRegistered()
        {
            Exception e = Assert.Throws<Exception>(delegate
            {
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageReceivedMetric(), TimeUnit.Second, new TestDiskReadOperationMetric().Name, "Test description.");
                testPerformanceCounterMetricLogger.RegisterMetric(new TestDiskReadOperationMetric());
            });

            Assert.That(e.Message, Does.StartWith("Metric or metric aggregate with name '" + new TestDiskReadOperationMetric().Name + "' has already been registered."));
        }

        [Test]
        public void DefineMetricAggregate_MetricWithSameNameAlreadyRegistered()
        {
            Exception e = Assert.Throws<Exception>(delegate
            {
                testPerformanceCounterMetricLogger.RegisterMetric(new TestDiskReadOperationMetric());
                testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageReceivedMetric(), TimeUnit.Second, new TestDiskReadOperationMetric().Name, "Test description.");
            });

            Assert.That(e.Message, Does.StartWith("Metric or metric aggregate with name '" + new TestDiskReadOperationMetric().Name + "' has already been registered."));
        }

        [Test]
        public void Dispose()
        {
            mockPerformanceCounterFactory.Create(testMetricCategoryName, new TestDiskReadOperationMetric().Name, false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, new TestMessageReceivedMetric().Name, false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "BytesReceivedPerMessage", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "BytesReceivedPerMessageInstantaneous", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "BytesReceivedPerMessageInstantaneousBase", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockDateTime.UtcNow.Returns<System.DateTime>(new System.DateTime(2014, 7, 3, 21, 20, 39));

            testPerformanceCounterMetricLogger.RegisterMetric(new TestDiskReadOperationMetric());
            testPerformanceCounterMetricLogger.RegisterMetric(new TestMessageReceivedMetric());
            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), new TestMessageReceivedMetric(), "BytesReceivedPerMessage", "The number of bytes received per message");
            testPerformanceCounterMetricLogger.Start();
            testPerformanceCounterMetricLogger.Stop();
            testPerformanceCounterMetricLogger.Dispose();

            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, new TestDiskReadOperationMetric().Name, false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, new TestMessageReceivedMetric().Name, false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "BytesReceivedPerMessage", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "BytesReceivedPerMessageInstantaneous", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "BytesReceivedPerMessageInstantaneousBase", false);
            var throwAway = mockDateTime.Received(1).UtcNow;
            mockPerformanceCounter.Received(5).Dispose();
        }
        
        [Test]
        public void Increment()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 6, 26, 22, 49, 01)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Increment()
                200000L
            );
            mockPerformanceCounterFactory.Create(testMetricCategoryName, (new TestMessageReceivedMetric()).Name, false).Returns<IPerformanceCounter>(mockPerformanceCounter);

            testPerformanceCounterMetricLogger.RegisterMetric(new TestMessageReceivedMetric());
            testPerformanceCounterMetricLogger.Increment(new TestMessageReceivedMetric());
            testPerformanceCounterMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway = mockDateTime.Received(1).UtcNow;
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, (new TestMessageReceivedMetric()).Name, false);
            var throwAway2 = mockStopWatch.Received(1).ElapsedTicks;
            mockPerformanceCounter.Received(1).RawValue = 1L;
        }
        
        [Test]
        public void Add()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 7, 12, 09, 32, 02)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Add()
                200000L
            );
            mockPerformanceCounterFactory.Create(testMetricCategoryName, (new TestMessageBytesReceivedMetric()).Name, false).Returns<IPerformanceCounter>(mockPerformanceCounter);

            testPerformanceCounterMetricLogger.RegisterMetric(new TestMessageBytesReceivedMetric());
            testPerformanceCounterMetricLogger.Add(new TestMessageBytesReceivedMetric(), 1024);
            testPerformanceCounterMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, (new TestMessageBytesReceivedMetric()).Name, false);
            var throwAway2 = mockStopWatch.Received(1).ElapsedTicks;
            mockPerformanceCounter.Received(1).RawValue = 1024L;
        }
        
        [Test]
        public void Set()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 07, 14, 22, 54, 00)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Set()
                100000L, 
                300000L, 
                600000L
            );
            mockPerformanceCounterFactory.Create(testMetricCategoryName, (new TestAvailableMemoryMetric()).Name, false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, (new TestFreeWorkerThreadsMetric()).Name, false).Returns<IPerformanceCounter>(mockPerformanceCounter);

            testPerformanceCounterMetricLogger.RegisterMetric(new TestAvailableMemoryMetric());
            testPerformanceCounterMetricLogger.RegisterMetric(new TestFreeWorkerThreadsMetric());
            testPerformanceCounterMetricLogger.Set(new TestAvailableMemoryMetric(), 80740352);
            testPerformanceCounterMetricLogger.Set(new TestFreeWorkerThreadsMetric(), 8);
            testPerformanceCounterMetricLogger.Set(new TestAvailableMemoryMetric(), 714768384);
            testPerformanceCounterMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, (new TestAvailableMemoryMetric()).Name, false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, (new TestFreeWorkerThreadsMetric()).Name, false);
            var throwAway2 = mockStopWatch.Received(3).ElapsedTicks;
            mockPerformanceCounter.Received(1).RawValue = 714768384L;
            mockPerformanceCounter.Received(1).RawValue = 8L;
        }
       
        [Test]
        public void BeginEnd()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 07, 14, 22, 54, 00)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Begin() and End()
                10000000L,
                32500000L,
                69870000L,
                71230000L,
                1795010000L,
                2412670000L
            );
            mockPerformanceCounterFactory.Create(testMetricCategoryName, (new TestMessageProcessingTimeMetric()).Name, false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, (new TestDiskReadTimeMetric()).Name, false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            

            testPerformanceCounterMetricLogger.RegisterMetric(new TestMessageProcessingTimeMetric());
            testPerformanceCounterMetricLogger.RegisterMetric(new TestDiskReadTimeMetric());
            testPerformanceCounterMetricLogger.Begin(new TestMessageProcessingTimeMetric());
            testPerformanceCounterMetricLogger.Begin(new TestDiskReadTimeMetric());
            testPerformanceCounterMetricLogger.End(new TestDiskReadTimeMetric());
            testPerformanceCounterMetricLogger.End(new TestMessageProcessingTimeMetric());
            testPerformanceCounterMetricLogger.Begin(new TestMessageProcessingTimeMetric());
            testPerformanceCounterMetricLogger.End(new TestMessageProcessingTimeMetric());
            testPerformanceCounterMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, (new TestMessageProcessingTimeMetric()).Name, false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, (new TestDiskReadTimeMetric()).Name, false);
            var throwAway2 = mockStopWatch.Received(6).ElapsedTicks;
            mockPerformanceCounter.Received(1).RawValue = 3737L;
            mockPerformanceCounter.Received(1).RawValue = 67889L;
        }

        [Test]
        public void Start_MetricsNotRegisteredAreNotLogged()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
             (
                 // Returns for calls to Start()
                 new System.DateTime(2014, 6, 26, 22, 49, 55)
             );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Increment() etc...
                10000000L,
                32500000L,
                69870000L
            );
            mockPerformanceCounterFactory.Create(testMetricCategoryName, (new TestMessageReceivedMetric()).Name, false).Returns<IPerformanceCounter>(mockPerformanceCounter);


            testPerformanceCounterMetricLogger.RegisterMetric(new TestMessageReceivedMetric());
            // Tests that below metrics other than TestMessageReceivedMetric are not logged
            testPerformanceCounterMetricLogger.Increment(new TestDiskReadOperationMetric());
            testPerformanceCounterMetricLogger.Add(new TestMessageBytesReceivedMetric(), 1024);
            testPerformanceCounterMetricLogger.Increment(new TestMessageReceivedMetric());
            testPerformanceCounterMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, (new TestMessageReceivedMetric()).Name, false);
            var throwAway2 = mockStopWatch.Received(3).ElapsedTicks;
            mockPerformanceCounter.Received(1).RawValue = 1L;
            mockPerformanceCounter.Received(0).RawValue = 1024L;
        }

        [Test]
        public void LogCountOverTimeUnitAggregate()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
             (
                 // Returns for calls to Start()
                 new System.DateTime(2014, 7, 3, 22, 52, 39, 000)
             );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Increment()
                5000000L,
                15000000L,
                23330000L,
                26660000L
            );
            mockStopWatch.ElapsedMilliseconds.Returns<Int64>
            (
                // Returns for creation of aggregates
                //   ... simulates 4 messages received over elapsed time of 3 seconds (average of 1 message per second)
                3000L,
                //   ... simluates 4 messages received over elapsed time of 2 minutes (average of 2 messages per minute)
                123000L,
                //   ... simluates elapsed time of 0 days, hence no counter values are written
                123000L
            );
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessagesReceivedPerSecond", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessagesReceivedPerSecondInstantaneous", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessagesReceivedPerMinute", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessagesReceivedPerMinuteInstantaneous", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessagesReceivedPerMinuteInstantaneousBase", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessagesReceivedPerDay", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessagesReceivedPerDayInstantaneous", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessagesReceivedPerDayInstantaneousBase", false).Returns<IPerformanceCounter>(mockPerformanceCounter);

            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageReceivedMetric(), TimeUnit.Second, "MessagesReceivedPerSecond", "The number of messages received per second");
            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageReceivedMetric(), TimeUnit.Minute, "MessagesReceivedPerMinute", "The number of messages received per minute");
            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageReceivedMetric(), TimeUnit.Day, "MessagesReceivedPerDay", "The number of messages received per day");
            for (int i = 0; i < 4; i++)
            {
                testPerformanceCounterMetricLogger.Increment(new TestMessageReceivedMetric());
            }
            testPerformanceCounterMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();
            // Wait a few more milliseconds so that any unexpected method calls after the signal are caught
            Thread.Sleep(100);

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessagesReceivedPerSecond", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessagesReceivedPerSecondInstantaneous", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessagesReceivedPerMinute", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessagesReceivedPerMinuteInstantaneous", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessagesReceivedPerMinuteInstantaneousBase", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessagesReceivedPerDay", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessagesReceivedPerDayInstantaneous", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessagesReceivedPerDayInstantaneousBase", false);
            var throwAway2 = mockStopWatch.Received(4).ElapsedTicks;
            throwAway2 = mockStopWatch.Received(3).ElapsedMilliseconds;
            mockPerformanceCounter.Received(1).RawValue = 1L;
            mockPerformanceCounter.Received(2).RawValue = 4L;
            mockPerformanceCounter.Received(2).RawValue = 2L;
        }

        [Test]
        public void LogCountOverTimeUnitAggregate_NoInstances()
        {
            // Tests defining a count over time unit aggregate, where no instances of the underlying count metric have been logged

            mockDateTime.UtcNow.Returns<System.DateTime>
             (
                 // Returns for calls to Start()
                 new System.DateTime(2014, 7, 3, 22, 52, 39, 000)
             );
            mockStopWatch.ElapsedMilliseconds.Returns<Int64>
            (
                // Returns for calls to MetricAggregateLogger.LogCountOverTimeUnitAggregates() and PerformanceCounterMetricLoggerImplementation.LogCountOverTimeUnitAggregate()
                5000L
            );
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessagesReceivedPerSecond", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessagesReceivedPerSecondInstantaneous", false).Returns<IPerformanceCounter>(mockPerformanceCounter);

            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageReceivedMetric(), TimeUnit.Second, "MessagesReceivedPerSecond", "The number of messages received per second");
            testPerformanceCounterMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();
            // Wait a few more milliseconds so that any unexpected method calls after the signal are caught
            Thread.Sleep(100);

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessagesReceivedPerSecond", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessagesReceivedPerSecondInstantaneous", false);
            var throwAway2 = mockStopWatch.Received(1).ElapsedMilliseconds;
            mockPerformanceCounter.Received(2).RawValue = 0L;
        }

        [Test]
        public void LogAmountOverCountAggregate()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 07, 13, 10, 40, 05, 000)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Add(), Increment()
                10260000L,
                10260000L,
                10390000L,
                10390000L
            );
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "BytesReceivedPerMessage", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "BytesReceivedPerMessageInstantaneous", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "BytesReceivedPerMessageInstantaneousBase", false).Returns<IPerformanceCounter>(mockPerformanceCounter);

            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), new TestMessageReceivedMetric(), "BytesReceivedPerMessage", "The number of bytes received per message");
            testPerformanceCounterMetricLogger.Add(new TestMessageBytesReceivedMetric(), 125);
            testPerformanceCounterMetricLogger.Increment(new TestMessageReceivedMetric());
            testPerformanceCounterMetricLogger.Add(new TestMessageBytesReceivedMetric(), 384);
            testPerformanceCounterMetricLogger.Increment(new TestMessageReceivedMetric());
            testPerformanceCounterMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();
            // Wait a few more milliseconds so that any unexpected method calls after the signal are caught
            Thread.Sleep(100);

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "BytesReceivedPerMessage", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "BytesReceivedPerMessageInstantaneous", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "BytesReceivedPerMessageInstantaneousBase", false);
            var throwAway2 = mockStopWatch.Received(4).ElapsedTicks;
            mockPerformanceCounter.Received(1).RawValue = 254L;
            mockPerformanceCounter.Received(1).RawValue = 509L;
            mockPerformanceCounter.Received(1).RawValue = 2L;
        }

        [Test]
        public void LogAmountOverCountAggregate_NoInstances()
        {
            // Tests defining an amount over count aggregate, where no instances of the underlying count metric have been logged

            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 07, 13, 10, 40, 05, 000)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Add()
                10260000L,
                10390000L
            );
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "BytesReceivedPerMessage", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "BytesReceivedPerMessageInstantaneous", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "BytesReceivedPerMessageInstantaneousBase", false).Returns<IPerformanceCounter>(mockPerformanceCounter);

            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), new TestMessageReceivedMetric(), "BytesReceivedPerMessage", "The number of bytes received per message");
            testPerformanceCounterMetricLogger.Add(new TestMessageBytesReceivedMetric(), 125);
            testPerformanceCounterMetricLogger.Add(new TestMessageBytesReceivedMetric(), 384);
            testPerformanceCounterMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();
            // Wait a few more milliseconds so that any unexpected method calls after the signal are caught
            Thread.Sleep(100);

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "BytesReceivedPerMessage", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "BytesReceivedPerMessageInstantaneous", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "BytesReceivedPerMessageInstantaneousBase", false);
            var throwAway2 = mockStopWatch.Received(2).ElapsedTicks;
            mockPerformanceCounter.DidNotReceiveWithAnyArgs().RawValue = 0L;
        }

        [Test]
        public void LogAmountOverTimeUnitAggregateSuccessTest()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
             (
                 // Returns for calls to Start()
                 new System.DateTime(2014, 7, 3, 22, 52, 39, 000)
             );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Add()
                5000000L,
                15000000L,
                23330000L,
                26660000L
            );
            mockStopWatch.ElapsedMilliseconds.Returns<Int64>
            (
                // Returns for creation of aggregates
                //   ... simulates 1118 bytes received over elapsed time of 3 seconds (average of 373 bytes per second)
                3000L,
                //   ... simluates 1118 bytes received over elapsed time of 2 minutes (average of 559 bytes per minute)
                123000L,
                //   ... simluates elapsed time of 0 days, hence no counter values are written
                123000L
            );
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessageBytesReceivedPerSecond", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessageBytesReceivedPerSecondInstantaneous", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessageBytesReceivedPerMinute", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessageBytesReceivedPerMinuteInstantaneous", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessageBytesReceivedPerMinuteInstantaneousBase", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessageBytesReceivedPerDay", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessageBytesReceivedPerDayInstantaneous", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessageBytesReceivedPerDayInstantaneousBase", false).Returns<IPerformanceCounter>(mockPerformanceCounter);

            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), TimeUnit.Second, "MessageBytesReceivedPerSecond", "The number of message bytes received per second");
            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), TimeUnit.Minute, "MessageBytesReceivedPerMinute", "The number of message bytes received per minute");
            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), TimeUnit.Day, "MessageBytesReceivedPerDay", "The number of message bytes received per day");
            testPerformanceCounterMetricLogger.Add(new TestMessageBytesReceivedMetric(), 149);
            testPerformanceCounterMetricLogger.Add(new TestMessageBytesReceivedMetric(), 257);
            testPerformanceCounterMetricLogger.Add(new TestMessageBytesReceivedMetric(), 439);
            testPerformanceCounterMetricLogger.Add(new TestMessageBytesReceivedMetric(), 273);
            testPerformanceCounterMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();
            // Wait a few more milliseconds so that any unexpected method calls after the signal are caught
            Thread.Sleep(100);

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessageBytesReceivedPerSecond", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessageBytesReceivedPerSecondInstantaneous", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessageBytesReceivedPerMinute", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessageBytesReceivedPerMinuteInstantaneous", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessageBytesReceivedPerMinuteInstantaneousBase", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessageBytesReceivedPerDay", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessageBytesReceivedPerDayInstantaneous", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessageBytesReceivedPerDayInstantaneousBase", false);
            var throwAway2 = mockStopWatch.Received(4).ElapsedTicks;
            throwAway2 = mockStopWatch.Received(3).ElapsedMilliseconds;
            mockPerformanceCounter.Received(1).RawValue = 373L;
            mockPerformanceCounter.Received(2).RawValue = 1118L;
            mockPerformanceCounter.Received(1).RawValue = 559L;
            mockPerformanceCounter.Received(1).RawValue = 2L;
        }

        [Test]
        public void LogAmountOverTimeUnitAggregate_NoInstancesSuccess()
        {
            // Tests defining an amount over time unit aggregate, where no instances of the underlying amount metric have been logged

            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 7, 11, 23, 30, 42, 000)
            );
            mockStopWatch.ElapsedMilliseconds.Returns<Int64>
            (
                // Returns for calls to MetricAggregateLogger.LogAmountOverTimeUnitAggregates() and PerformanceCounterMetricLoggerImplementation.LogAmountOverTimeUnitAggregate()
                5000L
            );
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessageBytesReceivedPerSecond", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessageBytesReceivedPerSecondInstantaneous", false).Returns<IPerformanceCounter>(mockPerformanceCounter);

            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), TimeUnit.Second, "MessageBytesReceivedPerSecond", "The number of message bytes received per second");
            testPerformanceCounterMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();
            // Wait a few more milliseconds so that any unexpected method calls after the signal are caught
            Thread.Sleep(100);

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessageBytesReceivedPerSecond", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessageBytesReceivedPerSecondInstantaneous", false);
            var throwAway2 = mockStopWatch.Received(1).ElapsedMilliseconds;
            mockPerformanceCounter.Received(2).RawValue = 0L;
        }

        [Test]
        public void LogAmountOverAmountAggregate()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
                (
                    // Returns for calls to Start()
                    new System.DateTime(2014, 07, 16, 21, 45, 38, 770)
                );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Add()
                7300000L,
                8000000L,
                195630000L,
                198330000L
            );
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessageBytesReceivedPerDiskBytesRead", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessageBytesReceivedPerDiskBytesReadBase", false).Returns<IPerformanceCounter>(mockPerformanceCounter);

            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), new TestDiskBytesReadMetric(), "MessageBytesReceivedPerDiskBytesRead", "The number of message bytes received per disk bytes read");
            testPerformanceCounterMetricLogger.Add(new TestMessageBytesReceivedMetric(), 149);
            testPerformanceCounterMetricLogger.Add(new TestDiskBytesReadMetric(), 257);
            testPerformanceCounterMetricLogger.Add(new TestMessageBytesReceivedMetric(), 439);
            testPerformanceCounterMetricLogger.Add(new TestDiskBytesReadMetric(), 271);
            testPerformanceCounterMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();
            // Wait a few more milliseconds so that any unexpected method calls after the signal are caught
            Thread.Sleep(100);

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessageBytesReceivedPerDiskBytesRead", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessageBytesReceivedPerDiskBytesReadBase", false);
            var throwAway2 = mockStopWatch.Received(4).ElapsedTicks;
            mockPerformanceCounter.Received(1).RawValue = 588L;
            mockPerformanceCounter.Received(1).RawValue = 528L;
        }
        
        [Test]
        public void LogAmountOverAmountAggregate_NoInstances()
        {
            // Tests defining an amount over amount aggregate, where no instances of the underlying denominator amount metric have been logged

            mockDateTime.UtcNow.Returns<System.DateTime>
             (
                 // Returns for calls to Start()
                 new System.DateTime(2014, 07, 16, 21, 45, 38, 770)
             );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Add()
                7300000L,
                195630000L
            );
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessageBytesReceivedPerDiskBytesRead", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessageBytesReceivedPerDiskBytesReadBase", false).Returns<IPerformanceCounter>(mockPerformanceCounter);

            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(), new TestDiskBytesReadMetric(), "MessageBytesReceivedPerDiskBytesRead", "The number of message bytes received per disk bytes read");
            testPerformanceCounterMetricLogger.Add(new TestMessageBytesReceivedMetric(), 149);
            testPerformanceCounterMetricLogger.Add(new TestMessageBytesReceivedMetric(), 439);
            testPerformanceCounterMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();
            // Wait a few more milliseconds so that any unexpected method calls after the signal are caught
            Thread.Sleep(100);

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessageBytesReceivedPerDiskBytesRead", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessageBytesReceivedPerDiskBytesReadBase", false);
            var throwAway2 = mockStopWatch.Received(2).ElapsedTicks;
            mockPerformanceCounter.DidNotReceiveWithAnyArgs().RawValue = 0L;
        }

        [Test]
        public void LogIntervalOverCountAggregate()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
             (
                 // Returns for calls to Start()
                 new System.DateTime(2014, 07, 16, 23, 01, 16, 999)
             );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Begin(), End(), and Increment()
                10000L,
                1210000L,
                1210000L,
                28510000L,
                39760000L,
                39810000L
            );
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "ProcessingTimePerMessage", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "ProcessingTimePerMessageInstantaneous", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "ProcessingTimePerMessageInstantaneousBase", false).Returns<IPerformanceCounter>(mockPerformanceCounter);

            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageProcessingTimeMetric(), new TestMessageReceivedMetric(), "ProcessingTimePerMessage", "The average time to process each message");
            testPerformanceCounterMetricLogger.Begin(new TestMessageProcessingTimeMetric());
            testPerformanceCounterMetricLogger.End(new TestMessageProcessingTimeMetric());
            testPerformanceCounterMetricLogger.Increment(new TestMessageReceivedMetric());
            testPerformanceCounterMetricLogger.Begin(new TestMessageProcessingTimeMetric());
            testPerformanceCounterMetricLogger.End(new TestMessageProcessingTimeMetric());
            testPerformanceCounterMetricLogger.Increment(new TestMessageReceivedMetric());
            testPerformanceCounterMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();
            // Wait a few more milliseconds so that any unexpected method calls after the signal are caught
            Thread.Sleep(100);

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "ProcessingTimePerMessage", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "ProcessingTimePerMessageInstantaneous", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "ProcessingTimePerMessageInstantaneousBase", false);
            var throwAway2 = mockStopWatch.Received(6).ElapsedTicks;
            mockPerformanceCounter.Received(1).RawValue = 622L;
            mockPerformanceCounter.Received(1).RawValue = 1245L;
            mockPerformanceCounter.Received(1).RawValue = 2L;
        }

        [Test]
        public void LogIntervalOverCountAggregate_NoInstances()
        {
            // Tests defining an interval over count aggregate, where no instances of the underlying count metric have been logged

            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 07, 16, 23, 01, 16, 999)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Begin(), and End()
                10000L,
                1210000L,
                28510000L,
                39760000L
            );
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "ProcessingTimePerMessage", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "ProcessingTimePerMessageInstantaneous", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "ProcessingTimePerMessageInstantaneousBase", false).Returns<IPerformanceCounter>(mockPerformanceCounter);

            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageProcessingTimeMetric(), new TestMessageReceivedMetric(), "ProcessingTimePerMessage", "The average time to process each message");
            testPerformanceCounterMetricLogger.Begin(new TestMessageProcessingTimeMetric());
            testPerformanceCounterMetricLogger.End(new TestMessageProcessingTimeMetric());
            testPerformanceCounterMetricLogger.Begin(new TestMessageProcessingTimeMetric());
            testPerformanceCounterMetricLogger.End(new TestMessageProcessingTimeMetric());
            testPerformanceCounterMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();
            // Wait a few more milliseconds so that any unexpected method calls after the signal are caught
            Thread.Sleep(100);

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "ProcessingTimePerMessage", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "ProcessingTimePerMessageInstantaneous", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "ProcessingTimePerMessageInstantaneousBase", false);
            var throwAway2 = mockStopWatch.Received(4).ElapsedTicks;
            mockPerformanceCounter.DidNotReceiveWithAnyArgs().RawValue = 0L;
        }

        [Test]
        public void LogIntervalOverTotalRunTimeAggregate()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 07, 19, 17, 33, 50, 000)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Add()
                10000000L,
                17890000L,
                20580000L,
                60320000L
            );
            mockStopWatch.ElapsedMilliseconds.Returns<Int64>
            (
                // Returns for creation of aggregates
                6300L
            );
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessageProcessingTimePercentage", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessageProcessingTimePercentageBase", false).Returns<IPerformanceCounter>(mockPerformanceCounter);

            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageProcessingTimeMetric(), "MessageProcessingTimePercentage", "The amount of time spent processing messages as a percentage of total run time");
            testPerformanceCounterMetricLogger.Begin(new TestMessageProcessingTimeMetric());
            testPerformanceCounterMetricLogger.End(new TestMessageProcessingTimeMetric());
            testPerformanceCounterMetricLogger.Begin(new TestMessageProcessingTimeMetric());
            testPerformanceCounterMetricLogger.End(new TestMessageProcessingTimeMetric());
            testPerformanceCounterMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();
            // Wait a few more milliseconds so that any unexpected method calls after the signal are caught
            Thread.Sleep(100);

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessageProcessingTimePercentage", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessageProcessingTimePercentageBase", false);
            var throwAway2 = mockStopWatch.Received(4).ElapsedTicks;
            throwAway2 = mockStopWatch.Received(1).ElapsedMilliseconds;
            mockPerformanceCounter.Received(1).RawValue = 4763L;
            mockPerformanceCounter.Received(1).RawValue = 6300L;
        }

        [Test]
        public void LogIntervalOverTotalRunTimeAggregate_ZeroElapsedTime()
        {
            // Tests that an aggregate is not logged when no time has elapsed

            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 07, 19, 17, 33, 50, 000)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Add()
                10000000L,
                17890000L,
                20580000L,
                60320000L
            );
            mockStopWatch.ElapsedMilliseconds.Returns<Int64>
            (
                // Returns for creation of aggregates
                0
            );
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessageProcessingTimePercentage", false).Returns<IPerformanceCounter>(mockPerformanceCounter);
            mockPerformanceCounterFactory.Create(testMetricCategoryName, "MessageProcessingTimePercentageBase", false).Returns<IPerformanceCounter>(mockPerformanceCounter);


            testPerformanceCounterMetricLogger.DefineMetricAggregate(new TestMessageProcessingTimeMetric(), "MessageProcessingTimePercentage", "The amount of time spent processing messages as a percentage of total run time");
            testPerformanceCounterMetricLogger.Begin(new TestMessageProcessingTimeMetric());
            testPerformanceCounterMetricLogger.End(new TestMessageProcessingTimeMetric());
            testPerformanceCounterMetricLogger.Begin(new TestMessageProcessingTimeMetric());
            testPerformanceCounterMetricLogger.End(new TestMessageProcessingTimeMetric());
            testPerformanceCounterMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();
            // Wait a few more milliseconds so that any unexpected method calls after the signal are caught
            Thread.Sleep(100);

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessageProcessingTimePercentage", false);
            mockPerformanceCounterFactory.Received(1).Create(testMetricCategoryName, "MessageProcessingTimePercentageBase", false);
            var throwAway2 = mockStopWatch.Received(4).ElapsedTicks;
            mockPerformanceCounter.DidNotReceiveWithAnyArgs().RawValue = 0L;
        }
    }
}
