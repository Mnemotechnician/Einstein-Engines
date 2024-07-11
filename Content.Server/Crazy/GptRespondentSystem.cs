using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Server.Speech;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using LLama;
using LLama.Common;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Crazy;

// Yes this won't be localized because -------------v
// Also I'm not creating CVars because this won't make its way into the real game
// And yes this is somewhat shitcode because -------^
public sealed class GptRespondentSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly PopupSystem _popups = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    // TESTING PURPOSES ONLY
    // REPLACE WITH THE PATH TO YOUR OWN MODEL BEFORE USING
    // Any model in the gguf format can be used, however, be wary of the performance implications
    // gpt2.Q8_0 can be found here: https://huggingface.co/igorbkz/gpt2-Q8_0-GGUF/tree/main
    // This model has pretty bad accuracy, but can run at a reasonable speed even on low-budget machines
    public static string ModelPath = "/home/fox/Models/gpt2.Q8_0.gguf";

    private LLamaWeights? _model;
    private LLamaContext? _context;

    private List<(EntityUid, string message)> _messages = new(); // TODO make a component field

    public override void Initialize()
    {
        Task.Run(LoadModelAsync);

        SubscribeLocalEvent<GptRespondentComponent, ListenEvent>(OnListen);
    }

    private void OnListen(EntityUid uid, GptRespondentComponent component, ListenEvent args)
    {
        if (args.Source == uid)
            return;

        var instruction = $"You are {Describe(uid)}. Your description is:\n{GetExamination(uid, uid)}\n\n"
                             + $"{Describe(args.Source)} spoke to you. Their description is:\n{GetExamination(args.Source, uid)}\n\n"
                             + $"Stay in character and write a short response in the style this character would write in.\n"
                             + $"You are a real-world character and cannot use internet slang. Speak like this character."
                             + $"\n\n### Message:\n\n{args.Message}";

        Log.Info("Using prompt: " + instruction);

        // TODO assign to the component and cancel if new speech is heard
        Task.Run(async () =>
        {
            try { await GenerateResponse(uid, instruction, component.Temperature); }
            catch { }
        });

        _popups.PopupEntity($"{Identity.Name(uid, EntityManager)} is thinking...", uid);
    }

    private async Task GenerateResponse(EntityUid uid, string instruction, float temperature)
    {
        while (_context == null || _model == null)
            await Task.Delay(100);

        var executor = new InstructExecutor(_context);
        var param = new InferenceParams()
        {
            MaxTokens = 64,
            Temperature = temperature
        };

        // TODO add a way to interrupt this
        var message = new StringBuilder();
        await foreach (var response in executor.InferAsync(instruction, param))
        {
            Log.Info($"Received response part for {uid}: " + response);
            message.Append(response);

            if (response.Contains("\n"))
            {
                _messages.Add((uid, message.ToString()));
                message.Clear();
            }
        }

        if (message.Length != 0)
            _messages.Add((uid, message.ToString()));
    }

    private string Describe(EntityUid uid)
    {
        var speciesStr = "";
        if (TryComp<HumanoidAppearanceComponent>(uid, out var comp))
            speciesStr = $" (humanoid, {Loc.GetString("species-name-" + comp.Species.Id)})";

        return $"{Identity.Name(uid, EntityManager)}{speciesStr})";
    }

    private string GetExamination(EntityUid uid, EntityUid examiner)
    {
        var ev = new ExaminedEvent(new FormattedMessage(), uid, examiner, false, false);
        ev.PushText(MetaData(uid).EntityDescription, 100);
        RaiseLocalEvent(uid, ev);

        return ev.GetTotalMessage().ToString().ReplaceLineEndings(" ");
    }

    public override void Update(float frameTime)
    {
        for (int i = _messages.Count - 1; i >= 0; i--)
        {
            var (entity, message) = _messages[i];

            if (Exists(entity) && MetaData(entity).EntityLifeStage < EntityLifeStage.Terminating)
            {
                _chat.TrySendInGameICMessage(entity, message, InGameICChatType.Speak, ChatTransmitRange.Normal);
            }

            _messages.RemoveAt(i);
        }
    }

    private async Task LoadModelAsync()
    {
        var param = new ModelParams(ModelPath)
        {
            ContextSize = 2048,
            Threads = 4,
            Seed = (uint) _random.Next(0, 10000)
        };
        var model = await LLamaWeights.LoadFromFileAsync(param, new CancellationToken(), new Progress<float>(value =>
        {
            Log.Info($"Model loading: {(int) (value * 100)}%");
        }));

        _model = model;
        _context = model.CreateContext(param);
    }
}
