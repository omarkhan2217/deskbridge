using Deskbridge.Core.Policy;

namespace Deskbridge.Ui;

/// <summary>
/// Shows one confirmation window at a time on the UI thread; later requests wait their turn.
/// No answer within the limit (2 minutes by default) is a Deny.
/// </summary>
public sealed class ConfirmationPresenter(UiThread ui, TimeSpan limit) : IConfirmationPresenter
{
    public static readonly TimeSpan DefaultLimit = TimeSpan.FromMinutes(2);

    readonly SemaphoreSlim oneAtATime = new(1, 1);

    public async Task<ConfirmationAnswer> AskAsync(ConfirmationRequest request, CancellationToken ct)
    {
        await oneAtATime.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var answer = new TaskCompletionSource<ConfirmationAnswer>(TaskCreationOptions.RunContinuationsAsynchronously);
            var form = await ui.InvokeAsync(() =>
            {
                var shown = new ConfirmationForm(request, limit, result => answer.TrySetResult(result));
                shown.Show();
                return shown;
            }).ConfigureAwait(false);

            await using (ct.Register(() => ui.Post(() =>
                   {
                       if (!form.IsDisposed)
                       {
                           form.Close();
                       }
                   })).ConfigureAwait(false))
            {
                var result = await answer.Task.ConfigureAwait(false);

                // The next window appears only once this one is gone, so two never overlap.
                await form.Gone.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
                return result;
            }
        }
        finally
        {
            oneAtATime.Release();
        }
    }
}
