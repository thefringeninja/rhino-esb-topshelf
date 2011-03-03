using System;
using System.IO;
using System.Threading;
using Rhino.ServiceBus.Hosting;
using Rhino.ServiceBus.Internal;
using Topshelf.Configuration.Dsl;
using Topshelf.Shelving;

namespace Rhino.ServiceBus.Topshelf
{
	public abstract class BootstrapConsumer<TMessageConsumer>
		: BootstrapConsumer<TMessageConsumer, MSMQHostedService, NoOpBootStrapper>
		where TMessageConsumer : class, IMessageConsumer
	{
	}

	[Serializable]
	public abstract class BootstrapConsumer<TMessageConsumer, THost, TBootStrapper>
		: Bootstrapper<IApplicationHost>
		where TMessageConsumer : class, IMessageConsumer
		where TBootStrapper : AbstractBootStrapper
		where THost : HostedService, new()
	{
		public virtual string StandaloneConfigurationFilename
		{
			get
			{
				var fileName = typeof (TMessageConsumer).Name + ".config";
				return false == File.Exists(fileName) ? null : fileName;
			}
		}

		#region Bootstrapper<IApplicationHost> Members

		public void InitializeHostedService(IServiceConfigurator<IApplicationHost> cfg)
		{
			var appDomain = CreateAppDomain();

			cfg.HowToBuildService(name => BuildAndConfigureHost(appDomain));
			cfg.WhenStarted(host =>
			{
				var mutex = new Mutex(false, "Huh");
				try
				{
					mutex.WaitOne();
					host.SetBootStrapperTypeName(typeof (TBootStrapper).FullName);
					host.Start(typeof (TBootStrapper).Assembly.FullName);
				}
				finally
				{
					mutex.ReleaseMutex();
				}
			});
			cfg.WhenStopped(host => host.Dispose());
		}

		#endregion

		public void InitialDeployment()
		{
			var appDomain = CreateAppDomain();
			using (var host = BuildAndConfigureHost(appDomain))
			{
				host.SetBootStrapperTypeName(typeof (TBootStrapper).FullName);
				host.InitialDeployment(typeof (TMessageConsumer).Assembly.FullName, Thread.CurrentPrincipal.Identity.Name);
			}
		}

		private AppDomain CreateAppDomain()
		{
			var appDomain = AppDomain.CreateDomain(typeof (TMessageConsumer).Name, null,
			                                       new AppDomainSetup
			                                       {
			                                       	ApplicationBase = AppDomain.CurrentDomain.BaseDirectory,
			                                       	LoaderOptimization = LoaderOptimization.MultiDomain
			                                       });
			ServiceAppDomain.Set(ServiceAppDomain.Instance, appDomain);
			return appDomain;
		}

		private HostedService BuildAndConfigureHost(AppDomain appDomain)
		{
			var host = (HostedService) appDomain
			                           	.CreateInstanceAndUnwrap(typeof (THost).Assembly.FullName,
			                           	                         typeof (THost).FullName);
			host.SetMessageConsumerType(typeof (TMessageConsumer).AssemblyQualifiedName);
			host.UseStandaloneCastleConfigurationFileName(StandaloneConfigurationFilename);
			host.Configure();
			return host;
		}
	}
}