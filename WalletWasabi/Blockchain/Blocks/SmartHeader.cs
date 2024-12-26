using NBitcoin;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;

namespace WalletWasabi.Blockchain.Blocks;

public class SmartHeader
{
	public SmartHeader(uint256 blockHash, uint256 prevHash, uint height, DateTimeOffset blockTime)
		: this(blockHash, prevHash, height, blockTime.ToUnixTimeSeconds())
	{
	}

	public SmartHeader(uint256 blockHash, uint256 prevHash, uint height, long epochBlockTime)
	{
		BlockHash = Guard.NotNull(nameof(blockHash), blockHash);
		PrevHash = Guard.NotNull(nameof(prevHash), prevHash);
		if (blockHash == prevHash)
		{
			throw new InvalidOperationException($"{nameof(blockHash)} cannot be equal to {nameof(prevHash)}. Value: {blockHash}.");
		}

		Height = height;
		EpochBlockTime = epochBlockTime;
	}

	public uint256 BlockHash { get; }
	public uint256 PrevHash { get; }
	public uint Height { get; }

	/// <summary>Timestamp in seconds.</summary>
	public long EpochBlockTime { get; }

	public DateTimeOffset BlockTime => DateTimeOffset.FromUnixTimeSeconds(EpochBlockTime);

	#region SpecialHeaders

	private static SmartHeader StartingHeaderSegwitMain { get; } = new SmartHeader(
		new uint256("0000000000000000001c8018d9cb3b742ef25114f27563e3fc4a1902167f9893"),
		new uint256("000000000000000000cbeff0b533f8e1189cf09dfbebf57a8ebe349362811b80"),
		481824,
		1503539857);

	private static SmartHeader StartingHeaderSegwitTestNet4 { get; } = new SmartHeader(
		Network.TestNet.GenesisHash,
		Network.TestNet.GetGenesis().Header.HashPrevBlock,
		0,
		Network.TestNet.GetGenesis().Header.BlockTime);

	private static SmartHeader StartingHeaderRegTest { get; } = new SmartHeader(
		Network.RegTest.GenesisHash,
		Network.RegTest.GetGenesis().Header.HashPrevBlock,
		0,
		Network.RegTest.GetGenesis().Header.BlockTime);

	public static SmartHeader GetStartingHeader(Network network) =>
		network.Name switch
		{
			"Main" => StartingHeaderSegwitMain,
			"TestNet4" => StartingHeaderSegwitTestNet4,
			"RegTest" => StartingHeaderRegTest,
			_ => throw new NotSupportedNetworkException(network)
		};

	#endregion SpecialHeaders
}
