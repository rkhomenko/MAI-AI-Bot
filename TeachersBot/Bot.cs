using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;

using Microsoft.Bot;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Core.Extensions;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Prompts.Choices;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Recognizers.Text;
using Microsoft.Extensions.Configuration;

using MAIAIBot.Core;
using MAIAIBot.Core.DirectLine;

namespace MAIAIBot.TeachersBot
{
    public class Bot : IBot
    {
        private const int Timeout = 1000;

        private readonly MicrosoftAppCredentials AppCredentials;
        private readonly IDatabaseProvider DatabaseProvider;
        private readonly IStorageProvider StorageProvider;
        private readonly ICognitiveServiceProvider CognitiveServiceProvider;
        private readonly IConfiguration Configuration;

        public Bot(MicrosoftAppCredentials appCredentials,
                   IConfiguration configuration,
                   IDatabaseProvider databaseProvider,
                   IStorageProvider storageProvider,
                   ICognitiveServiceProvider cognitiveServiceProvider)
        {
            AppCredentials = appCredentials;
            Configuration = configuration;
            DatabaseProvider = databaseProvider;
            StorageProvider = storageProvider;
            CognitiveServiceProvider = cognitiveServiceProvider;
        }

        private async Task NotifyStudent(ITurnContext context, string studentId, string teacherId)
        {

            var client = new DirectLineClient(Configuration[Constants.DirectLineSecretIndex]);

            var activity = new Activity
            {
                Type = Constants.ActivityTypes.MyProactive,
                From = new ChannelAccount("MAI-AI-Teachers-Bot"),
                Text = $"{Constants.NotifyStudentCommand} {teacherId} {studentId} \"{DateTime.Now}\""
            };

            await client.SendActivityAsync(activity);
        }

        private async Task CheckPhotos(ITurnContext context)
        {
            var state = context.GetConversationState<BotState>();
            var students = new List<Student>();
            var attachments = context.Activity.Attachments;

            if (attachments == null)
            {
                await context.SendActivity("Прикрептие хотя бы одну фотографию!");
                return;
            }

            var now = DateTime.Now;
            var allStudents = DatabaseProvider.GetAllStudents().ToList();
            allStudents.ForEach(student => student.AddVisit(now, false));
            DatabaseProvider.UpdateStudents(allStudents).Wait();

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

                    student.UpdateLastVisit(true);
                    await DatabaseProvider.UpdateStudent(student);

                    studentsList += $"{index++,5}. {student.Name}" + "\n";

                    await NotifyStudent(context, student.Id, state.Id);

                    Thread.Sleep(Timeout);
                }

                //await StorageProvider.Remove(url);
            }

            if (studentsList.Length == 0)
            {
                studentsList = "Распознать никого не удалось.";
            }

            await context.SendActivity(studentsList);
        }

        private async Task Registration(ITurnContext context)
        {
            var teacher = new Student(context.Activity.From.Name,
                Constants.TeachersGroupName,
                new List<string>(),
                new StudentChannelInfo
                {
                    ToId = context.Activity.From.Id,
                    ToName = context.Activity.From.Name,
                    FromId = context.Activity.Recipient.Id,
                    FromName = context.Activity.Recipient.Name,
                    ServiceUrl = context.Activity.ServiceUrl,
                    ChannelId = context.Activity.ChannelId,
                    ConversationId = context.Activity.Conversation.Id
                },
                true);

            var state = context.GetConversationState<BotState>();
            state.RegistrationComplete = true;
            state.Id = teacher.Id;

            await DatabaseProvider.AddStudent(teacher);
        }

        private async Task OnProactiveMessage(ITurnContext context)
        {
            var pattern = $"{Constants.NotifyTeacherCommand}\\s+(\\S+)\\s+(\\S+)";
            var mathces = Regex.Match(context.Activity.Text, pattern);
            if (!mathces.Success)
            {
                return;
            }

            var teacher = await DatabaseProvider.GetStudent(mathces.Groups[1].ToString());
            var student = await DatabaseProvider.GetStudent(mathces.Groups[2].ToString());

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

            var userAccount = new ChannelAccount(teacher.ChannelInfo.ToId, teacher.ChannelInfo.ToName);
            var botAccount = new ChannelAccount(teacher.ChannelInfo.FromId, teacher.ChannelInfo.FromName);
            var serviceUrl = teacher.ChannelInfo.ServiceUrl;

            var connector = new ConnectorClient(new Uri(serviceUrl), AppCredentials);
            MicrosoftAppCredentials.TrustServiceUrl(serviceUrl);

            var channelId = teacher.ChannelInfo.ChannelId;
            var conversationId = teacher.ChannelInfo.ConversationId;

            var message = Activity.CreateMessageActivity();
            if (!string.IsNullOrEmpty(conversationId) && !string.IsNullOrEmpty(channelId))
            {
                message.ChannelId = channelId;
            }
            else
            {
                conversationId = (await connector.Conversations.CreateDirectConversationAsync(botAccount, userAccount)).Id;
            }

            message.From = botAccount;
            message.Recipient = userAccount;
            message.Conversation = new ConversationAccount(id: conversationId);
            message.Locale = "en-us";
            message.Attachments = new List<Attachment> { heroCard.ToAttachment() };

            await connector.Conversations.SendToConversationAsync((Activity)message);
        }

        private async Task SendResponseToStudent(ITurnContext context, string studentId, string teacherId, string command)
        {
            var client = new DirectLineClient(Configuration[Constants.DirectLineSecretIndex]);

            var activity = new Activity
            {
                Type = Constants.ActivityTypes.MyProactive,
                From = new ChannelAccount("MAI-AI-Teachers-Bot"),
                Text = $"{command} {studentId}"
            };

            await client.SendActivityAsync(activity);

            string text = null;
            var student = await DatabaseProvider.GetStudent(studentId);
            switch (command)
            {
                case Constants.AcceptStudentCommand:
                    text = "подтвержден";
                    break;
                case Constants.DeclineStudentCommand:
                    text = "отклонен";
                    break;
            }

            await context.SendActivity($"{student.Name} ({student.Group}) {text}.");
        }

        public async Task OnTurn(ITurnContext context)
        {
            string pattern = $"({Constants.AcceptStudentCommand}|{Constants.DeclineStudentCommand})\\s+(\\S+)";
            var state = context.GetConversationState<BotState>();

            switch (context.Activity.Type)
            {
                case Constants.ActivityTypes.MyProactive:
                    await OnProactiveMessage(context);
                    break;
                case ActivityTypes.Message:
                    if (!state.RegistrationComplete && context.Activity.From.Id != null)
                    {
                        await Registration(context);
                    }

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
                            var studentId = matches.Groups[2].ToString();

                            await SendResponseToStudent(context, studentId, state.Id, command);
                        }
                        else
                        {
                            await context.SendActivity("Прикрепите хотя бы одну фотографию!");
                        }
                    }
                    break;
            }
        }
    }
}