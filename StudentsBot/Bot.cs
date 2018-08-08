using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Bot;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Core.Extensions;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Prompts;
using Microsoft.Bot.Schema;
using Microsoft.Recognizers.Text;
using PromptsDialog = Microsoft.Bot.Builder.Dialogs;

using MAIAIBot.Core;

namespace MAIAIBot.StudentsBot
{
    public static class PromptStep
    {
        public const string GatherInfo = "gatherInfo";
        public const string NamePrompt = "namePrompt";
        public const string GroupPrompt = "groupPrompt";
        public const string PhotoPrompt = "photoPrompt";
    }

    public class Bot : IBot
    {
        private readonly DialogSet dialogs;
        private readonly IDatabaseProvider DatabaseProvider = null;

        private async Task NonEmptyStringValidator(ITurnContext context, TextResult result, string message)
        {
            if (result.Value.Trim().Length == 0) {
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

        private async Task AskPhotoStep(DialogContext dialogContext, object result, SkipStepFunction next) {
            var state = dialogContext.Context.GetConversationState<BotState>();
            state.Group = (result as TextResult).Value.Trim();
            await dialogContext.Context.SendActivity("Прикрепи от 2 до 6 фотографий, на которых только ты.");
        }

        private async Task<IEnumerable<string>> UploadPhotos(DialogContext dialogContext, IEnumerable<Attachment> attachments) {
            var result = new List<string>();

            foreach (var attachment in attachments) {
                string url = attachment.ContentUrl;
                result.Add(url);
                await dialogContext.Context.SendActivity($"Upload {url}");
            }

            return result;
        }

        private async Task GatherInfoStep(DialogContext dialogContext, object result, SkipStepFunction next)
        {
            var state = dialogContext.Context.GetConversationState<BotState>();

            var attachments = await UploadPhotos(dialogContext, dialogContext.Context.Activity.Attachments);

            state.RegistrationComplete = true;
            await dialogContext.Context.SendActivity($"Name:\"{state.Name}\", group: {state.Group}");
            await dialogContext.Context.SendActivity("Отлично! Ты в списке)");
            await dialogContext.End();
        }

        public Bot(IDatabaseProvider databaseProvider)
        {
            DatabaseProvider = databaseProvider;
            dialogs = new DialogSet();

            dialogs.Add(PromptStep.NamePrompt,
                new PromptsDialog.TextPrompt(NameValidator));
            dialogs.Add(PromptStep.GroupPrompt,
                new PromptsDialog.TextPrompt(GroupValidator));
            dialogs.Add(PromptStep.PhotoPrompt,
                new PromptsDialog.AttachmentPrompt());
            dialogs.Add(PromptStep.GatherInfo,
                new WaterfallStep[] { AskNameStep, AskGroupStep, AskPhotoStep, GatherInfoStep });
        }

        public async Task OnTurn(ITurnContext context)
        {
            var state = context.GetConversationState<BotState>();
            var dialogCtx = dialogs.CreateContext(context, state);


            await DatabaseProvider.Init();

            switch (context.Activity.Type)
            {
                case ActivityTypes.ConversationUpdate:
                    var newUserName = context.Activity.MembersAdded[0]?.Name;
                    if (!string.IsNullOrWhiteSpace(newUserName) && newUserName != "Bot")
                    {
                        await context.SendActivity("Привет! Я буду отмечать тебя на лекциях, "
                            + "но сначала нужно зарегистрироваться и отправить мне фотки) "
                            + "Напиши что-нибудь, чтобы начать регистрацию.");
                    }
                    break;
                case ActivityTypes.Message:
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