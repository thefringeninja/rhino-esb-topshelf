using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.ServiceBus.Hosting;
using Rhino.ServiceBus.Internal;
using Topshelf.Configuration.Dsl;
using Topshelf.Shelving;

namespace Rhino.ServiceBus.Topshelf
{
	public abstract class BootstrapConsumer<TMessageConsumer>
		: BootstrapConsumer<TMessageConsumer, NoOpBootStrapper>
		where TMessageConsumer : class, IMessageConsumer
	{

	}

	public abstract class BootstrapConsumer<TMessageConsumer, TBootStrapper>
		: Bootstrapper<DefaultHost>
		where TMessageConsumer : class, IMessageConsumer
		where TBootStrapper : AbstractBootStrapper
	{
		protected virtual IEnumerable<Type> MessageConsumerImplementations
		{
			get
			{
				return from type in typeof(TMessageConsumer).Assembly.GetTypes()
					   where typeof(IMessageConsumer).IsAssignableFrom(type)
							 && false == type.IsInterface
							 && false == type.IsAbstract
					   select type;
			}
		}

		#region Bootstrapper<DefaultHost> Members

		public void InitializeHostedService(IServiceConfigurator<DefaultHost> cfg)
		{
			cfg.HowToBuildService(name =>
			{
				var host = new DefaultHost();
				host.BusConfiguration(BusConfiguration);
				return host;
			});
			cfg.WhenStarted(host => host.Start<TBootStrapper>());
			cfg.WhenStopped(host => host.Dispose());
		}

		#endregion

		protected virtual HostConfiguration BusConfiguration(HostConfiguration configuration)
		{
			configuration.Bus(BuildEndpointForMessageConsumer(typeof(TMessageConsumer)));
			var consumerTypes = from type in MessageConsumerImplementations
								from iface in type.GetInterfaces()
								where iface.IsGenericType
									  && false == iface.IsGenericTypeDefinition
									  && iface.GetGenericTypeDefinition() == typeof(ConsumerOf<>)
								select new
								{
									message = iface.GetGenericArguments()[0].FullName,
									implementation = type
								};
			foreach (var item in consumerTypes)
			{
				configuration.Receive(item.message, BuildEndpointForMessageConsumer(item.implementation));
			}
			return configuration;
		}

		protected virtual string BuildEndpointForMessageConsumer(Type type)
		{
			return "msmq://localhost/" + type.FullName;
		}
	}
}
