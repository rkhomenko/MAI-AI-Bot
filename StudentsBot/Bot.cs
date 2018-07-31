using System.Threading.Tasks;
using Microsoft.Bot;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Core.Extensions;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Prompts;
using Microsoft.Bot.Schema;
using Microsoft.Recognizers.Text;
using PromptsDialog = Microsoft.Bot.Builder.Dialogs;

namespace MAIAIBot.StudentsBot
{
    public static class PromptStep
    {
        public const string GatherInfo = "gatherInfo";
        public const string NamePrompt = "namePrompt";
        public const string GroupPrompt = "groupPrompt";
    }

    public class Bot : IBot
    {
        private readonly DialogSet dialogs;

        private async Task NonEmptyStringValidator(ITurnContext context, TextResult result, string message)
        {
            if (result.Value.Trim().Length == 0) {
                result.Status = PromptStatus.NotRecognized;
                await context.SendActivity(message);
            }
        }

        private async Task NameValidator(ITurnContext context, TextResult result)
        {
            await NonEmptyStringValidator(context, result, "Введите корректные ФИО!");
        }

        private async Task GroupValidator(ITurnContext context, TextResult result)
        {
            await NonEmptyStringValidator(context, result, "Введите корректный номер группы!");
        }

        private async Task AskNameStep(DialogContext dialogContext, object result, SkipStepFunction next)
        {
            await dialogContext.Prompt(PromptStep.NamePrompt, "Введите ФИО:");
        }

        private async Task AskGroupStep(DialogContext dialogContext, object result, SkipStepFunction next)
        {
            var state = dialogContext.Context.GetConversationState<BotState>();
            state.Name = (result as TextResult).Value;
            await dialogContext.Prompt(PromptStep.NamePrompt, "Введите номер группы:");
        }

        private async Task GatherInfoStep(DialogContext dialogContext, object result, SkipStepFunction next)
        {
            var state = dialogContext.Context.GetConversationState<BotState>();
            state.Group = (result as TextResult).Value.Trim();
            await dialogContext.Context.SendActivity($"Name:\"{state.Name}\", group: {state.Group}");
            await dialogContext.End();
        }

        public Bot()
        {
            dialogs = new DialogSet();

            dialogs.Add(PromptStep.NamePrompt,
                new PromptsDialog.TextPrompt(NameValidator));
            dialogs.Add(PromptStep.GroupPrompt,
                new PromptsDialog.TextPrompt(GroupValidator));
            dialogs.Add(PromptStep.GatherInfo,
                new WaterfallStep[] { AskNameStep, AskGroupStep, GatherInfoStep });
        }

        public async Task OnTurn(ITurnContext context)
        {
            var state = context.GetConversationState<BotState>();
            var dialogCtx = dialogs.CreateContext(context, state);
            switch (context.Activity.Type)
            {
                case ActivityTypes.Message:
                    await dialogCtx.Continue();
                    if (!context.Responded)
                    {
                        await dialogCtx.Begin(PromptStep.GatherInfo);
                    }
                    break;
            }
        }
    }
}