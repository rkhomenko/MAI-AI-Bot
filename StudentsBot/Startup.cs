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
    public class DatabaseConfigurator : IMiddleware
    {
        private readonly IDatabaseProvider DatabaseProvider;

        public DatabaseConfigurator(IDatabaseProvider databaseProvider) {
            DatabaseProvider = databaseProvider;
        }

        public Task OnTurn(ITurnContext context, MiddlewareSet.NextDelegate next) {
            context.Services.Add("dbservice", DatabaseProvider);
            return next();
        }
    }

    public class Startup
    {
        private const string CosmosDbConnectionStrIndex = "CosmosDb";
        private const string CosmosDbKeyIndex = "CosmosDb:Key";

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

            services.AddBot<Bot>(options =>
            {
                options.CredentialProvider = new ConfigurationCredentialProvider(Configuration);

                options.Middleware.Add(new CatchExceptionMiddleware<Exception>(async (context, exception) =>
                {
                    await context.TraceActivity("EchoBot Exception", exception);
                    await context.SendActivity("Sorry, it looks like something went wrong!");
                }));

                IStorage dataStore = new MemoryStorage();

                options.Middleware.Add(new ConversationState<BotState>(dataStore));

                var databaseProvider = new CosmosDBProvider();
                var connectionString = Configuration.GetConnectionString(CosmosDbConnectionStrIndex);
                var key = Configuration[CosmosDbKeyIndex];
                databaseProvider.Init(connectionString, key).Wait();
                options.Middleware.Add(new DatabaseConfigurator(databaseProvider));
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