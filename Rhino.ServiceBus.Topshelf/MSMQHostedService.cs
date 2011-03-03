namespace Rhino.ServiceBus.Topshelf
{
	public class MSMQHostedService : HostedService
	{
		protected override string Protocol
		{
			get { return "msmq"; }
		}
	}
}