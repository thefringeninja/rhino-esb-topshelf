using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
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
		private static readonly char[] invalidQueueNameCharacters = new[] {'\\', ';', '\r', '\n', '+', ',', '"'};

		public virtual string StandaloneConfigurationFilename
		{
			get { return typeof (TMessageConsumer).Name + ".config"; }
		}

		protected virtual IEnumerable<Type> MessageConsumerImplementations
		{
			get
			{
				return from type in typeof (TMessageConsumer).Assembly.GetTypes()
				       where typeof (IMessageConsumer).IsAssignableFrom(type)
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
				host.UseStandaloneCastleConfigurationFileName(StandaloneConfigurationFilename);
				host.BusConfiguration(BusConfiguration);
				return host;
			});
			cfg.WhenStarted(host => host.Start<TBootStrapper>());
			cfg.WhenStopped(host => host.Dispose());
		}

		#endregion

		protected virtual HostConfiguration BusConfiguration(HostConfiguration configuration)
		{
			configuration.Bus(BuildEndpointForMessageConsumer(typeof (TMessageConsumer)));
			var consumerTypes = from type in MessageConsumerImplementations
			                    from iface in type.GetInterfaces()
			                    where iface.IsGenericType
			                          && false == iface.IsGenericTypeDefinition
			                          && iface.GetGenericTypeDefinition() == typeof (ConsumerOf<>)
			                    select new
			                    {
			                    	message = iface.GetGenericArguments()[0].FullName,
			                    	implementation = type
			                    };
			foreach (var item in consumerTypes)
			{
				configuration.Receive(item.message, BuildEndpointForMessageConsumer(item.implementation));
			}

			configuration = PatchWithExistingConfiguration(configuration);
			return configuration;
		}

		protected HostConfiguration PatchWithExistingConfiguration(HostConfiguration configuration)
		{
			var file = new FileInfo(StandaloneConfigurationFilename);
			if (false == file.Exists)
				return configuration;

			var attributes = GetConfigurationNode(file)
				.ToDictionary(a => a.Name.LocalName, a => a.Value);

			if (attributes.ContainsKey("numberOfRetries"))
				configuration = configuration.Retries(Convert.ToInt32(attributes["numberOfRetries"]));
			if (attributes.ContainsKey("threadCount"))
				configuration = configuration.Threads(Convert.ToInt32(attributes["threadCount"]));
			if (attributes.ContainsKey("loadBalancerEndpoint"))
				configuration = configuration.LoadBalancer(attributes["loadBalancerEndpoint"]);
			if (attributes.ContainsKey("logEndpoint"))
				configuration = configuration.Logging(attributes["logEndpoint"]);

			return configuration;
		}

		private static IEnumerable<XAttribute> GetConfigurationNode(FileInfo file)
		{
			using (var stream = file.OpenRead())
			{
				var element = (from e in XDocument.Load(stream).Descendants("facility")
				               where (string) e.Attribute("id") == "rhino.esb"
				               select e).FirstOrDefault();
				if (element == null)
					yield break;
				var bus = element.Element("bus");
				if (bus == null)
					yield break;
				foreach (var a in bus.Attributes())
					yield return a;
			}
		}

		protected string BuildEndpointForMessageConsumer(Type type)
		{
			return "msmq://localhost/" + MakeTypeNameSafeForQueue(type);
		}

		private string MakeTypeNameSafeForQueue(Type type)
		{
			return invalidQueueNameCharacters.Aggregate(type.FullName, (name, c) => name.Replace(c, '_'));
		}
	}
}