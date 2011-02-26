using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Rhino.ServiceBus.Hosting;
using Topshelf;
using Topshelf.Configuration.Dsl;
using Topshelf.Shelving;

namespace Rhino.ServiceBus.Topshelf
{
	public class HostingRhinoESB
	{
		public static void Start(string[] args)
		{
			var cfg = RunnerConfigurator.New(c =>
			{
				c.RunAsLocalSystem();

				var bootStrapperTypes = from type in Assembly.GetCallingAssembly().GetTypes()
				                        where type.IsGenericallyAssignableFrom(typeof (BootstrapConsumer<>))
				                              && false == type.IsAbstract
				                              && false == type.IsInterface
				                              && type.GetConstructor(Type.EmptyTypes) != null
				                        select type;

				foreach (var bootStrapperType in bootStrapperTypes)
				{
					var bootStrapper = (Bootstrapper<DefaultHost>) (bootStrapperType.GetConstructor(Type.EmptyTypes).IsPublic
					                                                	? Activator.CreateInstance(bootStrapperType)
					                                                	: Activator.CreateInstance(bootStrapperType, true));

					c.ConfigureService<DefaultHost>(bootStrapper.InitializeHostedService);
				}
			});
			Runner.Host(cfg, args);
		}
	}
}
