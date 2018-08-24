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

using MAIAIBot.Core;

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
            var dateTime = DateTime.Now.ToString("MM/dd/yy H:mm:ss");

            var message = new Message
            {
                Conversation = new Message.ConversationInfo
                {
                    Id = student.ChannelInfo.ConversationId
                },
                ServiceUrl = student.ChannelInfo.ServiceUrl,
                ChannelId = student.ChannelInfo.ChannelId,
                From = new Message.FromToInfo()
                {
                    Id = context.Activity.From.Id,
                    Role = context.Activity.From.Role,
                },
                Recipient = new Message.FromToInfo()
                {
                    Id = student.ChannelInfo.ToId,
                    Name = student.ChannelInfo.ToName,
                    Role = "bot"
                },
                Text = $"{Constants.NotifyStudentCommand}: \"@{context.Activity.From.Name}\" \"{dateTime}\""
            };


            HttpClient client = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:23116/api/messages")
            };

            var jsonString = message.ToString();
            var stringContent = new StringContent(jsonString);
            stringContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            var responce = await client.PostAsync("http://localhost:23116/api/messages", stringContent);

            client.Dispose();

            //await context.SendActivity(responce.StatusCode);
            //await context.SendActivity(jsonString);
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

                    await NotifyStudent(context, student);

                    Thread.Sleep(Timeout);
                }

                //await StorageProvider.Remove(url);
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
                    ToId = context.Activity.Recipient.Id,
                    ToName = context.Activity.Recipient.Name,
                    ServiceUrl = context.Activity.ServiceUrl,
                    ChannelId = context.Activity.ChannelId,
                    ConversationId = context.Activity.Conversation.Id
                },
                true);

            await DatabaseProvider.AddStudent(teacher);
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

        private async Task SendResponseToStudent(ITurnContext context, string id, string command)
        {
            var student = await DatabaseProvider.GetStudent(id);

            var message = new Message
            {
                Conversation = new Message.ConversationInfo
                {
                    Id = student.ChannelInfo.ConversationId
                },
                ServiceUrl = student.ChannelInfo.ServiceUrl,
                ChannelId = student.ChannelInfo.ChannelId,
                From = new Message.FromToInfo()
                {
                    Id = context.Activity.From.Id,
                    Role = context.Activity.From.Role,
                },
                Recipient = new Message.FromToInfo()
                {
                    Id = student.ChannelInfo.ToId,
                    Name = student.ChannelInfo.ToName,
                    Role = "bot"
                },
                Text = command
            };
                    

            HttpClient client = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:23116/api/messages")
            };

            var jsonString = message.ToString();
            var stringContent = new StringContent(jsonString);
            stringContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            var responce = await client.PostAsync("http://localhost:23116/api/messages", stringContent);

            client.Dispose();

            // --> Debug
            //await context.SendActivity(responce.ToString());
            //await context.SendActivity(jsonString);

            if (command == Constants.AcceptStudentCommand)
            {
                student.UpdateLastVisit(true);
                await DatabaseProvider.UpdateStudent(student);
            }
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
                    if (!state.RegistrationComplete)
                    {
                        await Registration(context);
                        state.RegistrationComplete = true;
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
                            var id = matches.Groups[2].ToString();

                            await SendResponseToStudent(context, id, command);
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