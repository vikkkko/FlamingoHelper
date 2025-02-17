const { exec } = require('child_process');
const fs = require('fs');
const path = require('path');
const { rpc, wallet, sc, tx, u, experimental, CONST } = require('@cityofzion/neon-js');

// Script directory
const ROOT_DIR = path.resolve(__dirname);

// Connection and account details
const RPC_URL = 'http://0.0.0.0:50012';
const RPC_CLIENT = new rpc.RPCClient(RPC_URL);
const NETWORK_MAGIC = 3029459637;

// Accounts (private keys taken from localnet.neo-express file)
const SUPER_ADMIN = new wallet.Account('f59c390e2d132773dbc4ae6ac5a3704c44f4fcccbbca0ef0dc77c083622d5abb');
const ALICE = new wallet.Account('729be1ea8764f6db810f29439cf3b539f5de94a8502529a1c676531ea793f790');
const BOB = new wallet.Account('6130c6d131b29832c392765e6ac8cc0d88370adbb6bd06f4070934b251c1c9ce');

// Varibles needed to correctly set super admin account for contracts
const OLD_SUPER_ADMIN = {
	address: 'NL1JGjDe22U44R57ZXVSeRa4T7Jo1HDLF4',
	scriptHash: '0x14131211100f0e0d0c0b0a090807060504030201',
};
const NEW_SUPER_ADMIN = SUPER_ADMIN;
const PROJECTS_WITH_SUPER_ADMIN = [
	'Flamingo.FLM',
	'Flamingo.Nep17',
	'Flamingo.Staking',
	'Flamingo.SwapFactory',
	'Flamingo.SwapPair',
	'Flamingo.SwapPairWhiteList',
	'Flamingo.SwapRouter',
	'Flamingo.OrderBook',
];

// Function to run a dotnet command and wait for its completion
async function runCommand(command) {
	return new Promise((resolve, reject) => {
		exec(command, (error, stdout, stderr) => {
			if (error) {
				console.error(`Error executing command: ${error.message}`);
				reject(error);
			} else if (stderr) {
				console.error(`Command error: ${stderr}`);
				reject(stderr);
			} else {
				console.log(`Command output: ${stdout}`);
				resolve(stdout);
			}
		});
	});
}

async function deployContract(nefFilePath, manifestFilePath, account) {
	try {
		// Read the NEF file and manifest from the file system
		const contractBytecode = fs.readFileSync(nefFilePath); // Read NEF as Buffer
		const contractManifest = JSON.parse(fs.readFileSync(manifestFilePath, 'utf8')); // Read manifest as JSON

		// Convert the NEF buffer to an NEF object
		const nefObject = sc.NEF.fromBuffer(contractBytecode);
		const manifestObject = sc.ContractManifest.fromJson(contractManifest);

		console.log(`Deploying ${contractManifest.name} contract...`);
		// Deploy the contract
		await experimental.deployContract(nefObject, manifestObject, {
			networkMagic: NETWORK_MAGIC,
			rpcAddress: RPC_URL,
			account,
			systemFeeOverride: u.BigInteger.fromNumber(20_00000000),
			networkFeeOverride: u.BigInteger.fromNumber(20_00000000),
		});

		// Get the deployed contract script hash
		const scriptHash = experimental.getContractHash(
			u.HexString.fromHex(wallet.getScriptHashFromAddress(account.address)), // Get script hash from account address
			nefObject.checksum, // Use the NEF checksum
			contractManifest.name, // Use the contract name from the manifest
		);

		console.log('Deployed contract hash: 0x' + scriptHash);
		return scriptHash; // Return the calculated script hash
	} catch (error) {
		console.error('Error during contract deployment:', error);
	}
}

async function setupSwapFactory() {
	const swapFactoryHash = await deployContract(
		path.join(ROOT_DIR, 'src', 'Flamingo.SwapFactory', 'bin', 'sc', 'FlamingoSwapFactory.nef'),
		path.join(ROOT_DIR, 'src', 'Flamingo.SwapFactory', 'bin', 'sc', 'FlamingoSwapFactory.manifest.json'),
		NEW_SUPER_ADMIN,
	);

	return swapFactoryHash;
}

