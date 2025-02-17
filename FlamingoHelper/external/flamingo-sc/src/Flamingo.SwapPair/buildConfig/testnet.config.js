module.exports = {
	// The hash of the super admin
	superAdmin: 'NMXY5eaTH1jBTMW8DinT4sRX8oSJ2RrNdK',
	// The public key of the group that the contract trusts
	contractTrustGroup: '02738f9efdd954f8436d91ff5f373ae8af14641abc6511de3d1a2ab40665e9a21f',
	// The hash of the white list contract
	whiteListContract: '0xfb75a5314069b56e136713d38477f647a13991b4',
	// The liquidity pool contracts to build
	liquidityPools: [
		// Release candidate pools
		{
			contractName: 'FLP-WBTC-USDT-rc6',
			tokenSymbol: 'FLP-WBTC-USDT-rc6',
			tokenA: '0xcfaa772f2e8aaa26f4b9c711c063738761ee176f',
			tokenB: '0x1eb6e6cd28636ecd8d1a3b7dacb10a310a03265c',
			tokenDecimals: 8,
			minimumLiquidity: 1000,
		},
		{
			contractName: 'FLP-FLM-WBTC-rc6',
			tokenSymbol: 'FLP-FLM-WBTC-rc6',
			tokenA: '0x5008d66c3e6d164eb2ed7f4f39f5b4f31f910f90',
			tokenB: '0xcfaa772f2e8aaa26f4b9c711c063738761ee176f',
			tokenDecimals: 8,
			minimumLiquidity: 1000,
		},
		{
			contractName: 'FLP-FLM-USDT-rc6',
			tokenSymbol: 'FLP-FLM-USDT-rc6',
			tokenA: '0x5008d66c3e6d164eb2ed7f4f39f5b4f31f910f90',
			tokenB: '0x1eb6e6cd28636ecd8d1a3b7dacb10a310a03265c',
			tokenDecimals: 8,
			minimumLiquidity: 1000,
		},
		{
			contractName: 'FLP-WETH-USDT-rc6',
			tokenSymbol: 'FLP-WETH-USDT-rc6',
			tokenA: '0xb0741fc6012f2c199ffc2ea09eb796849698bfcf',
			tokenB: '0x1eb6e6cd28636ecd8d1a3b7dacb10a310a03265c',
			tokenDecimals: 8,
			minimumLiquidity: 1000,
		},
		{
			contractName: 'FLP-WETH-WBTC-rc6',
			tokenSymbol: 'FLP-WETH-WBTC-rc6',
			tokenA: '0xb0741fc6012f2c199ffc2ea09eb796849698bfcf',
			tokenB: '0xcfaa772f2e8aaa26f4b9c711c063738761ee176f',
			tokenDecimals: 8,
			minimumLiquidity: 1000,
		},
		{
			contractName: 'FLP-BNB-USDT-rc6',
			tokenSymbol: 'FLP-BNB-USDT-rc6',
			tokenA: '0x31070bc04c82dec4164a3ff5c9fdc37f050cdeb5',
			tokenB: '0x1eb6e6cd28636ecd8d1a3b7dacb10a310a03265c',
			tokenDecimals: 8,
			minimumLiquidity: 1000,
		},
		{
			contractName: 'FLP-XRP-USDT-rc6',
			tokenSymbol: 'FLP-XRP-USDT-rc6',
			tokenA: '0x480234aac86cbd8c226b530c72e9c7be9b938e9d',
			tokenB: '0x1eb6e6cd28636ecd8d1a3b7dacb10a310a03265c',
			tokenDecimals: 8,
			minimumLiquidity: 1000,
		},
		{
			contractName: 'FLP-DOGE-USDT-rc6',
			tokenSymbol: 'FLP-DOGE-USDT-rc6',
			tokenA: '0x4678e8d214ab621859fc249dd4689eae3756b244',
			tokenB: '0x1eb6e6cd28636ecd8d1a3b7dacb10a310a03265c',
			tokenDecimals: 8,
			minimumLiquidity: 1000,
		},
		{
			contractName: 'FLP-SHIB-USDT-rc6',
			tokenSymbol: 'FLP-SHIB-USDT-rc6',
			tokenA: '0x559128b1ff2b9efd5d9f2b3c51aeb8733c85eab5',
			tokenB: '0x1eb6e6cd28636ecd8d1a3b7dacb10a310a03265c',
			tokenDecimals: 8,
			minimumLiquidity: 1000,
		},
		{
			contractName: 'FLP-PEPE-USDT-rc6',
			tokenSymbol: 'FLP-PEPE-USDT-rc6',
			tokenA: '0xaa06c894ab02c22624720de0f37a18c68791d707',
			tokenB: '0x1eb6e6cd28636ecd8d1a3b7dacb10a310a03265c',
			tokenDecimals: 8,
			minimumLiquidity: 1000,
		},
		{
			contractName: 'FLP-USDT-FUSD-rc6',
			tokenSymbol: 'FLP-USDT-FUSD-rc6',
			tokenA: '0xdc53f971b97b46a2cb3fd1dfce4c2762496f124d',
			tokenB: '0x1eb6e6cd28636ecd8d1a3b7dacb10a310a03265c',
			tokenDecimals: 8,
			minimumLiquidity: 1000,
		},
		{
			contractName: 'FLP-bNEO-USDT-rc6',
			tokenSymbol: 'FLP-bNEO-USDT-rc6',
			tokenA: '0x833b3d6854d5bc44cab40ab9b46560d25c72562c',
			tokenB: '0x1eb6e6cd28636ecd8d1a3b7dacb10a310a03265c',
			tokenDecimals: 8,
			minimumLiquidity: 1000,
		},
		// NGD TEST POOLS
		{
			contractName: 'FLP-TEST2-USDC-rc7',
			tokenSymbol: 'FLP-TEST2-USDC-rc7',
			tokenA: '0xf419d6f93da6de039e5d84f644680ddb86d6bfdf',
			tokenB: '0xe62176f6b6a77439a834a4bfddb8bd41e2bf0b53',
			tokenDecimals: 8,
			minimumLiquidity: 1000,
		},
	]
};
