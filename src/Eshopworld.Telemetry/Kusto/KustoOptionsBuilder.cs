﻿using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading;
using Eshopworld.Core;
using Microsoft.ApplicationInsights;

namespace Eshopworld.Telemetry.Kusto
{
    public class KustoOptionsBuilder
    {
        internal readonly IDestinationDispatcher Dispatcher;
        internal readonly Subject<BaseEvent> TelemetryStream;
        internal readonly Metric IngestionTimeMetric;
        internal readonly ISubject<TelemetryEvent> ErrorStream;
        private KustoDbDetails _dbDetails;

        public KustoOptionsBuilder(IDestinationDispatcher dispatcher, Subject<BaseEvent> telemetryStream, Metric ingestionTimeMetric, ISubject<TelemetryEvent> errorStream)
        {
            Dispatcher = dispatcher;
            TelemetryStream = telemetryStream;
            IngestionTimeMetric = ingestionTimeMetric;
            ErrorStream = errorStream;
        }
        
        public KustoOptionsBuilder UseCluster(string engine, string region, string database, string tenantId)
        { 
            _dbDetails = new KustoDbDetails { ClientId = tenantId, DbName = database, Engine = engine, Region = region };

            Dispatcher.Initialise(_dbDetails);

            return this;
        }
    }

    public static class QueuedClientBuilderExtensions
    {
        private static QueuedIngestionStrategy _queuedIngestionStrategy;

        private static void Check(KustoOptionsBuilder builder, CancellationToken token, int bufferInterval, int maxBufferItems)
        {
            if (_queuedIngestionStrategy == null)
            {
                _queuedIngestionStrategy = new QueuedIngestionStrategy(token, bufferInterval, maxBufferItems);
                builder.Dispatcher.AddStrategy(_queuedIngestionStrategy);
            }
        }

        public static KustoOptionsBuilder UseQueuedIngestion<T>(this KustoOptionsBuilder builder, CancellationToken token, int bufferInterval = 1000, int maxBufferItems = 50)
            where  T:TelemetryEvent
        {
            Check(builder, token, bufferInterval, maxBufferItems);
            builder.Dispatcher.Subscribe<T, QueuedIngestionStrategy>(builder.TelemetryStream, builder.IngestionTimeMetric, builder.ErrorStream);
            return builder;
        }

        public static void OnMessageSent(this KustoOptionsBuilder builder, Action<int> onSent)
        {
            _queuedIngestionStrategy.OnMessageSent += x => onSent(x);
        }
    }
}