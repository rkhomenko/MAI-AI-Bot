using System;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;

using Microsoft.Bot;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Core.Extensions;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Prompts;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using PromptsDialog = Microsoft.Bot.Builder.Dialogs;

using Newtonsoft.Json;

using MAIAIBot.Core;
using MAIAIBot.Core.DirectLine;
using Microsoft.Bot.Connector;

namespace MAIAIBot.StudentsBot
{
    public static class PromptStep
    {
        public const string GatherInfo = "gatherInfo";
        public const string NamePrompt = "namePrompt";
        public const string GroupPrompt = "groupPrompt";
        public const string PhotoPrompt = "photoPrompt0";
        public static readonly string[] PhotoPrompts =
        {
            "photoPrompt1", "photoPrompt2"
        };
    }

    public class Bot : IBot
    {
        private const int MinPhoto = 3;
        private const int MaxPhoto = 6;
        private const string ImHere = "Я на лекции";

        private readonly IConfiguration Configuration;
        private readonly DialogSet dialogs = null;
        private readonly IDatabaseProvider DatabaseProvider = null;
        private readonly IStorageProvider StorageProvider = null;
        private readonly ICognitiveServiceProvider CognitiveServiceProvider = null;
        private readonly MicrosoftAppCredentials AppCredentials;

        private async Task NonEmptyStringValidator(ITurnContext context, TextResult result, string message)
        {
            if (result.Value.Trim().Length == 0)
            {
                result.Status = PromptStatus.NotRecognized;
                await context.SendActivity(message);
            }
        }

        private async Task NameValidator(ITurnContext context, TextResult result)
        {
            await NonEmptyStringValidator(context, result, "Введи корректные ФИО!");
        }

        private async Task GroupValidator(ITurnContext context, TextResult result)
        {
            await NonEmptyStringValidator(context, result, "Введи корректный номер группы!");
        }

        private async Task AskNameStep(DialogContext dialogContext, object result, SkipStepFunction next)
        {
            await dialogContext.Prompt(PromptStep.NamePrompt, "Напиши ФИО:");
        }

        private async Task AskGroupStep(DialogContext dialogContext, object result, SkipStepFunction next)
        {
            var state = dialogContext.Context.GetConversationState<BotState>();
            state.Name = (result as TextResult).Value;
            await dialogContext.Prompt(PromptStep.NamePrompt, "Напиши номер группы:");
        }

        private async Task AskPhotoStep(DialogContext dialogContext, object result, SkipStepFunction next)
        {
            var state = dialogContext.Context.GetConversationState<BotState>();
            state.Group = (result as TextResult).Value.Trim();
            await dialogContext.Prompt(PromptStep.PhotoPrompt, $"Прикрепи {MinPhoto} фотографии, на которых только ты.");
        }

        private async Task UploadPhotos(DialogContext dialogContext, object result, SkipStepFunction next)
        {
            Stream urlToStream(string url)
            {
                byte[] imageData = null;
                using (var wc = new System.Net.WebClient())
                    imageData = wc.DownloadData(url);
                return new MemoryStream(imageData);
            }

            var context = dialogContext.Context;
            var state = context.GetConversationState<BotState>();

            if (state.ImgUrls.Count >= MinPhoto)
            {
                await next();
                return;
            }

            foreach (var attachment in dialogContext.Context.Activity.Attachments)
            {
                var url = await StorageProvider.Load(urlToStream(attachment.ContentUrl),
                    attachment.Name);
                state.AddImgUrl(url.ToString());
            }

            Thread.Sleep(1000);

            if (state.ImgUrls.Count >= MinPhoto)
            {
                await next();
                return;
            }

            await dialogContext.Prompt(PromptStep.PhotoPrompt, $"Прикрепи еще фото (осталось {MinPhoto - state.ImgUrls.Count}).");
        }

