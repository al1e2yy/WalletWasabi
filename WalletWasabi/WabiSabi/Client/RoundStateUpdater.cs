using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Client
{
	public class RoundStateUpdater : PeriodicRunner
	{
		public RoundStateUpdater(TimeSpan requestInterval, IWabiSabiApiRequestHandler arenaRequestHandler) : base(requestInterval)
		{
			ArenaRequestHandler = arenaRequestHandler;
		}

		private IWabiSabiApiRequestHandler ArenaRequestHandler { get; }
		private Dictionary<uint256, RoundState> RoundStates { get; set; } = new();

		private List<RoundStateAwaiter> Awaiters { get; } = new();
		private object AwaitersLock { get; } = new();

		protected override async Task ActionAsync(CancellationToken cancellationToken)
		{
			var statusResponse = await ArenaRequestHandler.GetStatusAsync(cancellationToken).ConfigureAwait(false);
			var responseRoundStates = statusResponse.ToDictionary(round => round.Id);

			var updatedRoundStates = responseRoundStates.Where(round => RoundStates.ContainsKey(round.Key));
			var newRoundStates = responseRoundStates.Where(round => !RoundStates.ContainsKey(round.Key));
			var removedRoundStates = RoundStates.Where(round => !responseRoundStates.ContainsKey(round.Key));

			var roundsToUpdate = updatedRoundStates.Where(updatedRound => RoundStates[updatedRound.Key] != updatedRound.Value)
				.Union(newRoundStates)
				.Union(removedRoundStates)
				.Select(rs => rs.Key)
				.ToList();

			RoundStates = updatedRoundStates.Union(newRoundStates).ToDictionary(s => s.Key, s => s.Value);

			lock (AwaitersLock)
			{
				foreach (var awaiter in Awaiters.Where(awaiter => awaiter.IsCompleted(RoundStates)).ToArray())
				{
					// The predicate was fulfilled.
					Awaiters.Remove(awaiter);
					break;
				}
			}
		}

		public Task<RoundState> CreateRoundAwaiter(uint256? roundId, Predicate<RoundState> predicate, CancellationToken cancellationToken)
		{
			RoundStateAwaiter? roundStateAwaiter = null;

			lock (AwaitersLock)
			{
				roundStateAwaiter = new RoundStateAwaiter(predicate, roundId, cancellationToken);
				Awaiters.Add(roundStateAwaiter);
			}

			cancellationToken.Register(() =>
			{
				lock (AwaitersLock)
				{
					Awaiters.Remove(roundStateAwaiter);
				}
			});

			return roundStateAwaiter.Task;
		}

		public Task<RoundState> CreateRoundAwaiter(Predicate<RoundState> predicate, CancellationToken cancellationToken)
		{
			return CreateRoundAwaiter(null, predicate, cancellationToken);
		}

		public override Task StopAsync(CancellationToken cancellationToken)
		{
			lock (AwaitersLock)
			{
				foreach (var awaiter in Awaiters)
				{
					awaiter.Cancel();
				}
			}
			return base.StopAsync(cancellationToken);
		}
	}
}
