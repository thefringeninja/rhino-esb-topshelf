using System;
using System.Linq;
using System.Threading;
using Castle.MicroKernel;
using Rhino.ServiceBus.Hosting;
using Rhino.ServiceBus.Impl;
using Topshelf.Configuration.Dsl;
using Topshelf.Shelving;
using Xunit;

namespace Rhino.ServiceBus.Topshelf.Tests
{
	public class HostingRhinoQueuesWithTopshelf
		: HostingWithTopshelf<RhinoQueuesHostedService>
	{
		protected override void ConfigureBus(DefaultHost host)
		{
			RhinoQueuesHostedService.StartWithPort(22001);
			host.BusConfiguration(
			                      c =>
			                      c.Bus("rhino.queues://localhost:22002/rhino_servicebus_topshelf_tests_pongconsumer", "pongconsumer")
			                      	.Receive("Rhino.ServiceBus.Topshelf.Tests.PingMessage",
			                      	         "rhino-queues://localhost:22001/rhino_servicebus_topshelf_tests_pingconsumer"));
		}
	}

	public class HostingMSMQWithTopshelf
		: HostingWithTopshelf<MSMQHostedService>
	{
		protected override void ConfigureBus(DefaultHost host)
		{
			host.BusConfiguration(c => c.Bus("msmq://localhost/rhino.servicebus.topshelf.tests.pongconsumer")
			                           	.Receive("Rhino.ServiceBus.Topshelf.Tests.PingMessage",
			                           	         "msmq://localhost/rhino.servicebus.topshelf.tests.pingconsumer"));
		}
	}

	public abstract class HostingWithTopshelf<THost> : IDisposable
		where THost : HostedService, new()
	{
		private readonly DefaultHost host;
		private readonly Bootstrapper<IApplicationHost> pingService;

		protected HostingWithTopshelf()
		{
			ServiceAppDomain.Set(AppDomain.CurrentDomain);
			pingService = new PingBootstrapConsumer();
			host = new DefaultHost();
			ConfigureBus(host);
			host.Start<PongBootstrapper>();
		}

		protected abstract void ConfigureBus(DefaultHost host);

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

					Assert.True(PongConsumer.Wait.WaitOne(TimeSpan.FromSeconds(5)));
				}

				finally
				{
					runner.Coordinator.Stop();
					PongConsumer.Wait = new ManualResetEvent(false);
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

					PongConsumer.Wait.WaitOne(TimeSpan.FromSeconds(5));
					Assert.Equal(12, PongConsumer.RetryCount);
				}

				finally
				{
					runner.Coordinator.Stop();
					PongConsumer.Wait = new ManualResetEvent(false);
				}
			}
		}

		#region Nested type: PingBootstrapConsumer

		[Serializable]
		public class PingBootstrapConsumer : BootstrapConsumer<PingConsumer, THost, TestBootStrapper>
		{
		}

		#endregion

		public void Dispose()
		{
			host.Container.Dispose();
		}
	}

	
	public class PingMessage
	{
	}

	public class PongBootstrapper : AbstractBootStrapper
	{
		protected override bool IsTypeAcceptableForThisBootStrapper(Type t)
		{
			return t == typeof (PongConsumer);
		}
	}


	public class PongConsumer : ConsumerOf<PongMessage>
	{
		public static int RetryCount;
		public static ManualResetEvent Wait = new ManualResetEvent(false);

		#region ConsumerOf<PongMessage> Members

		public void Consume(PongMessage message)
		{
			RetryCount = message.NumberOfRetries;
			Wait.Set();
		}

		#endregion
	}


	public class PongMessage
	{
		public int NumberOfRetries { get; set; }
	}


	public class TestBootStrapper : AbstractBootStrapper
	{
		protected override bool IsTypeAcceptableForThisBootStrapper(Type t)
		{
			return t == typeof (PingConsumer);
		}
	}

	public class PingConsumer : ConsumerOf<PingMessage>
	{
		private static int numberOfRetries;
		private readonly IServiceBus bus;

		public PingConsumer(IServiceBus bus, IKernel kernel)
		{
			this.bus = bus;
			var facility = kernel.GetFacilities().OfType<AbstractRhinoServiceBusFacility>()
				.First();
			numberOfRetries = facility.NumberOfRetries;
		}

		#region ConsumerOf<PingMessage> Members

		public void Consume(PingMessage message)
		{
			bus.Send(new PongMessage
			{
				NumberOfRetries = numberOfRetries
			});
		}

		#endregion
	}
}