using System;
using System.Linq;

namespace Rhino.ServiceBus.Topshelf
{
	public static class TypeExtensions
	{
		public static bool IsGenericallyAssignableFrom(this Type genericType, Type givenType)
		{
			var interfaceTypes = givenType.GetInterfaces();

			if (interfaceTypes.Where(it => it.IsGenericType).Any(it => it.GetGenericTypeDefinition() == genericType))
			{
				return true;
			}

			var baseType = givenType.BaseType;
			if (baseType == null)
			{
				return false;
			}

			if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == genericType)
				return true;
			return genericType.IsGenericallyAssignableFrom(baseType);
		}
	}
}