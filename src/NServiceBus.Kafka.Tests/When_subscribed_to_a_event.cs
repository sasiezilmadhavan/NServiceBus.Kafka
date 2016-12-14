﻿namespace NServiceBus.Transport.Kafka.Tests
{
    using System.Threading.Tasks;
    using Extensibility;
    using NUnit.Framework;
    using Transport;
    using Transport.Kafka.Tests;
    using System;
    using System.Linq;
    using System.Runtime.Serialization.Formatters.Binary;
    using System.IO;
    using System.Collections.Generic;
    using Routing;

    [TestFixture]
    class When_subscribed_to_a_event : KafkaContext
    {
       

        [Test]
        public async Task Should_receive_published_events_of_that_type()
        {
            base.SetUp(typeof(MyEvent));

            await Publish<MyEvent>();

            var receivedMessages = ReceiveMessages(10, 5);

            Assert.IsTrue( receivedMessages.Any(m => m.Headers["NServiceBus.CorrelationId"] == typeof(MyEvent).FullName));


        }

        [Test]
        public async Task Should_receive_the_event_if_subscribed_to_the_base_class()
        {
            base.SetUp(typeof(EventBase));
            await Publish<SubEvent1>();
            await Publish<SubEvent2>();


            var receivedMessages = ReceiveMessages(10, 5);
            Assert.IsTrue(receivedMessages.Any(m => m.Headers["NServiceBus.CorrelationId"] == typeof(SubEvent1).FullName));
            Assert.IsTrue(receivedMessages.Any(m => m.Headers["NServiceBus.CorrelationId"] == typeof(SubEvent2).FullName));
        }

        [Test]
        public async Task Should_not_receive_the_event_if_subscribed_to_the_specific_class()
        {
            base.SetUp(typeof(SubEvent1));            

            await Publish<EventBase>();

            var receivedMessages = ReceiveMessages(10, 5);
            Assert.IsFalse(receivedMessages.Any(m => m.Headers["NServiceBus.CorrelationId"] == typeof(EventBase).FullName));
        }

        [Test]
        public async Task Should_receive_the_event_if_subscribed_to_the_base_interface()
        {
            base.SetUp(typeof(IMyEvent));          

            await Publish<MyEvent1>();
            await Publish<MyEvent2>();

            var receivedMessages = ReceiveMessages(10, 5);
            Assert.IsTrue(receivedMessages.Any(m => m.Headers["NServiceBus.CorrelationId"] == typeof(MyEvent1).FullName));
            Assert.IsTrue(receivedMessages.Any(m => m.Headers["NServiceBus.CorrelationId"] == typeof(MyEvent2).FullName));
           
        }

        [Test]
        public async Task Should_not_receive_the_event_if_subscribed_to_specific_interface()
        {
           
            base.SetUp(typeof(MyEvent1));

            await Publish<IMyEvent>();

            var receivedMessages = ReceiveMessages(10, 5);
            Assert.IsFalse(receivedMessages.Any(m => m.Headers["NServiceBus.CorrelationId"] == typeof(IMyEvent).FullName));
          
        }

        [Test]
        public async Task Should_not_receive_events_of_other_types()
        {
            base.SetUp(typeof(MyEvent1));
            

            //publish a event that that this publisher isn't subscribed to
            await Publish<MyOtherEvent>();
            await Publish<MyEvent>();

            var receivedMessages = ReceiveMessages(10, 5);
            Assert.IsTrue(receivedMessages.Any(m => m.Headers["NServiceBus.CorrelationId"] == typeof(MyEvent).FullName));
            Assert.IsFalse(receivedMessages.Any(m => m.Headers["NServiceBus.CorrelationId"] == typeof(MyOtherEvent).FullName));
          
        }

        [Test]
        public async Task Subscribing_to_IEvent_should_subscribe_to_all_published_messages()
        {
            
            base.SetUp(typeof(IEvent));

            await Publish<MyOtherEvent>();
            await Publish<MyEvent>();

            var receivedMessages = ReceiveMessages(10, 5);
            Assert.IsTrue(receivedMessages.Any(m => m.Headers["NServiceBus.CorrelationId"] == typeof(MyOtherEvent).FullName));
            Assert.IsTrue(receivedMessages.Any(m => m.Headers["NServiceBus.CorrelationId"] == typeof(MyEvent).FullName));
            
        }

        [Test]
        public async Task Subscribing_to_Object_should_subscribe_to_all_published_messages()
        {
            base.SetUp(typeof(object));
            

            await Publish<MyOtherEvent>();
            await Publish<MyEvent>();

            var receivedMessages = ReceiveMessages(10, 5);
            Assert.IsTrue(receivedMessages.Any(m => m.Headers["NServiceBus.CorrelationId"] == typeof(MyOtherEvent).FullName));
            Assert.IsTrue(receivedMessages.Any(m => m.Headers["NServiceBus.CorrelationId"] == typeof(MyEvent).FullName));
            
        }

        [Test]
        public async Task Subscribing_to_a_class_implementing_a_interface_should_only_give_the_concrete_class()
        {            
            base.SetUp(typeof(CombinedClassAndInterface));

            await Publish<CombinedClassAndInterface>();
            await Publish<IMyEvent>();

            var receivedMessages = ReceiveMessages(10, 5);
            Assert.IsTrue(receivedMessages.Any(m => m.Headers["NServiceBus.CorrelationId"] == typeof(CombinedClassAndInterface).FullName));
            Assert.IsFalse(receivedMessages.Any(m => m.Headers["NServiceBus.CorrelationId"] == typeof(IMyEvent).FullName));

        }

        [Test]
        public async Task Subscribing_to_a_interface_that_is_implemented_be_a_class_should_give_the_event_if_the_class_is_published()
        {           
            base.SetUp(typeof(IMyEvent));

            await Publish<CombinedClassAndInterface>();
            await Publish<IMyEvent>();

            var receivedMessages = ReceiveMessages(10, 5);
            Assert.IsTrue(receivedMessages.Any(m => m.Headers["NServiceBus.CorrelationId"] == typeof(CombinedClassAndInterface).FullName));
            Assert.IsTrue(receivedMessages.Any(m => m.Headers["NServiceBus.CorrelationId"] == typeof(IMyEvent).FullName));
        }

        [Test]
        public async Task Should_not_receive_events_after_unsubscribing()
        {
           // await Subscribe<MyEvent>();

            await subscriptionManager.Unsubscribe(typeof(MyEvent), new ContextBag());

            //publish a event that that this publisher isn't subscribed to
            await Publish<MyEvent>();

            AssertNoEventReceived();
        }

        Task Subscribe<T>() => subscriptionManager.Subscribe(typeof(T), new ContextBag());

        Task Publish<T>() where T : class, new()
        {
            var type = typeof(T);
            var m = new T();

            var message = new OutgoingMessageBuilder().WithBody(ObjectToByteArray(m)).CorrelationId(type.FullName).PublishType(type).Build();

            messageDispatcher.Dispatch(message, new TransportTransaction(), new ContextBag()).Wait();

            return Task.FromResult(0);
        }

        void AssertReceived<T>()
        {
            var receivedMessage = ReceiveMessage();

            AssertReceived<T>(receivedMessage.Result);
        }

        void AssertReceived<T>(IncomingMessage receivedEvent)
        {
            Assert.AreEqual(typeof(T).FullName, receivedEvent.Headers[Headers.CorrelationId]);
        }

        void AssertNoEventReceived()
        {
            var messageWasReceived = TryWaitForMessageReceipt();

            Assert.False(messageWasReceived);
        }

        byte[] ObjectToByteArray(object obj)
        {
            if (obj == null)
                return null;
            BinaryFormatter bf = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream())
            {
                bf.Serialize(ms, obj);
                return ms.ToArray();
            }
        }
    }


    [Serializable]
    public class MyOtherEvent
    {
    }

    [Serializable]
    public class MyEvent : IMessage 
    {
    }

    [Serializable]
    public class EventBase : IEvent
    {

    }

    [Serializable]
    public class SubEvent1 : EventBase
    {

    }

    [Serializable]
    public class SubEvent2 : EventBase
    {

    }

    [Serializable]
    public class IMyEvent : IEvent
    {

    }

    [Serializable]
    public class MyEvent1 : IMyEvent
    {

    }

    [Serializable]
    public class MyEvent2 : IMyEvent
    {

    }

    [Serializable]
    public class CombinedClassAndInterface : IMyEvent
    {

    }
}