async function setupSwapRouter(swapFactoryHash) {
	const swapRouterHash = await deployContract(
		path.join(ROOT_DIR, 'src', 'Flamingo.SwapRouter', 'bin', 'sc', 'FlamingoSwapRouter.nef'),
		path.join(ROOT_DIR, 'src', 'Flamingo.SwapRouter', 'bin', 'sc', 'FlamingoSwapRouter.manifest.json'),
		NEW_SUPER_ADMIN,
	);

	console.log(`Setup swap router...`);
	await RPC_CLIENT.invokeFunction(
		swapRouterHash,
		'setFactory',
		[sc.ContractParam.hash160(swapFactoryHash)],
		[
			{
				account: SUPER_ADMIN.scriptHash,
				scopes: 128,
			},
		],
	);
	console.log(`Setup completed.`);
	return swapRouterHash;
}

async function setupSwapPairWhiteList(swapRouterHash) {
	const swapPairWhiteListHash = await deployContract(
		path.join(ROOT_DIR, 'src', 'Flamingo.SwapPairWhiteList', 'bin', 'sc', 'FlamingoSwapPairWhiteList.nef'),
		path.join(ROOT_DIR, 'src', 'Flamingo.SwapPairWhiteList', 'bin', 'sc', 'FlamingoSwapPairWhiteList.manifest.json'),
		NEW_SUPER_ADMIN,
	);

	console.log(`Setup swap pair whitelist...`);
	await RPC_CLIENT.invokeFunction(
		swapPairWhiteListHash,
		'addRouter',
		[sc.ContractParam.hash160(swapRouterHash)],
		[
			{
				account: SUPER_ADMIN.scriptHash,
				scopes: 128,
			},
		],
	);
	console.log(`Setup completed.`);
	return swapPairWhiteListHash;
}

async function setupTokens() {
	const fUSDTContractHash = await deployContract(
		path.join(ROOT_DIR, 'src', 'Flamingo.Nep17', 'bin', 'sc', 'fUSDT.nef'),
		path.join(ROOT_DIR, 'src', 'Flamingo.Nep17', 'bin', 'sc', 'fUSDT.manifest.json'),
		NEW_SUPER_ADMIN,
	);
	const fWBTCContractHash = await deployContract(
		path.join(ROOT_DIR, 'src', 'Flamingo.Nep17', 'bin', 'sc', 'fWBTC.nef'),
		path.join(ROOT_DIR, 'src', 'Flamingo.Nep17', 'bin', 'sc', 'fWBTC.manifest.json'),
		NEW_SUPER_ADMIN,
	);

	const oneMillion = u.BigInteger.fromNumber(1_000_000_00000000);

	console.log(`Setup fUSDT and fWBTC tokens to accounts...`);
	// Mint tokens for SUPER_ADMIN
	await RPC_CLIENT.invokeFunction(
		fUSDTContractHash,
		'mint',
		[sc.ContractParam.hash160(SUPER_ADMIN.scriptHash), sc.ContractParam.integer(oneMillion)],
		[
			{
				account: SUPER_ADMIN.scriptHash,
				scopes: 128,
			},
		],
	);
	await RPC_CLIENT.invokeFunction(
		fWBTCContractHash,
		'mint',
		[sc.ContractParam.hash160(SUPER_ADMIN.scriptHash), sc.ContractParam.integer(oneMillion)],
		[
			{
				account: SUPER_ADMIN.scriptHash,
				scopes: 128,
			},
		],
	);

	// Mint and transfer to ALICE and BOB
	await RPC_CLIENT.invokeFunction(
		fUSDTContractHash,
		'mint',
		[sc.ContractParam.hash160(ALICE.scriptHash), sc.ContractParam.integer(oneMillion)],
		[
			{
				account: SUPER_ADMIN.scriptHash,
				scopes: 128,
			},
		],
	);
	await RPC_CLIENT.invokeFunction(
		fWBTCContractHash,
		'mint',
		[sc.ContractParam.hash160(ALICE.scriptHash), sc.ContractParam.integer(oneMillion)],
		[
			{
				account: SUPER_ADMIN.scriptHash,
				scopes: 128,
			},
		],
	);

	await RPC_CLIENT.invokeFunction(
		fUSDTContractHash,
		'mint',
		[sc.ContractParam.hash160(BOB.scriptHash), sc.ContractParam.integer(oneMillion)],
		[
			{
				account: SUPER_ADMIN.scriptHash,
				scopes: 128,
			},
		],
	);
	await RPC_CLIENT.invokeFunction(
		fWBTCContractHash,
		'mint',
		[sc.ContractParam.hash160(BOB.scriptHash), sc.ContractParam.integer(oneMillion)],
		[
			{
				account: SUPER_ADMIN.scriptHash,
				scopes: 128,
			},
		],
	);
	console.log(`Setup completed.`);
	return { fUSDTContractHash, fWBTCContractHash };
}

