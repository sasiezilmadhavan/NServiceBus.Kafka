﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NServiceBus.Routing;

using NServiceBus.Settings;
using NServiceBus.Kafka.Sending;
using NServiceBus.Performance.TimeToBeReceived;
using NServiceBus.Transport.Kafka.Receiving;
using NServiceBus.Transports.Kafka.Wrapper;
using NServiceBus.Serialization;
using NServiceBus.Transports.Kafka.Administration;
using NServiceBus.Transports.Kafka.Connection;

namespace NServiceBus.Transport.Kafka
{


    class KafkaTransportInfrastructure : TransportInfrastructure, IDisposable
    {
        readonly SettingsHolder settings;
        readonly string connectionString;
        private static MessageWrapperSerializer serializer;
        static Object o = new Object();
        
        MessagePump  messagePump;
        MessageDispatcher messageDispatcher;
        QueueCreator queueCreator;
        SubscriptionManager subscriptionManager;

        public KafkaTransportInfrastructure(SettingsHolder settings, string connectionString)
        {
            this.settings = settings;
            this.connectionString = connectionString;
            serializer = BuildSerializer(settings);
            this.queueCreator = new QueueCreator(settings);

            messagePump = new MessagePump(settings,connectionString);
            messageDispatcher = new MessageDispatcher(new Transports.Kafka.Connection.ProducerFactory(connectionString));
            subscriptionManager = new SubscriptionManager(messagePump, queueCreator);
        }

        public override TransportReceiveInfrastructure ConfigureReceiveInfrastructure()
        {
            return new TransportReceiveInfrastructure(() => messagePump, 
                () => queueCreator, 
                () => Task.FromResult(StartupCheckResult.Success));
        }

        public override TransportSendInfrastructure ConfigureSendInfrastructure()
        {
            return new TransportSendInfrastructure(() => messageDispatcher, () => Task.FromResult(StartupCheckResult.Success));
        }


        public override IEnumerable<Type> DeliveryConstraints => new[] { typeof(DiscardIfNotReceivedBefore) };

        public override OutboundRoutingPolicy OutboundRoutingPolicy
        {
            get
            {
                return new OutboundRoutingPolicy(
                    sends: OutboundRoutingType.Unicast,
                    publishes: OutboundRoutingType.Multicast,
                    replies: OutboundRoutingType.Unicast);
            }
        }

        public override TransportTransactionMode TransactionMode
        {
            get
            {
                return TransportTransactionMode.ReceiveOnly;
            }
        }

        public override EndpointInstance BindToLocalEndpoint(EndpointInstance instance)
        {
            return instance;
        }

      
        public override TransportSubscriptionInfrastructure ConfigureSubscriptionInfrastructure()
        {
            return new TransportSubscriptionInfrastructure(() => subscriptionManager);
        }

        public override string ToTransportAddress(LogicalAddress logicalAddress)
        {
            var topic = new StringBuilder(logicalAddress.EndpointInstance.Endpoint);
                       
            if (logicalAddress.Qualifier != null)
            {
                topic.Append("." + logicalAddress.Qualifier);
            }

            return topic.ToString();
        }

        static MessageWrapperSerializer BuildSerializer(ReadOnlySettings settings)
        {
            if (serializer == null)
            {
                lock (o)
                {
                    if (serializer == null)
                    {
                        SerializationDefinition wrapperSerializer;
                        if (settings!=null && settings.TryGet(WellKnownConfigurationKeys.MessageWrapperSerializationDefinition, out wrapperSerializer))
                        {
                            serializer = new MessageWrapperSerializer(wrapperSerializer.Configure(settings)(MessageWrapperSerializer.GetMapper()));
                        }

                        serializer = new MessageWrapperSerializer(KafkaTransport.GetMainSerializer(MessageWrapperSerializer.GetMapper(), settings));
                    }
                }
            }


            return serializer;
        }

        internal static MessageWrapperSerializer GetSerializer()
        {
            return serializer;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls
       

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    messagePump.Dispose();
                    
                }

             

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
