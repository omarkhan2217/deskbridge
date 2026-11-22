using Deskbridge.Core;
using Deskbridge.Core.Policy;
using Deskbridge.Core.Tools;
using Deskbridge.Engines.Uia;

namespace Deskbridge.Mcp;

/// <summary>
/// What every tool shares: the pipeline (policy, confirmation, audit), the config, and the UI Automation
/// worker, which starts on first use so a broker that only lists windows never loads UI Automation (D38).
/// </summary>
public sealed class ToolServices(ToolPipeline pipeline, ConfigStore config, Func<UiaWorker> createUiaWorker, INotifier? notifier = null) : IDisposable
{
    readonly Lazy<UiaWorker> uia = new(createUiaWorker, LazyThreadSafetyMode.ExecutionAndPublication);
    UiaActions? actions;

    public ToolPipeline Pipeline { get; } = pipeline;

    public ConfigStore Config { get; } = config;

    public INotifier Notifier { get; set; } = notifier ?? new NoNotifier();

    public UiaWorker Uia => uia.Value;

    public UiaReader Reader => new(uia.Value);

    /// <summary>One set of UI actions per broker, so element IDs found in one call work in the next.</summary>
    public UiaActions Ui => LazyInitializer.EnsureInitialized(ref actions, () => new UiaActions(uia.Value));

    public void Dispose()
    {
        if (uia.IsValueCreated)
        {
            uia.Value.Dispose();
        }
    }
}