        private async Task GatherStudentInfo(DialogContext dialogContext)
        {
            var context = dialogContext.Context;
            var state = context.GetConversationState<BotState>();

            var studentChannelInfo = new StudentChannelInfo
            {
                ToId = context.Activity.Recipient.Id,
                ToName = context.Activity.Recipient.Name,
                ServiceUrl = context.Activity.ServiceUrl,
                ChannelId = context.Activity.ChannelId,
                ConversationId = context.Activity.Conversation.Id
            };

            var student = new Student(state.Name,
                                      state.Group,
                                      state.ImgUrls,
                                      studentChannelInfo);

            await DatabaseProvider.AddStudent(student);

            var imgUrlsSas = from imgUrl in state.ImgUrls
                             select StorageProvider.GetCorrectUri(new Uri(imgUrl)).ToString();
            await CognitiveServiceProvider.AddPerson(student.Id, imgUrlsSas);
            await CognitiveServiceProvider.TrainGroup();

            state.RegistrationComplete = true;
            state.Id = student.Id;
        }

        private async Task GatherInfoStep(DialogContext dialogContext, object result, SkipStepFunction next)
        {
            await GatherStudentInfo(dialogContext);
            await dialogContext.Context.SendActivity("Отлично! Ты в списке)");
            await dialogContext.End();
        }

        public Bot(IConfiguration configuration,
                   IDatabaseProvider databaseProvider,
                   IStorageProvider storageProvider,
                   ICognitiveServiceProvider cognitiveServiceProvider,
                   MicrosoftAppCredentials appCredentials)
        {

            Configuration = configuration;
            DatabaseProvider = databaseProvider;
            StorageProvider = storageProvider;
            CognitiveServiceProvider = cognitiveServiceProvider;
            AppCredentials = appCredentials;

            dialogs = new DialogSet();

            dialogs.Add(PromptStep.NamePrompt,
                new PromptsDialog.TextPrompt(NameValidator));
            dialogs.Add(PromptStep.GroupPrompt,
                new PromptsDialog.TextPrompt(GroupValidator));
            dialogs.Add(PromptStep.PhotoPrompt,
                new PromptsDialog.AttachmentPrompt());
            dialogs.Add(PromptStep.PhotoPrompts[0],
                new PromptsDialog.AttachmentPrompt());
            dialogs.Add(PromptStep.PhotoPrompts[1],
                new PromptsDialog.AttachmentPrompt());
            dialogs.Add(PromptStep.GatherInfo,
                new WaterfallStep[]
                {
                    AskNameStep, AskGroupStep, AskPhotoStep, UploadPhotos, UploadPhotos, GatherInfoStep
                });
        }

        private async Task NotifyTeacher(ITurnContext context, string studentId, string teacherId)
        {
            var teacher = await DatabaseProvider.GetStudent(teacherId);

            var client = new DirectLineClient(Configuration[Constants.DirectLineSecretIndex]);

            var activity = new Activity
            {
                Type = Constants.ActivityTypes.MyProactive,
                From = new ChannelAccount("MAI-AI-Students-Bot"),
                Text = $"{Constants.NotifyTeacherCommand} {teacherId} {studentId}"
            };

            await client.SendActivityAsync(activity);
        }