async function setupSwapPairs(swapFactoryHash, swapPairWhiteListHash) {
	const FLPfWBTCfUSDTContractHash = await deployContract(
		path.join(ROOT_DIR, 'src', 'Flamingo.SwapPair', 'bin', 'sc', 'FLPfWBTCfUSDT.nef'),
		path.join(ROOT_DIR, 'src', 'Flamingo.SwapPair', 'bin', 'sc', 'FLPfWBTCfUSDT.manifest.json'),
		NEW_SUPER_ADMIN,
	);

	console.log(`Setup swap pairs...`);
	await RPC_CLIENT.invokeFunction(
		swapFactoryHash,
		'registerExchangePair',
		[sc.ContractParam.hash160(FLPfWBTCfUSDTContractHash)],
		[
			{
				account: SUPER_ADMIN.scriptHash,
				scopes: 128,
			},
		],
	);
	await RPC_CLIENT.invokeFunction(
		FLPfWBTCfUSDTContractHash,
		'setWhiteListContract',
		[sc.ContractParam.hash160(swapPairWhiteListHash)],
		[
			{
				account: SUPER_ADMIN.scriptHash,
				scopes: 128,
			},
		],
	);

	console.log(`Setup completed.`);
	return FLPfWBTCfUSDTContractHash;
}

async function setupStaking(FLPfWBTCfUSDTContractHash) {
	const FLMContractHash = await deployContract(
		path.join(ROOT_DIR, 'src', 'Flamingo.FLM', 'bin', 'sc', 'FLM.nef'),
		path.join(ROOT_DIR, 'src', 'Flamingo.FLM', 'bin', 'sc', 'FLM.manifest.json'),
		NEW_SUPER_ADMIN,
	);
	const StakingContractHash = await deployContract(
		path.join(ROOT_DIR, 'src', 'Flamingo.Staking', 'bin', 'sc', 'FlamingoStaking.nef'),
		path.join(ROOT_DIR, 'src', 'Flamingo.Staking', 'bin', 'sc', 'FlamingoStaking.manifest.json'),
		NEW_SUPER_ADMIN,
	);

	console.log(`Setup staking...`);
	await RPC_CLIENT.invokeFunction(
		StakingContractHash,
		'addAuthor',
		[sc.ContractParam.hash160(SUPER_ADMIN.address)],
		[
			{
				account: SUPER_ADMIN.scriptHash,
				scopes: 128,
			},
		],
	);
	await RPC_CLIENT.invokeFunction(
		StakingContractHash,
		'setFLMAddress',
		[sc.ContractParam.hash160(FLMContractHash), sc.ContractParam.hash160(SUPER_ADMIN.address)],
		[
			{
				account: SUPER_ADMIN.scriptHash,
				scopes: 128,
			},
		],
	);
	await RPC_CLIENT.invokeFunction(
		StakingContractHash,
		'addAsset',
		[sc.ContractParam.hash160(FLPfWBTCfUSDTContractHash), sc.ContractParam.hash160(SUPER_ADMIN.address)],
		[
			{
				account: SUPER_ADMIN.scriptHash,
				scopes: 128,
			},
		],
	);
	console.log(`Setup completed.`);
	return { FLMContractHash, StakingContractHash };
}

