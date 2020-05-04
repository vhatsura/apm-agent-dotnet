using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace Elastic.Apm.AspNetCore.Tests
{
	public class CustomWebApplicationFactory<TStartup>
		: WebApplicationFactory<TStartup> where TStartup : class
	{
		protected override IWebHostBuilder CreateWebHostBuilder() =>
			new WebHostBuilder().UseContentRoot(@"D:\Projects\github\apm-agent-dotnet\test\Elastic.Apm.AspNetCore.Tests"); //.UseSolutionRelativeContentRoot("test/Elastic.Apm.AspNetCore.Tests");
	}
}
