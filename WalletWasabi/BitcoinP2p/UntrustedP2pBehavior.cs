using NBitcoin.Protocol;
using System.Net;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.BitcoinP2p;

public class UntrustedP2pBehavior : P2pBehavior
{
	public UntrustedP2pBehavior(MempoolService mempoolService) : base(mempoolService)
	{
	}

	protected override void ProcessTx(TxPayload payload)
	{
		if (!MempoolService.TrustedNodeMode)
		{
			base.ProcessTx(payload);
		}
	}

	protected override bool ProcessInventoryVector(InventoryVector inv, EndPoint remoteSocketEndpoint)
	{
		if (inv.Type.HasFlag(InventoryType.MSG_TX))
		{
			if (MempoolService.TryGetFromBroadcastStore(inv.Hash, out TransactionBroadcastEntry? entry)) // If we have the transaction then adjust confirmation.
			{
				entry.ConfirmPropagationOnce(remoteSocketEndpoint);
			}

			// If we already processed it or we're in trusted node mode, then don't ask for it.
			if (MempoolService.TrustedNodeMode || MempoolService.IsProcessed(inv.Hash))
			{
				return false;
			}

			return true;
		}

		return false;
	}

	public override object Clone() => new UntrustedP2pBehavior(MempoolService);
}
