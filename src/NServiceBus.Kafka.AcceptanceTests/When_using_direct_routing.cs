﻿namespace NServiceBus.Transport.Kafka.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Kafka;

    public class When_using_direct_routing : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_receive_the_message()
        {
            var directory = TestContext.CurrentContext.TestDirectory;

            var context = await Scenario.Define<MyContext>()
                   .WithEndpoint<Receiver>(
                                                b => b.When((bus, c) => bus.SendLocal(new MyRequest())
                                                ))
                   .Done(c => c.GotTheMessage)
                   .Run();

            Assert.True(context.GotTheMessage, "Should receive the message");
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c => c.UseTransport<KafkaTransport>().ConnectionString("127.0.0.1:9092"));
            }

            public class MyEventHandler : IHandleMessages<MyRequest>
            {
                readonly MyContext myContext;

                public MyEventHandler(MyContext myContext)
                {
                    this.myContext = myContext;
                }

                public Task Handle(MyRequest message, IMessageHandlerContext context)
                {
                    myContext.GotTheMessage = true;

                    return TaskEx.CompletedTask;
                }
            }
        }

        public class MyRequest : IMessage
        {
        }

        public class MyContext : ScenarioContext
        {
            public bool GotTheMessage { get; set; }
        }
    }
}