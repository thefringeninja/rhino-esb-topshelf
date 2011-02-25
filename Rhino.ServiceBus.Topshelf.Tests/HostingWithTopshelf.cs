using System;
using System.Threading;
using Castle.Windsor;
using Rhino.ServiceBus.Hosting;
using Topshelf.Configuration.Dsl;
using Topshelf.Shelving;
using Xunit;

namespace Rhino.ServiceBus.Topshelf.Tests
{
	public class HostingWithTopshelf
	{
		private readonly Bootstrapper<DefaultHost> pingService;
		private readonly DefaultHost host;
		public HostingWithTopshelf()
		{
			pingService = new PingBootstrapConsumer();
			wait = new ManualResetEvent(false);
			host = new DefaultHost();
			host.BusConfiguration(c => c.Bus("msmq://localhost/rhino.servicebus.topshelf.tests.hostingwithtopshelf_pongconsumer")
				.Receive("Rhino.ServiceBus.Topshelf.Tests.HostingWithTopshelf+PingMessage", "msmq://localhost/rhino.servicebus.topshelf.tests.hostingwithtopshelf_pingconsumer"));
			host.Start<PongBootstrapper>();
		}

		private static ManualResetEvent wait = new ManualResetEvent(false);

		[Fact]
		public void Can_set_the_default_endpoint_by_convention()
		{
			var runner = RunnerConfigurator.New(c => c.ConfigureService<DefaultHost>(pingService.InitializeHostedService));
			using (runner.Coordinator)
			{
				try
				{
					runner.Coordinator.Start();

					var bus = host.Container.Resolve<IServiceBus>();
					bus.Send(new PingMessage());

					Assert.True(wait.WaitOne(TimeSpan.FromSeconds(5)));
				}

				finally
				{
					runner.Coordinator.Stop();
				}
			}
		}

		public class TestBootStrapper : AbstractBootStrapper
		{
			protected override bool IsTypeAcceptableForThisBootStrapper(Type t)
			{
				return t == typeof (PingConsumer);
			}
		}

		public class PingMessage
		{

		}
		public class PongMessage { }
		public class PingConsumer : ConsumerOf<PingMessage>
		{
			private readonly IServiceBus bus;

			public PingConsumer(IServiceBus bus)
			{
				this.bus = bus;
			}

			public void Consume(PingMessage message)
			{
				bus.Send(new PongMessage());
			}
		}
		public class PongConsumer : ConsumerOf<PongMessage>
		{
			public void Consume(PongMessage message)
			{
				wait.Set();
			}
		}

		public class PingBootstrapConsumer : BootstrapConsumer<PingConsumer, TestBootStrapper>
		{


		}

		public class PongBootstrapper : AbstractBootStrapper
		{
			protected override bool IsTypeAcceptableForThisBootStrapper(Type t)
			{
				return t == typeof (PongConsumer);
			}
		}
	}
}
