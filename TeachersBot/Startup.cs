using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder.BotFramework;
using Microsoft.Bot.Builder.Core.Extensions;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder.TraceExtensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using MAIAIBot.Core;

namespace MAIAIBot.TeachersBot
{
    public class Startup
    {
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
                var connectionString = Configuration.GetConnectionString(Constants.CosmosDbConnectionStrIndex);
                var key = Configuration[Constants.CosmosDbKeyIndex];
                return new CosmosDBProvider(connectionString, key);
            });

            services.AddTransient<IStorageProvider>(serviceProvider => {
                var sasToken = Configuration[Constants.AzureStorageSasTokenIndex];
                var connectionString = Configuration[Constants.AzureStorageConnectionStrIndex];

                return new AzureStorageProvider(Constants.AzureStorageShareName,
                    Constants.AzureStorageImagePrefix,
                    sasToken,
                    connectionString);
            });

            services.AddTransient<ICognitiveServiceProvider>(serviceProvider => {
                var endpoint = Configuration.GetConnectionString(Constants.CognitiveServiceConnectionStrIndex);
                var key = Configuration[Constants.CognitiveServiceKeyIndex];

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