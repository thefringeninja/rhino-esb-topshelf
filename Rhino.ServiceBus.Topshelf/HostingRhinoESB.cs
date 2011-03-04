using System;
using System.Linq;
using System.Reflection;
using Rhino.ServiceBus.Hosting;
using Topshelf;
using Topshelf.Configuration.Dsl;
using Topshelf.Shelving;

namespace Rhino.ServiceBus.Topshelf
{
	public class HostingRhinoESB
	{
		public static void Start(Assembly assemblyToScan, string[] args)
		{
			if (args != null && args.Length > 0 && args[0].ToLowerInvariant() == "install")
			{
				Host(InitialDeployment, assemblyToScan, args);
			}
			else
			{
				Host(InitializeHostedService, assemblyToScan, args);
			}
		}

		private static void Host(Action<IRunnerConfigurator, Bootstrapper<IApplicationHost>> action, Assembly assemblyToScan, string[] args)
		{
			var cfg = RunnerConfigurator.New(c =>
			{
				ServiceAppDomain.Set(AppDomain.CurrentDomain);
				c.RunAsLocalSystem();
				var types = assemblyToScan.GetTypes();
				var bootStrapperTypes = from type in types
				                        where typeof (BootstrapConsumer<,,>).IsGenericallyAssignableFrom(type)
				                              && false == type.IsAbstract
				                              && false == type.IsInterface
				                              && type.GetConstructor(Type.EmptyTypes) != null
				                        select type;

				foreach (var bootStrapperType in bootStrapperTypes)
				{
					var bootStrapper = (Bootstrapper<IApplicationHost>)Activator.CreateInstance(bootStrapperType);
					action(c, bootStrapper);
				}
			});
			Runner.Host(cfg, args);
		}

		static void InitializeHostedService(IRunnerConfigurator c, Bootstrapper<IApplicationHost> bootstrapper)
		{
			c.ConfigureService<IApplicationHost>(bootstrapper.InitializeHostedService);
		}

		static void InitialDeployment(IRunnerConfigurator c, Bootstrapper<IApplicationHost> bootstrapper)
		{
			bootstrapper.GetType().GetMethod("InitialDeployment").Invoke(bootstrapper, null);
		}
	}
}
