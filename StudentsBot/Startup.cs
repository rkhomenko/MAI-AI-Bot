using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.BotFramework;
using Microsoft.Bot.Builder.Core.Extensions;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder.TraceExtensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using MAIAIBot.Core;

namespace MAIAIBot.StudentsBot
{
    public class Startup
    {
        private const string CosmosDbConnectionStrIndex = "CosmosDb";
        private const string CosmosDbKeyIndex = "CosmosDb:Key";
        private const string AzureStorageSasTokenIndex = "AzureStorage:SasToken";
        private const string AzureStorageConnectionStringIndex = "AzureStorage:ConnectionString";
        private const string AzureStorageShareName = "mai-ai-bot-photo";
        private const string AzureStorageImagePrefix = "maiaibotphoto-";
        private const string CognitiveServiceConnectionStrIndex = "CognitiveService";
        private const string CognitiveServiceKeyIndex = "CognitiveService:Key";

        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json")
                .AddUserSecrets<Startup>()
                .AddEnvironmentVariables();

            Configuration = builder.Build();
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<ApplicationConfiguration>(Configuration);

            services.AddMvc();

            services.AddTransient<IDatabaseProvider>(serviceProvider => {
                var connectionString = Configuration.GetConnectionString(CosmosDbConnectionStrIndex);
                var key = Configuration[CosmosDbKeyIndex];
                return new CosmosDBProvider(connectionString, key);
            });

            services.AddTransient<IStorageProvider>(serviceProvider => {
                var sasToken = Configuration[AzureStorageSasTokenIndex];
                var connectionString = Configuration[AzureStorageConnectionStringIndex];

                return new AzureStorageProvider(AzureStorageShareName,
                    AzureStorageImagePrefix,
                    sasToken,
                    connectionString);
            });

            // fixme: Only for debug usecases. Delete it in production. ->
            var cognitiveService = new AzureCognitiveServiceProvider(Constants.CognitiveServiceGroupId,
                Constants.CognitiveServiceGroupName,
                Configuration[CognitiveServiceKeyIndex],
                Configuration.GetConnectionString(CognitiveServiceConnectionStrIndex));

            try
            {
                cognitiveService.DeleteGroup().Wait();
            }
            catch (Exception)
            {
                // ignore
            }
            // <-

            services.AddTransient<ICognitiveServiceProvider>(serviceProvider => {
                var endpoint = Configuration.GetConnectionString(CognitiveServiceConnectionStrIndex);
                var key = Configuration[CognitiveServiceKeyIndex];

                return new AzureCognitiveServiceProvider(Constants.CognitiveServiceGroupId,
                    Constants.CognitiveServiceGroupName,
                    key,
                    endpoint);
            });

            services.AddBot<Bot>(options =>
            {
                options.CredentialProvider = new ConfigurationCredentialProvider(Configuration);

                options.Middleware.Add(new CatchExceptionMiddleware<Exception>(async (context, exception) =>
                {
                    await context.TraceActivity("Exception", exception);
                    await context.SendActivity("Sorry, it looks like something went wrong!");
                }));

                IStorage dataStore = new MemoryStorage();

                options.Middleware.Add(new ConversationState<BotState>(dataStore));
            });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseMvc();

            app.UseDefaultFiles()
                .UseStaticFiles()
                .UseBotFramework();
        }
    }

    public class ApplicationConfiguration
    {
        public string MicrosoftAppId { get; set; }
        public string MicrosoftAppPassword { get; set; }
    }
}