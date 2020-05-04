using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Elastic.Apm.Api;
using Elastic.Apm.Config;
using Elastic.Apm.DiagnosticSource;
using Elastic.Apm.EntityFrameworkCore;
using Elastic.Apm.Logging;
using Elastic.Apm.Tests.Mocks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Xunit;
using Xunit.Abstractions;

#if NETCOREAPP3_0 || NETCOREAPP3_1

namespace Elastic.Apm.AspNetCore.Tests
{
	public class AspNetCoreMiddlewareTests_EndpointRoute : IClassFixture<CustomWebApplicationFactory<EndpointRouteStartup>>
	{
		private const string ThisClassName = nameof(AspNetCoreMiddlewareTests);
		private readonly ApmAgent _agent;
		private readonly MockPayloadSender _capturedPayload;
		private readonly WebApplicationFactory<EndpointRouteStartup> _factory;

		private HttpClient _client;

		// ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
		private readonly IApmLogger _logger;

		public AspNetCoreMiddlewareTests_EndpointRoute(CustomWebApplicationFactory<EndpointRouteStartup> factory, ITestOutputHelper xUnitOutputHelper
		) //: base(xUnitOutputHelper)
		{
			//_logger = LoggerBase.Scoped(ThisClassName);
			_factory = factory;

			_agent = new ApmAgent(new TestAgentComponents(
				_logger,
				new MockConfigSnapshot(_logger, captureBody: ConfigConsts.SupportedValues.CaptureBodyAll),
				// _agent needs to share CurrentExecutionSegmentsContainer with Agent.Instance
				// because the sample application used by the tests (SampleAspNetCoreApp) uses Agent.Instance.Tracer.CurrentTransaction/CurrentSpan
				currentExecutionSegmentsContainer: Agent.Instance.TracerInternal.CurrentExecutionSegmentsContainer)
			);
			ApmMiddlewareExtension.UpdateServiceInformation(_agent.Service);

			_capturedPayload = _agent.PayloadSender as MockPayloadSender;
			_client = _factory.WithWebHostBuilder(builder =>
					builder
						.UseSolutionRelativeContentRoot("test/Elastic.Apm.AspNetCore.Tests")
						.Configure(c => c.UseElasticApm(_agent, _agent.Logger, new HttpDiagnosticsSubscriber(), new EfCoreDiagnosticsSubscriber())))
				.CreateClient();
#if NETCOREAPP3_0 || NETCOREAPP3_1
			_client.DefaultRequestVersion = new Version(2, 0);
#endif
		}

		/// <summary>
		/// Simulates an HTTP GET call to /home/simplePage and asserts on what the agent should send to the server
		/// </summary>
		[Fact]
		public async Task HomeSimplePageTransactionTest()
		{
			var headerKey = "X-Additional-Header";
			var headerValue = "For-Elastic-Apm-Agent";
			_client.DefaultRequestHeaders.Add(headerKey, headerValue);
			var response = await _client.GetAsync("/api");

			//test service
			_capturedPayload.Transactions.Should().ContainSingle();

			_agent.Service.Name.Should()
				.NotBeNullOrWhiteSpace()
				.And.NotBe(ConfigConsts.DefaultValues.UnknownServiceName);

			_agent.Service.Agent.Name.Should().Be(Apm.Consts.AgentName);
			var apmVersion = typeof(Agent).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
			_agent.Service.Agent.Version.Should().Be(apmVersion);

			_agent.Service.Framework.Name.Should().Be("ASP.NET Core");

			var aspNetCoreVersion = Assembly.Load("Microsoft.AspNetCore").GetName().Version.ToString();
			_agent.Service.Framework.Version.Should().Be(aspNetCoreVersion);

			_agent.Service.Runtime.Name.Should().Be(Runtime.DotNetCoreName);
			_agent.Service.Runtime.Version.Should().Be(Directory.GetParent(typeof(object).Assembly.Location).Name);

			_capturedPayload.Transactions.Should().ContainSingle();
			var transaction = _capturedPayload.FirstTransaction;
			var transactionName = $"{response.RequestMessage.Method} Home/SimplePage";
			transaction.Name.Should().Be(transactionName);
			transaction.Result.Should().Be("HTTP 2xx");
			transaction.Duration.Should().BeGreaterThan(0);

			transaction.Type.Should().Be("request");
			transaction.Id.Should().NotBeEmpty();

			//test transaction.context.response
			transaction.Context.Response.StatusCode.Should().Be(200);
			if (_agent.ConfigurationReader.CaptureHeaders)
			{
				transaction.Context.Response.Headers.Should().NotBeNull();
				transaction.Context.Response.Headers.Should().NotBeEmpty();

				transaction.Context.Response.Headers.Should().ContainKeys(headerKey);
				transaction.Context.Response.Headers[headerKey].Should().Be(headerValue);
			}

			//test transaction.context.request
#if NETCOREAPP3_0 || NETCOREAPP3_1
			transaction.Context.Request.HttpVersion.Should().Be("2");
#else
			transaction.Context.Request.HttpVersion.Should().Be("2.0");
#endif
			transaction.Context.Request.Method.Should().Be("GET");

			//test transaction.context.request.url
			transaction.Context.Request.Url.Full.Should().Be(response.RequestMessage.RequestUri.AbsoluteUri);
			transaction.Context.Request.Url.HostName.Should().Be("localhost");
			transaction.Context.Request.Url.Protocol.Should().Be("HTTP");

			if (_agent.ConfigurationReader.CaptureHeaders)
			{
				transaction.Context.Request.Headers.Should().NotBeNull();
				transaction.Context.Request.Headers.Should().NotBeEmpty();

				transaction.Context.Request.Headers.Should().ContainKeys(headerKey);
				transaction.Context.Request.Headers[headerKey].Should().Be(headerValue);
			}

			//test transaction.context.request.encrypted
			transaction.Context.Request.Socket.Encrypted.Should().BeFalse();
		}
	}
}

#endif
