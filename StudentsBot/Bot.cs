using System;

using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Bot;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Core.Extensions;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Prompts;
using Microsoft.Bot.Schema;
using PromptsDialog = Microsoft.Bot.Builder.Dialogs;

using MAIAIBot.Core;

using CoreActivityTypes = MAIAIBot.Core.ActivityTypes;
using MicrosoftActivityTypes = Microsoft.Bot.Schema.ActivityTypes;

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

        private readonly DialogSet dialogs = null;
        private readonly IDatabaseProvider DatabaseProvider = null;
        private readonly IStorageProvider StorageProvider = null;
        private readonly ICognitiveServiceProvider CognitiveServiceProvider = null;

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

            foreach (var attachment in dialogContext.Context.Activity.Attachments)
            {
                var url = await StorageProvider.Load(urlToStream(attachment.ContentUrl),
                    attachment.Name);
                state.AddImgUrl(url.ToString());
            }

            if (state.ImgUrls.Count >= MinPhoto)
            {
                await next();
                return;
            }

            await dialogContext.Prompt(PromptStep.PhotoPrompt, $"{state.ImgUrls.Count} Прикрепи еще фото.");
        }

        private async Task GatherStudentInfo(DialogContext dialogContext)
        {
            var context = dialogContext.Context;
            var state = context.GetConversationState<BotState>();
            var attachments = context.Activity.Attachments;

            var studentChannelInfo = new StudentChannelInfo
            {
                ToId = context.Activity.From.Id,
                ToName = context.Activity.From.Name,
                FromId = context.Activity.Recipient.Id,
                FromName = context.Activity.Recipient.Name,
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
        }

        private async Task GatherInfoStep(DialogContext dialogContext, object result, SkipStepFunction next)
        {
            await GatherStudentInfo(dialogContext);
            await dialogContext.Context.SendActivity("Отлично! Ты в списке)");
            await dialogContext.End();
        }

        public Bot(IDatabaseProvider databaseProvider,
                   IStorageProvider storageProvider,
                   ICognitiveServiceProvider cognitiveServiceProvider)
        {
            DatabaseProvider = databaseProvider;
            StorageProvider = storageProvider;
            CognitiveServiceProvider = cognitiveServiceProvider;
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

        public async Task OnTurn(ITurnContext context)
        {
            var state = context.GetConversationState<BotState>();
            var dialogCtx = dialogs.CreateContext(context, state);

            await DatabaseProvider.Init();

            switch (context.Activity.Type)
            {
                case CoreActivityTypes.MyProactive:
                    await context.SendActivity("Get proactive activity!");
                    break;
                case MicrosoftActivityTypes.ConversationUpdate:
                    var newUserName = context.Activity.MembersAdded[0]?.Name;
                    if (!string.IsNullOrWhiteSpace(newUserName) && newUserName != "Bot")
                    {
                        await context.SendActivity("Привет! Я буду отмечать тебя на лекциях, "
                            + "но сначала нужно зарегистрироваться и отправить мне фотки) "
                            + "Напиши что-нибудь, чтобы начать регистрацию.");
                    }
                    break;
                case MicrosoftActivityTypes.Message:
                    if (state.RegistrationComplete)
                    {
                        await context.SendActivity("Ты уже в списке)");
                    }
                    else
                    {
                        await dialogCtx.Continue();
                        if (!context.Responded)
                        {
                            await dialogCtx.Begin(PromptStep.GatherInfo);
                        }
                    }
                    break;
            }
        }
    }
}