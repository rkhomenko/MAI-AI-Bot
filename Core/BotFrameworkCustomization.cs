using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Adapters;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder.Integration.AspNet.Core.Handlers;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace MAIAIBot.Core
{
    public static class ActivityTypes
    {
        public const string MyProactive = "MyProactive";
    }

    public class CustomBotMessageHandler : BotMessageHandlerBase
    {
        public CustomBotMessageHandler(BotFrameworkAdapter botFrameworkAdapter) : base(botFrameworkAdapter) { }

        protected override async Task<InvokeResponse> ProcessMessageRequestAsync(HttpRequest request, BotFrameworkAdapter botFrameworkAdapter, Func<ITurnContext, Task> botCallbackHandler)
        {
            var activity = default(Activity);

            Console.WriteLine($"{request}");

            using (var bodyReader = new JsonTextReader(new StreamReader(request.Body, Encoding.UTF8)))
            {
                activity = BotMessageHandlerBase.BotMessageSerializer.Deserialize<Activity>(bodyReader);
            }

            activity.Type = ActivityTypes.MyProactive;

#pragma warning disable UseConfigureAwait // Use ConfigureAwait
            var invokeResponse = await botFrameworkAdapter.ProcessActivity(
                    request.Headers["Authorization"],
                    activity,
                    botCallbackHandler);
#pragma warning restore UseConfigureAwait // Use ConfigureAwait

            return invokeResponse;
        }
    }

    public static class CustomApplicationBuilderExtensions
    {
        public const string MyProactivePath = "/myproactive";

        public static IApplicationBuilder UseBotFramework(this IApplicationBuilder applicationBuilder) =>
            applicationBuilder.UseBotFramework(paths => { });

        public static IApplicationBuilder UseBotFramework(this IApplicationBuilder applicationBuilder, Action<BotFrameworkPaths> configurePaths)
        {
            if (applicationBuilder == null)
            {
                throw new ArgumentNullException(nameof(applicationBuilder));
            }

            if (configurePaths == null)
            {
                throw new ArgumentNullException(nameof(configurePaths));
            }

            var options = applicationBuilder.ApplicationServices.GetRequiredService<IOptions<BotFrameworkOptions>>().Value;

            var botFrameworkAdapter = new BotFrameworkAdapter(options.CredentialProvider, options.ConnectorClientRetryPolicy);

            foreach (var middleware in options.Middleware)
            {
                botFrameworkAdapter.Use(middleware);
            }

            var paths = new BotFrameworkPaths();

            configurePaths(paths);

            if (options.EnableProactiveMessages)
            {
                applicationBuilder.Map(
                    paths.BasePath + paths.ProactiveMessagesPath,
                    botProactiveAppBuilder => botProactiveAppBuilder.Run(new BotProactiveMessageHandler(botFrameworkAdapter).HandleAsync));
            }

            var pathString = paths.BasePath + MyProactivePath;

            applicationBuilder.Map(
                pathString,
                botActivitiesAppBuilder => botActivitiesAppBuilder.Run(new CustomBotMessageHandler(botFrameworkAdapter).HandleAsync));

            return applicationBuilder;

        }
    }
}