async function setupOrderBook(swapRouterContractHash, swapFactoryContract, fWBTCContractHash, fUSDTContractHash) {
	const treeBitLength = 16;
	const priceCoefficient = new u.BigInteger('1000000000000000000');
	// This price precision is two decimal places less than the quote token precision.
	// For example with USDT that has 6 decimal places, we can trade on every 0.000_000_01 USDT.
	const pricePrecision = priceCoefficient / 10000000;

	const orderBookContractHash = await deployContract(
		path.join(ROOT_DIR, 'src', 'Flamingo.OrderBook', 'bin', 'sc', 'FlamingoOrderBook.nef'),
		path.join(ROOT_DIR, 'src', 'Flamingo.OrderBook', 'bin', 'sc', 'FlamingoOrderBook.manifest.json'),
		NEW_SUPER_ADMIN,
	);

	console.log(`Setup orderbook...`);
	await RPC_CLIENT.invokeFunction(
		orderBookContractHash,
		'changeAMMRouter',
		[sc.ContractParam.hash160(swapRouterContractHash)],
		[
			{
				account: SUPER_ADMIN.scriptHash,
				scopes: 128,
			},
		],
	);
	await RPC_CLIENT.invokeFunction(
		orderBookContractHash,
		'changeAMMFactory',
		[sc.ContractParam.hash160(swapFactoryContract)],
		[
			{
				account: SUPER_ADMIN.scriptHash,
				scopes: 128,
			},
		],
	);
	await RPC_CLIENT.invokeFunction(
		orderBookContractHash,
		'changeFeeCollector',
		[sc.ContractParam.hash160(ALICE.scriptHash)],
		[
			{
				account: SUPER_ADMIN.scriptHash,
				scopes: 128,
			},
		],
	);

	await RPC_CLIENT.invokeFunction(
		orderBookContractHash,
		'addPair',
		[
			sc.ContractParam.hash160(fWBTCContractHash),
			sc.ContractParam.hash160(fUSDTContractHash),
			sc.ContractParam.integer(treeBitLength),
			sc.ContractParam.integer(pricePrecision),
		],
		[
			{
				account: SUPER_ADMIN.scriptHash,
				scopes: 128,
			},
		],
	);
	await RPC_CLIENT.invokeFunction(
		orderBookContractHash,
		'unpausePairOrderTrading',
		[sc.ContractParam.integer(1)],
		[
			{
				account: SUPER_ADMIN.scriptHash,
				scopes: 128,
			},
		],
	);
	await RPC_CLIENT.invokeFunction(
		orderBookContractHash,
		'unpausePairOrderManagement',
		[sc.ContractParam.integer(1)],
		[
			{
				account: SUPER_ADMIN.scriptHash,
				scopes: 128,
			},
		],
	);

	await RPC_CLIENT.invokeFunction(
		orderBookContractHash,
		'enableTokenDeposit',
		[sc.ContractParam.hash160(fWBTCContractHash)],
		[
			{
				account: SUPER_ADMIN.scriptHash,
				scopes: 128,
			},
		],
	);
	await RPC_CLIENT.invokeFunction(
		orderBookContractHash,
		'enableTokenWithdraw',
		[sc.ContractParam.hash160(fWBTCContractHash)],
		[
			{
				account: SUPER_ADMIN.scriptHash,
				scopes: 128,
			},
		],
	);
	await RPC_CLIENT.invokeFunction(
		orderBookContractHash,
		'enableTokenDeposit',
		[sc.ContractParam.hash160(fUSDTContractHash)],
		[
			{
				account: SUPER_ADMIN.scriptHash,
				scopes: 128,
			},
		],
	);
	await RPC_CLIENT.invokeFunction(
		orderBookContractHash,
		'enableTokenWithdraw',
		[sc.ContractParam.hash160(fUSDTContractHash)],
		[
			{
				account: SUPER_ADMIN.scriptHash,
				scopes: 128,
			},
		],
	);

	console.log(`Setup completed.`);
	return orderBookContractHash;
}

// Add Liquidity to SwapRouter using the deployed contract hash
async function addLiquidityToSwapRouter(routerHash, fWBTCContractHash, fUSDTContractHash) {
	const baseLiquidityUSDT = u.BigInteger.fromNumber(100_000_000000);
	const baseLiquidityWBTC = u.BigInteger.fromNumber(10_00000000);

	console.log(`Adding swap router liquidity to fWBTC-fUSDT pool...`);
	await RPC_CLIENT.invokeFunction(
		routerHash,
		'addLiquidity',
		[
			sc.ContractParam.hash160(BOB.scriptHash),
			sc.ContractParam.hash160(fWBTCContractHash),
			sc.ContractParam.hash160(fUSDTContractHash),
			sc.ContractParam.integer(baseLiquidityWBTC),
			sc.ContractParam.integer(baseLiquidityUSDT),
			sc.ContractParam.integer(0), // Min liquidity for token 0
			sc.ContractParam.integer(0), // Min liquidity for token 1
			sc.ContractParam.integer(9999999999999), // Deadline
		],
		[
			{
				account: BOB.scriptHash,
				scopes: 128,
			},
		],
	);
	console.log(`Addition completed.`);
}

