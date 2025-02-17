const yargs = require('yargs/yargs');
const fs = require('fs');
const path = require('path');
const {hideBin} = require("yargs/helpers");
const {wallet, sc, u, experimental, rpc} = require('@cityofzion/neon-js');

const NETWORK_MAGIC = 894710606;
const RPC_URL = 'http://seed1t5.neo.org:20332';
const RPC_CLIENT = new rpc.RPCClient(RPC_URL);
const SUPER_ADMIN = new wallet.Account('f59c390e2d132773dbc4ae6ac5a3704c44f4fcccbbca0ef0dc77c083622d5abb');

async function deployContract(account, nefFilePath, manifestFilePath, data = null) {
	try {
		// Read the NEF file and manifest from the file system
		const contractBytecode = fs.readFileSync(nefFilePath); // Read NEF as Buffer
		const contractManifest = JSON.parse(fs.readFileSync(manifestFilePath, 'utf8')); // Read manifest as JSON

		// Convert the NEF buffer to an NEF object
		const nefObject = sc.NEF.fromBuffer(contractBytecode);
		const manifestObject = sc.ContractManifest.fromJson(contractManifest);

		console.log(`Deploying ${contractManifest.name} contract...`);

		const args = [
			{
				"type": "ByteArray",
				"value": u.HexString.fromHex(nefObject.serialize(), false).toBase64(),
			},
			{
				"type": "String",
				"value": JSON.stringify(manifestObject.toJson()),
			}
		];

		if (data != null) {
			// Validate that data argument is base64 encoded
			if (!Buffer.from(data, 'base64').toString('base64') === data) {
				throw new Error('Data argument must be base64 encoded.');
			}

			args.push({
				"type": "ByteArray",
				"value": data,
			});
		}

		const result = await RPC_CLIENT.invokeFunction(
			'0xfffdc93764dbaddd97c48f252a53ea4643faa3fd',
			'deploy',
			args,
			[
				{
					account: `0x${wallet.getScriptHashFromAddress(account.address)}`,
					scopes: "Global",
					"allowedcontracts": [],
					"allowedgroups": [],
				},
			],
		)

		if (result.state !== 'HALT') {
			console.error('Contract deployment test invoke failed:', result.exception);
			console.log('Result:', result);
			return;
		}

		const gasconsumed = result.gasconsumed;

		// Deploy the contract
		const txId = await experimental.deployContract(nefObject, manifestObject, {
			networkMagic: NETWORK_MAGIC,
			rpcAddress: RPC_URL,
			account,
			systemFeeOverride: u.BigInteger.fromNumber(gasconsumed),
		});
		console.log('Contract deployed successfully. Transaction ID:', txId);

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

async function createContractUpdateParameters(account, nefFilePath, manifestFilePath) {
	// Print out the contract NEF as base64 and the manifest JSON as a string with no whitespace
	const contractBytecode = fs.readFileSync(nefFilePath);
	const nefObject = sc.NEF.fromBuffer(contractBytecode);
	const nefBase64 = u.HexString.fromHex(nefObject.serialize(), false).toBase64();
	console.log('NEF as base64:');
	console.log(nefBase64);

	const manifestJsonFile = fs.readFileSync(manifestFilePath, 'utf8');
	const manifestJson = JSON.parse(manifestJsonFile);
	const manifestString = JSON.stringify(manifestJson);
	console.log('Manifest as JSON string:');
	console.log(manifestString);
}

yargs(hideBin(process.argv))
	.command(
		'deploy <nef> <manifest> [data]',
		'Deploy the contract to testnet.',
		(yargsCmd) => {
			yargsCmd.positional('nef', {
				describe: 'Path to the NEF file',
				type: 'string',
			});
			yargsCmd.positional('manifest', {
				describe: 'Path to the manifest file',
				type: 'string',
			});
			yargsCmd.positional('data', {
				describe: 'Base64 encoded data to pass to the contract',
				type: 'string',
				default: null,
			});
			yargsCmd.demandOption(['nef', 'manifest']);
		},
		(argv) => {
			const nef = argv.nef;
			const manifest = argv.manifest;
			const data = argv.data;
			deployContract(SUPER_ADMIN, nef, manifest, data);
		},
	)
	.command(
		'create-update-params <nef> <manifest>',
		'Create the contract update parameters.',
		(yargsCmd) => {
			yargsCmd.positional('nef', {
				describe: 'Path to the NEF file',
				type: 'string',
			});
			yargsCmd.positional('manifest', {
				describe: 'Path to the manifest file',
				type: 'string',
			});
			yargsCmd.demandOption(['nef', 'manifest']);
		},
		(argv) => {
			const nef = argv.nef;
			const manifest = argv.manifest;
			createContractUpdateParameters(SUPER_ADMIN, nef, manifest);
		}
	)
	.demandCommand(1, 'You must provide a valid command.')
	.help()
	.parse();
