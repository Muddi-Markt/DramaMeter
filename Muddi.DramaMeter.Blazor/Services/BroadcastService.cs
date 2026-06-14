namespace Muddi.DramaMeter.Blazor.Services;

/// <summary>
///     Singleton service that notifies all connected Blazor Server clients
///     when the vote data has changed. Clients subscribe via <see cref="Register"/>
///     and call <c>LoadAsync()</c> to refresh their UI.
/// </summary>
public class BroadcastService
{
	private readonly Lock _lock = new();
	private readonly List<Func<Task>> _callbacks = [];

	/// <summary>
	///     Register an async callback. The returned <see cref="Registration"/>
	///     can be disposed to remove the callback early. When a Blazor circuit
	///     closes, all registrations are cleaned up automatically.
	/// </summary>
	public Registration Register(Func<Task> callback)
	{
		lock (_lock)
		{
			_callbacks.Add(callback);
		}

		return new Registration(this, callback);
	}

	/// <summary>
	///     Called by services (e.g. after <c>SubmitVoteAsync</c> or <c>DeleteVote</c>) to
	///     notify all connected clients that they should refresh their data.
	///     Awaits every callback so callers know all circuits have been notified.
	/// </summary>
	public async Task NotifyVoteChangedAsync()
	{
		Func<Task>[] callbacks;
		lock (_lock)
		{
			callbacks = _callbacks.ToArray();
		}

		await Task.WhenAll(callbacks.Select(async cb =>
		{
			try { await cb(); }
			catch { /* circuit may have been disposed between snapshot and notify */ }
		}));
	}

	/// <summary>
	///     Handle returned from <see cref="Register"/>. Disposing it removes
	///     the associated callback from the notification list.
	/// </summary>
	public sealed class Registration(BroadcastService owner, Func<Task> callback) : IDisposable
	{
		private bool _disposed;

		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;
			lock (owner._lock)
			{
				owner._callbacks.Remove(callback);
			}
		}
	}
}