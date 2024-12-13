using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using NBitcoin;
using NBitcoin.RPC;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using Microsoft.AspNetCore.Http.Timeouts;
using WalletWasabi.Backend.Middlewares;
using WalletWasabi.BitcoinCore;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Blockchain.BlockFilters;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Cache;
using WalletWasabi.Discoverability;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Interfaces;
using WalletWasabi.Logging;
using WalletWasabi.Userfacing;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.DoSPrevention;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Backend.Statistics;
using WalletWasabi.WabiSabi.Models.Serialization;
using WalletWasabi.WebClients;

[assembly: ApiController]

namespace WalletWasabi.Backend;

public class Startup
{
	public Startup(IConfiguration configuration)
	{
		Configuration = configuration;
	}

	public IConfiguration Configuration { get; }

	// This method gets called by the runtime. Use this method to add services to the container.
	public void ConfigureServices(IServiceCollection services)
	{
		string dataDir = Configuration["datadir"] ?? EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Backend"));
		Logger.InitializeDefaults(Path.Combine(dataDir, "Logs.txt"));

		services.AddMemoryCache();

		services.AddMvc(options =>
			{
				options.ModelMetadataDetailsProviders.Add(new SuppressChildValidationMetadataProvider(typeof(BitcoinAddress)));
				options.ModelMetadataDetailsProviders.Add(new SuppressChildValidationMetadataProvider(typeof(Script)));
			})
			.AddControllersAsServices();

		services.AddMvc()
			.AddNewtonsoftJson();

		services.AddControllers().AddNewtonsoftJson(x => x.SerializerSettings.Converters = JsonSerializationOptions.Default.Settings.Converters);

		// Register the Swagger generator, defining one or more Swagger documents
		services.AddSwaggerGen(c =>
		{
			c.SwaggerDoc(
				$"v{Constants.BackendMajorVersion}",
				new OpenApiInfo
				{
					Version = $"v{Constants.BackendMajorVersion}",
					Title = "Wasabi Wallet API",
					Description = "Privacy focused Bitcoin Web API.",
					License = new OpenApiLicense { Name = "Use under MIT.", Url = new Uri("https://github.com/WalletWasabi/WalletWasabi/blob/master/LICENSE.md") }
				});

			// Set the comments path for the Swagger JSON and UI.
			var basePath = AppContext.BaseDirectory;
			var xmlPath = Path.Combine(basePath, "WalletWasabi.Backend.xml");
			c.IncludeXmlComments(xmlPath);
		});

		services.AddSingleton<IExchangeRateProvider>(new ExchangeRateProvider());
		string configFilePath = Path.Combine(dataDir, "Config.json");
		Config config = new(configFilePath);
		config.LoadFile(createIfMissing: true);
		services.AddSingleton(serviceProvider => config );

		services.AddSingleton<IdempotencyRequestCache>();
		services.AddHttpClient("no-name").ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
		{
			// See https://github.com/dotnet/runtime/issues/18348#issuecomment-415845645
			PooledConnectionLifetime = TimeSpan.FromMinutes(5)
		});
		services.AddSingleton<IRPCClient>(provider =>
		{
			string host = config.GetBitcoinCoreRpcEndPoint().ToString(config.Network.RPCPort);
			RPCClient rpcClient = new(
					authenticationString: config.BitcoinRpcConnectionString,
					hostOrUri: host,
					network: config.Network);

			IMemoryCache memoryCache = provider.GetRequiredService<IMemoryCache>();
			CachedRpcClient cachedRpc = new(rpcClient, memoryCache);
			return cachedRpc;
		});

		var network = config.Network;

		services.AddSingleton(_ => network);
		WabiSabiConfig wabisabiConfig = new(Path.Combine(dataDir, "WabiSabiConfig.json"));
		wabisabiConfig.LoadFile(createIfMissing: true);
		services.AddSingleton(wabisabiConfig);
		services.AddSingleton<Prison>(s => s.GetRequiredService<Warden>().Prison);
		services.AddSingleton<Warden>(s =>
			new Warden(
				Path.Combine(dataDir, "WabiSabi", "Prison.txt"),
				s.GetRequiredService<WabiSabiConfig>()));
		services.AddSingleton<CoinJoinFeeRateStatStore>(s =>
			CoinJoinFeeRateStatStore.LoadFromFile(
				Path.Combine(dataDir, "WabiSabi", "CoinJoinFeeRateStatStore.txt"),
				s.GetRequiredService<WabiSabiConfig>(),
				s.GetRequiredService<IRPCClient>()
				));
		services.AddSingleton<RoundParameterFactory>();
		services.AddBackgroundService<Arena>();
		services.AddBackgroundService<BlockNotifier>();

		services.AddSingleton<AnnouncerConfig>(_ => config.AnnouncerConfig);
		services.AddBackgroundService<CoordinatorAnnouncer>();

		services.AddSingleton<MempoolService>();
		services.AddSingleton<P2pNode>(s =>
			new P2pNode(
				network,
				config.GetBitcoinP2pEndPoint(),
				s.GetRequiredService<MempoolService>()));
		services.AddSingleton<IdempotencyRequestCache>();
		services.AddSingleton<IndexBuilderService>(s =>
			new IndexBuilderService(
				s.GetRequiredService<IRPCClient>(),
				s.GetRequiredService<BlockNotifier>(),
				Path.Combine(dataDir, "IndexBuilderService", $"Index{network}.sqlite")
				));
		services.AddStartupTask<StartupTask>();
		services.AddResponseCompression();
		services.AddRequestTimeouts(options =>
			options.DefaultPolicy =
				new RequestTimeoutPolicy
				{
					Timeout = TimeSpan.FromSeconds(5)
				});
	}

	[SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "This method gets called by the runtime. Use this method to configure the HTTP request pipeline")]
	public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
	{
		// Enable middleware to serve generated Swagger as a JSON endpoint.
		app.UseSwagger();

		// Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.), specifying the Swagger JSON endpoint.
		app.UseSwaggerUI(c => c.SwaggerEndpoint($"/swagger/v{Constants.BackendMajorVersion}/swagger.json", $"Wasabi Wallet API V{Constants.BackendMajorVersion}"));

		app.UseRouting();

		// So to correctly handle HEAD requests.
		// https://www.tpeczek.com/2017/10/exploring-head-method-behavior-in.html
		// https://github.com/tpeczek/Demo.AspNetCore.Mvc.CosmosDB/blob/master/Demo.AspNetCore.Mvc.CosmosDB/Middlewares/HeadMethodMiddleware.cs
		app.UseMiddleware<HeadMethodMiddleware>();

		app.UseResponseCompression();

		app.UseEndpoints(endpoints => endpoints.MapControllers());
		app.UseRequestTimeouts();
	}
}