// Main function to initialize the entire setup
async function initialize() {
	try {
		console.log('');
		console.log('Localnet initialization starting...');

		console.log('');
		console.log('Updating build.js files with localnet super admin account...');
		// Update build.js in projects that has it to use current SUPER_ADMIN account
		for (const project of PROJECTS_WITH_SUPER_ADMIN) {
			// Get file path to extract its data
			const filePath = path.join(ROOT_DIR, 'src', project, 'build.js');
			// NB: PAY ATTENTION TO SPECIAL CHARACTERS IN REGEX AND ESCAPE THEM!
			let content = fs.readFileSync(filePath, 'utf8');
			// Update each mapping
			content = content.replace(new RegExp(`${OLD_SUPER_ADMIN.address}`, 'g'), `${NEW_SUPER_ADMIN.address}`);
			content = content.replace(new RegExp(`${OLD_SUPER_ADMIN.scriptHash}`, 'g'), `0x${NEW_SUPER_ADMIN.scriptHash}`);
			// Write the new defined file overwriting original one
			fs.writeFileSync(filePath, content);
		}
		console.log('Build.js files overwrite completed successfully.');

		// Run the dotnet command and wait for its completion
		console.log('');
		console.log('Running dotnet build command...');
		await runCommand('dotnet build');
		console.log('Dotnet build command completed successfully.');

		let errorOnContractSetup = null;
		let contractHashes = {};
		try {
			console.log('');
			console.log('Deploying and setting up contracts...');
			// Setup needed contracts on localnet (Like in FlamingoTestSuiteBase)
			const swapFactoryHash = await setupSwapFactory();
			const swapRouterHash = await setupSwapRouter(swapFactoryHash);
			const swapPairWhiteListHash = await setupSwapPairWhiteList(swapRouterHash);
			const { fUSDTContractHash, fWBTCContractHash } = await setupTokens();
			const FLPfWBTCfUSDTContractHash = await setupSwapPairs(swapFactoryHash, swapPairWhiteListHash);
			const { FLMContractHash, StakingContractHash } = await setupStaking(FLPfWBTCfUSDTContractHash);
			const orderBookContractHash = await setupOrderBook(
				swapRouterHash,
				swapFactoryHash,
				fWBTCContractHash,
				fUSDTContractHash,
			);

			// Utils function to move things around
			await addLiquidityToSwapRouter(swapRouterHash, fWBTCContractHash, fUSDTContractHash);
			console.log('Contracts deployed and initialized.');

			contractHashes = {
				swapFactoryHash,
				swapRouterHash,
				swapPairWhiteListHash,
				fUSDTContractHash,
				fWBTCContractHash,
				FLPfWBTCfUSDTContractHash,
				FLMContractHash,
				StakingContractHash,
				orderBookContractHash,
			};
		} catch (error2) {
			errorOnContractSetup = error2;
		}

		console.log('');
		console.log('Restoring build.js files with old super admin account...');
		// Update build.js in projects that has it to use current SUPER_ADMIN account
		for (const project of PROJECTS_WITH_SUPER_ADMIN) {
			// Get file path to extract its data
			const filePath = path.join(ROOT_DIR, 'src', project, 'build.js');
			// NB: PAY ATTENTION TO SPECIAL CHARACTERS IN REGEX AND ESCAPE THEM!
			let content = fs.readFileSync(filePath, 'utf8');
			// Update each mapping
			content = content.replace(new RegExp(`${NEW_SUPER_ADMIN.address}`, 'g'), `${OLD_SUPER_ADMIN.address}`);
			content = content.replace(new RegExp(`0x${NEW_SUPER_ADMIN.scriptHash}`, 'g'), `${OLD_SUPER_ADMIN.scriptHash}`);
			// Write the new defined file overwriting original one
			fs.writeFileSync(filePath, content);
		}
		console.log('Build.js files overwrite completed successfully.');

		// Run the dotnet command and wait for its completion
		console.log('');
		console.log('Running dotnet build command...');
		await runCommand('dotnet build');
		console.log('Dotnet build command completed successfully.');

		console.log('');
		console.log(
			errorOnContractSetup
				? 'Error during deployment phase:' + errorOnContractSetup
				: 'Localnet initialized correctly.',
		);

		if (Object.keys(contractHashes).length > 0) {
			console.log('Deployed contracts hashes:');
			Object.keys(contractHashes).forEach((key) => {
				console.log(`${key}: 0x${contractHashes[key]}`);
			});
		}
	} catch (error) {
		console.error('Error during initialization:', error);
	}
}

// Run the initialization
initialize();
