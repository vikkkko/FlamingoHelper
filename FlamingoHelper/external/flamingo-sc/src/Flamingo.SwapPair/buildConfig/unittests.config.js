module.exports = {
	// The hash of the super admin
	superAdmin: 'NMXY5eaTH1jBTMW8DinT4sRX8oSJ2RrNdK',
	// The public key of the group that the contract trusts
	contractTrustGroup: '02738f9efdd954f8436d91ff5f373ae8af14641abc6511de3d1a2ab40665e9a21f',
	// The hash of the white list contract
	whiteListContract: '0xa60133f5a4dbadf14d730095b74fb5fc378724cc',
	// The liquidity pool contracts to build
	liquidityPools: [
		{
			contractName: 'FLP-fWBTC-fUSDT',
			tokenSymbol: 'FLP-fWBTC-fUSDT',
			tokenA: '0x12d4787e5900c6903f61c1e7ee40522d6a80019a',
			tokenB: '0xa52c1dabf83ead8660c40d162b021a0749b404be',
			tokenDecimals: 8,
			minimumLiquidity: 1000,
		},
	]
};