        public async Task OnTurn(ITurnContext context)
        {
            var state = context.GetConversationState<BotState>();
            var dialogCtx = dialogs.CreateContext(context, state);

            await DatabaseProvider.Init();

            switch (context.Activity.Type)
            {
                case Constants.ActivityTypes.MyProactive:
                    {
                        string responsePattern = $"({Constants.AcceptStudentCommand}|{Constants.DeclineStudentCommand})\\s+(\\S+)";
                        string notificationPattern = $"{Constants.NotifyStudentCommand}\\s+(\\S+)\\s+(\\S+)\\s+\"(.+)\"";
                        var matches = Regex.Match(context.Activity.Text, responsePattern);
                        string text = null;
                        string studentId = null;
                        if (matches.Success)
                        {
                            studentId = matches.Groups[2].ToString();
                            switch (matches.Groups[1].ToString())
                            {
                                case Constants.AcceptStudentCommand:
                                    text = "Преподаватель подтвердил твоё присутствие.";
                                    break;
                                case Constants.DeclineStudentCommand:
                                    text = "Преподаватель не подтвердил твоё присутствие.";
                                    break;
                            }
                        }
                        else
                        {
                            matches = Regex.Match(context.Activity.Text, notificationPattern);
                            if (matches.Success)
                            {
                                studentId = matches.Groups[2].ToString();
                                var teacher = await DatabaseProvider.GetStudent(matches.Groups[1].ToString());
                                text = $"@{teacher.Name} отметил тебя на лекции ({matches.Groups[3]})";
                            }
                        }

                        if (studentId != null && text != null)
                        {
                            var student = await DatabaseProvider.GetStudent(studentId);
                            var userAccount = new ChannelAccount(student.ChannelInfo.ToId, student.ChannelInfo.ToName);
                            var botAccount = new ChannelAccount(student.ChannelInfo.FromId, student.ChannelInfo.FromName);
                            var serviceUrl = student.ChannelInfo.ServiceUrl;

                            var connector = new ConnectorClient(new Uri(serviceUrl), AppCredentials);
                            MicrosoftAppCredentials.TrustServiceUrl(serviceUrl);

                            var channelId = student.ChannelInfo.ChannelId;
                            var conversationId = student.ChannelInfo.ConversationId;

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
                            message.Text = text;
                            message.Locale = "en-us";

                            await connector.Conversations.SendToConversationAsync((Activity)message);
                        }
                    }
                    break;
                case ActivityTypes.ConversationUpdate:
                    {
                        var newUserName = context.Activity.MembersAdded[0]?.Name;
                        if (!string.IsNullOrWhiteSpace(newUserName) && newUserName != "Bot")
                        {
                            await context.SendActivity("Привет! Я буду отмечать тебя на лекциях, "
                                + "но сначала нужно зарегистрироваться и отправить мне фотки) "
                                + "Напиши что-нибудь, чтобы начать регистрацию.");
                        }
                    }
                    break;
                case ActivityTypes.Message:
                    {
                        if (context.Activity.Text == ImHere
                            &&
                            state.RegistrationComplete)
                        {
                            var teachers = from teacher in DatabaseProvider.GetAllStudents()
                                           where teacher.IsTeacher
                                           select teacher;
                            var teacherButtons = new List<CardAction>();

                            foreach (var teacher in teachers)
                            {
                                teacherButtons.Add(new CardAction()
                                {
                                    Type = ActionTypes.ImBack,
                                    Title = $"{teacher.Name}",
                                    Value = $"{Constants.NotifyTeacherCommand} {teacher.Id}"
                                });
                            }

                            var heroCard = new HeroCard()
                            {
                                Text = "Выбери преподавателя:",
                                Images = new List<CardImage>(),
                                Buttons = teacherButtons
                            };

                            var replyActivity = context.Activity.CreateReply();
                            replyActivity.Attachments = new List<Attachment> {
                                heroCard.ToAttachment()
                            };

                            await context.SendActivity(replyActivity);
                        }
                        else if (state.RegistrationComplete)
                        {
                            string pattern = $"({Constants.NotifyTeacherCommand})\\s+(\\S+)";
                            if (context.Activity.Text != null)
                            {
                                var matches = Regex.Match(context.Activity.Text, pattern);
                                if (matches.Success)
                                {
                                    await NotifyTeacher(context, state.Id, matches.Groups[2].ToString());
                                }
                                else
                                {
                                    await context.SendActivity("Ты уже в списке)");
                                }
                            }
                        }
                        else
                        {
                            await dialogCtx.Continue();
                            if (!context.Responded)
                            {
                                await dialogCtx.Begin(PromptStep.GatherInfo);
                            }
                        }
                    }
                    break;
            }
        }
    }
}