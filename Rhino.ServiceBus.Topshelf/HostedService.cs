using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Rhino.ServiceBus.Hosting;
using Rhino.ServiceBus.Internal;

namespace Rhino.ServiceBus.Topshelf
{
	public class HostedService : MarshalByRefObject, IApplicationHost
	{
		private static readonly char[] invalidQueueNameCharacters = new[] {'\\', ';', '\r', '\n', '+', ',', '"'};
		private readonly DefaultHost host;
		protected Type MessageConsumerType { get; private set; }
		protected string StandaloneConfigurationFilename { get; private set; }

		public HostedService()
		{
			host = new DefaultHost();
		}

		protected virtual IEnumerable<Type> MessageConsumerImplementations
		{
			get
			{
				return from type in MessageConsumerType.Assembly.GetTypes()
				       where typeof (IMessageConsumer).IsAssignableFrom(type)
				             && false == type.IsInterface
				             && false == type.IsAbstract
				       select type;
			}
		}

		#region IApplicationHost Members

		public void Dispose()
		{
			host.Dispose();
		}

		public void Start(string assembly)
		{
			host.Start(assembly);
		}

		public void InitialDeployment(string assembly, string user)
		{
			host.InitialDeployment(assembly, user);
		}

		public void SetBootStrapperTypeName(string typeName)
		{
			host.SetBootStrapperTypeName(typeName);
		}

		#endregion

		public void SetMessageConsumerType(string typeName)
		{
			MessageConsumerType = Type.GetType(typeName);
		}

		public void UseStandaloneCastleConfigurationFileName(string standaloneConfigurationFilename)
		{
			StandaloneConfigurationFilename = standaloneConfigurationFilename;
			host.UseStandaloneCastleConfigurationFileName(StandaloneConfigurationFilename);
		}

		public void Configure()
		{
			host.BusConfiguration(BusConfiguration);
		}

		protected HostConfiguration PatchWithExistingConfiguration(HostConfiguration configuration,
		                                                           string standaloneConfigurationFilename)
		{
			if (String.IsNullOrEmpty(standaloneConfigurationFilename))
				return configuration;
			var file = new FileInfo(standaloneConfigurationFilename);
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
			using (var reader = file.OpenText())
			{
				var element = (from e in XDocument.Load(reader).Descendants("facility")
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

		protected virtual HostConfiguration BusConfiguration(HostConfiguration configuration)
		{
			configuration.Bus(BuildEndpointForMessageConsumer(MessageConsumerType));
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

			configuration = PatchWithExistingConfiguration(configuration, StandaloneConfigurationFilename);
			return configuration;
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