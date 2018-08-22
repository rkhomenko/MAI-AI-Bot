using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Bot;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Core.Extensions;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Prompts.Choices;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Recognizers.Text;

using MAIAIBot.Core;

namespace MAIAIBot.TeachersBot
{
    public class Bot : IBot
    {
        private const int Timeout = 1000;
        private const string AcceptStudentCommand = "Accept";
        private const string DeclineStudentCommand = "Decline";

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

            var studentsList = "";
            int index = 1;
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

                    studentsList += $"{index,5} {student.Name}" + "\n";

                    /* if (result.CandidateIds.Count > 1)
                    {
                        await context.SendActivity("Внимание: для этого студента есть несколько кандидатов!");
                    } */

                    Thread.Sleep(Timeout);
                }

                await StorageProvider.Remove(url);
            }

            await context.SendActivity(studentsList);
        }

        private async Task OnProactiveMessage(ITurnContext context)
        {
            var replyActivity = context.Activity.CreateReply();
            var attachments = new List<Attachment>();

            var student = await DatabaseProvider.GetStudent(context.Activity.Text);

            var heroCard = new HeroCard()
            {
                Text = $"Студент {student.Name} ({student.Group}) сейчас на лекции",
                Images = new List<CardImage>()
                {
                    new CardImage(StorageProvider.GetCorrectUri(new Uri(student.ImgUrls[0])).ToString())
                },
                Buttons = new List<CardAction>()
                {
                    new CardAction()
                    {
                        Type = ActionTypes.ImBack,
                        Title = "Подвердить",
                        Value = $"Accept {student.Id}"
                    },
                    new CardAction()
                    {
                        Type = ActionTypes.ImBack,
                        Title = "Отклонить",
                        Value = $"Decline {student.Id}"
                    }
                }
            };

            attachments.Add(heroCard.ToAttachment());
            replyActivity.Attachments = attachments;

            await context.SendActivity(replyActivity);
        }

        private async Task AcceptStudent(ITurnContext context, string id)
        {
            var student = await DatabaseProvider.GetStudent(id);

            student.AddVisit(DateTime.Now);
            await DatabaseProvider.UpdateStudent(student);

            // Send accept to student
        }

        private async Task DeclineStudent(ITurnContext context, string id)
        {
            // Send decline to student
        }

        public async Task OnTurn(ITurnContext context)
        {
            string pattern = $"({AcceptStudentCommand}|{DeclineStudentCommand})\\s+(\\S+)";

            switch (context.Activity.Type)
            {
                case Constants.ActivityTypes.MyProactive:
                    await OnProactiveMessage(context);
                    break;
                case ActivityTypes.Message:
                    if (context.Activity.Attachments != null)
                    {
                        await CheckPhotos(context);
                    }
                    else if (context.Activity.Text != null)
                    {
                        var matches = Regex.Match(context.Activity.Text, pattern);
                        if (matches.Success)
                        {
                            var command = matches.Groups[1].ToString();
                            var id = matches.Groups[2].ToString();

                            switch (command)
                            {
                                case AcceptStudentCommand:
                                    await AcceptStudent(context, id);
                                    break;
                                case DeclineStudentCommand:
                                    await DeclineStudent(context, id);
                                    break;
                            }
                        }
                    }
                    break;
            }
        }
    }
}