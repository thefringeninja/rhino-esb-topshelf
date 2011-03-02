using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Rhino.ServiceBus.Hosting;
using Rhino.ServiceBus.Internal;
using Topshelf.Configuration.Dsl;
using Topshelf.Shelving;

namespace Rhino.ServiceBus.Topshelf
{
	public abstract class BootstrapConsumer<TMessageConsumer>
		: BootstrapConsumer<TMessageConsumer, HostedService, NoOpBootStrapper>
		where TMessageConsumer : class, IMessageConsumer
	{
	}

	[Serializable]
	public abstract class BootstrapConsumer<TMessageConsumer, THost, TBootStrapper>
		: Bootstrapper<IApplicationHost>
		where TMessageConsumer : class, IMessageConsumer
		where TBootStrapper : AbstractBootStrapper
		where THost : HostedService
	{
		public virtual string StandaloneConfigurationFilename
		{
			get
			{
				var fileName = typeof (TMessageConsumer).Name + ".config";
				return false == File.Exists(fileName) ? null : fileName;
			}
		}

		public void InitialDeployment()
		{
			var appDomain = AppDomain.CreateDomain(typeof (TMessageConsumer).Name, null,
			                                       new AppDomainSetup
			                                       {
			                                       	ApplicationBase = AppDomain.CurrentDomain.BaseDirectory
			                                       });
			try
			{
				using (var host = BuildAndConfigureHost(appDomain))
				{
					host.InitialDeployment(typeof(TMessageConsumer).Assembly.FullName, Thread.CurrentPrincipal.Identity.Name);
				}
			}
			finally
			{
				AppDomain.Unload(appDomain);
			}
		}

		#region Bootstrapper<IApplicationHost> Members

		public void InitializeHostedService(IServiceConfigurator<IApplicationHost> cfg)
		{
			var appDomain = AppDomain.CreateDomain(typeof(TMessageConsumer).Name, null, 
				new AppDomainSetup
			{
				ApplicationBase = AppDomain.CurrentDomain.BaseDirectory
			});

			cfg.HowToBuildService(name => BuildAndConfigureHost(appDomain));
			cfg.WhenStarted(host =>
			{
				host.SetBootStrapperTypeName(typeof (TBootStrapper).FullName);
				host.Start(typeof (TBootStrapper).Assembly.FullName);
			});
			cfg.WhenStopped(host =>
			{
				host.Dispose();
				AppDomain.Unload(appDomain);
			});
		}

		private HostedService BuildAndConfigureHost(AppDomain appDomain)
		{
			var host = (HostedService) appDomain
			                           	.CreateInstanceAndUnwrap(typeof (THost).Assembly.FullName,
			                           	                         typeof (THost).FullName);
			host.SetMessageConsumerType(typeof(TMessageConsumer).AssemblyQualifiedName);
			host.UseStandaloneCastleConfigurationFileName(StandaloneConfigurationFilename);
			host.Configure();
			return host;
		}

		#endregion

	}
}