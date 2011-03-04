using System;
using System.Collections.Generic;

namespace Rhino.ServiceBus.Topshelf
{
	public class RhinoQueuesHostedService : HostedService
	{
		protected override string Protocol
		{
			get { return "rhino.queues"; }
		}

		public static void StartWithPort(int port)
		{
			Singleton.Instance.StartWithPort(port);
		}

		protected override Uri BuildEndpointForMessageConsumer(Type type)
		{
			var uri = base.BuildEndpointForMessageConsumer(type);
			Singleton.Instance.AssignPort(ref uri);
			return uri;
		}

		protected override string MakeQueueNameSafe(string queueName)
		{
			return base.MakeQueueNameSafe(queueName).Replace('.', '_');
		}

		#region Nested type: Singleton

		protected class Singleton : MarshalByRefObject
		{
			private static Singleton instance;
			private readonly IDictionary<Uri, int> portAssignment = new Dictionary<Uri, int>();
			private int lastPort = 2200;

			private static AppDomain ServiceAppDomain
			{
				get { return Topshelf.ServiceAppDomain.Instance; }
			}

			public static Singleton Instance
			{
				get
				{
					if (instance != null)
						return instance;

					var targetAppDomain = ServiceAppDomain;
					var type = typeof (Singleton);
					var singleton = targetAppDomain.GetData(type.FullName) as Singleton;
					if (singleton == null)
					{
						singleton = (Singleton) targetAppDomain
						                        	.CreateInstanceAndUnwrap(type.Assembly.FullName,
						                        	                         type.FullName);
						targetAppDomain.SetData(type.FullName, singleton);
					}

					instance = singleton;
					return instance;
				}
			}

			public IDictionary<Uri, int> PortAssignment
			{
				get { return portAssignment; }
			}

			public void StartWithPort(int port)
			{
				lastPort = port;
			}

			public void AssignPort(ref Uri queue)
			{
				int port;
				if (false == PortAssignment.TryGetValue(queue, out port))
				{
					port = lastPort++;
					PortAssignment.Add(queue, port);
				}

				var builder = new UriBuilder(queue) {Port = port};

				queue = builder.Uri;
			}
		}

		#endregion
	}
}