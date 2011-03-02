using System;
using System.Linq;
using System.Threading;
using Castle.Core.Configuration;
using Castle.MicroKernel;
using Castle.Windsor;
using Rhino.ServiceBus.Config;
using Rhino.ServiceBus.Hosting;
using Rhino.ServiceBus.Impl;
using Topshelf.Configuration.Dsl;
using Topshelf.Model;
using Topshelf.Shelving;
using Xunit;

namespace Rhino.ServiceBus.Topshelf.Tests
{
	public class HostingWithTopshelf
	{
		private readonly Bootstrapper<IApplicationHost> pingService;
		private readonly DefaultHost host;

		private static int retryCount;

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
			var runner = RunnerConfigurator.New(c => c.ConfigureService<IApplicationHost>(pingService.InitializeHostedService));
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

		[Fact]
		public void Can_load_configuration_file_by_convention()
		{
			var runner = RunnerConfigurator.New(c => c.ConfigureService<IApplicationHost>(pingService.InitializeHostedService));
			using (runner.Coordinator)
			{
				try
				{
					runner.Coordinator.Start();

					var bus = host.Container.Resolve<IServiceBus>();
					bus.Send(new PingMessage());

					wait.WaitOne(TimeSpan.FromSeconds(5));
					Assert.Equal(12, retryCount);
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
		public class PongMessage
		{
			public int NumberOfRetries { get; set; }
		}
		public class PingConsumer : ConsumerOf<PingMessage>
		{
			private readonly IServiceBus bus;
			private static int numberOfRetries;

			public PingConsumer(IServiceBus bus, IKernel kernel)
			{
				this.bus = bus;
				var facility = kernel.GetFacilities().OfType<AbstractRhinoServiceBusFacility>()
					.First();
				numberOfRetries = facility.NumberOfRetries;
			}

			public void Consume(PingMessage message)
			{
				bus.Send(new PongMessage
				{
					NumberOfRetries = numberOfRetries
				});
			}
		}
		public class PongConsumer : ConsumerOf<PongMessage>
		{
			public void Consume(PongMessage message)
			{
				retryCount = message.NumberOfRetries;
				wait.Set();

			}
		}

		[Serializable]
		public class PingBootstrapConsumer : BootstrapConsumer<PingConsumer, HostedService, TestBootStrapper>{}

		public class PongBootstrapper : AbstractBootStrapper
		{
			protected override bool IsTypeAcceptableForThisBootStrapper(Type t)
			{
				return t == typeof (PongConsumer);
			}
		}
	}
}
