using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Bot;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Core.Extensions;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;

using MAIAIBot.Core;

using CoreActivityTypes = MAIAIBot.Core.ActivityTypes;
using MicrosoftActivityTypes = Microsoft.Bot.Schema.ActivityTypes;

namespace MAIAIBot.TeachersBot
{
    public class Bot : IBot
    {
        private const int Timeout = 1000;

        private readonly MicrosoftAppCredentials AppCredentials;
        private readonly IDatabaseProvider DatabaseProvider;
        private readonly IStorageProvider StorageProvider;
        private readonly ICognitiveServiceProvider CognitiveServiceProvider;

        public Bot(MicrosoftAppCredentials appCredentials,
                   IDatabaseProvider databaseProvider,
                   IStorageProvider storageProvider,
                   ICognitiveServiceProvider cognitiveServiceProvider)
        {
            AppCredentials = appCredentials;
            DatabaseProvider = databaseProvider;
            StorageProvider = storageProvider;
            CognitiveServiceProvider = cognitiveServiceProvider;
        }

        private async Task NotifyStudent(ITurnContext context, Student student)
        {
            var channelInfo = student.ChannelInfo;
            var userAccount = new ChannelAccount(channelInfo.ToId, channelInfo.ToName);
            var botAccount = new ChannelAccount(context.Activity.From.Id, context.Activity.From.Name);
            var serviceUrl = context.Activity.ServiceUrl;
            var conversationId = channelInfo.ConversationId;

            MicrosoftAppCredentials.TrustServiceUrl(serviceUrl, DateTime.Now.AddMinutes(5));
            var connector = new ConnectorClient(new Uri(serviceUrl), AppCredentials);

            var message = Activity.CreateMessageActivity();
            if (!string.IsNullOrEmpty(channelInfo.ConversationId) &&
                !string.IsNullOrEmpty(channelInfo.ConversationId))
            {

                message.ChannelId = channelInfo.ConversationId;
            }
            else
            {
                conversationId = (await connector.Conversations.CreateDirectConversationAsync(
                    botAccount, userAccount)).Id;
            }

            message.From = botAccount;
            message.Recipient = userAccount;
            message.Conversation = new ConversationAccount(id: conversationId);
            message.Text = $"Ты был на лекции {student.Visits[student.Visits.Count - 1]}";
            message.Locale = "ru-ru";

            await connector.Conversations.SendToConversationAsync(message as Activity);

        }

        private async Task CheckPhotos(ITurnContext context)
        {
            var students = new List<Student>();
            var attachments = context.Activity.Attachments;

            if (attachments == null)
            {
                await context.SendActivity("Прикрептие хотя бы одну фотографию!");
                return;
            }

            foreach (var attachment in attachments)
            {
                var url = await StorageProvider.Load(await StorageProvider.GetStream(
                        new Uri(attachment.ContentUrl)),
                    attachment.Name);

                var results = await CognitiveServiceProvider.Identify(StorageProvider.GetCorrectUri(url).ToString());

                foreach (var result in results)
                {
                    var student = await DatabaseProvider.GetStudent(result.CandidateIds[0]);

                    student.AddVisit(DateTime.Now);
                    await DatabaseProvider.UpdateStudent(student);

                    await context.SendActivity($"{student.Name} {student.Group}");
                    await NotifyStudent(context, student);

                    if (result.CandidateIds.Count > 1)
                    {
                        await context.SendActivity("WARNING: для этого студента есть несколько кандидатов!");
                    }

                    Thread.Sleep(Timeout);
                }
            }
        }

        public async Task OnTurn(ITurnContext context)
        {
            switch (context.Activity.Type)
            {
                case MicrosoftActivityTypes.Message:
                    await CheckPhotos(context);
                    break;
            }
        }
    }
}