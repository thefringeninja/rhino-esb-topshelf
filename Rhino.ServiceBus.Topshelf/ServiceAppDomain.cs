using System;
using System.Collections.Generic;
using System.Linq;

namespace Rhino.ServiceBus.Topshelf
{
	public static class ServiceAppDomain
	{
		public static void Set(AppDomain instance, AppDomain target = null)
		{
			(target ?? AppDomain.CurrentDomain).SetData("ServiceAppDomain", instance);
		}
		public static AppDomain Instance
		{
			get { return (AppDomain) AppDomain.CurrentDomain.GetData("ServiceAppDomain"); }
		}
	}
}