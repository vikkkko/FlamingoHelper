const fs = require('fs');
const path = require('path');
const {wallet, sc, u, experimental} = require('@cityofzion/neon-js');

const SUPER_ADMIN = new wallet.Account('f59c390e2d132773dbc4ae6ac5a3704c44f4fcccbbca0ef0dc77c083622d5abb');

function getContractHash(nefFilePath, manifestFilePath, account) {
	// Read the NEF file and manifest from the file system
	const contractBytecode = fs.readFileSync(nefFilePath); // Read NEF as Buffer
	const contractManifest = JSON.parse(fs.readFileSync(manifestFilePath, 'utf8')); // Read manifest as JSON

	// Convert the NEF buffer to an NEF object
	const nefObject = sc.NEF.fromBuffer(contractBytecode);
	const manifestObject = sc.ContractManifest.fromJson(contractManifest);

	// Get the deployed contract script hash
	const scriptHash = experimental.getContractHash(
		u.HexString.fromHex(wallet.getScriptHashFromAddress(account.address)), // Get script hash from account address
		nefObject.checksum, // Use the NEF checksum
		contractManifest.name, // Use the contract name from the manifest
	);

	return scriptHash;
}

function writeGroupToManifest(manifestFilePath, group) {
	const manifest = JSON.parse(fs.readFileSync(manifestFilePath, 'utf8'));
	manifest.groups = [group];
	fs.writeFileSync(manifestFilePath, JSON.stringify(manifest, null, 4));
}

function addContractGroupToContract(contractPath, account) {
	const nefFilePath = path.join(__dirname, `../${contractPath}.nef`);
	const manifestFilePath = path.join(__dirname, `../${contractPath}.manifest.json`);
	const scriptHash = getContractHash(nefFilePath, manifestFilePath, account);
	const group = {
		pubKey: account.publicKey,
		signature: wallet.sign(scriptHash, account.privateKey)
	}
	const isVerified = wallet.verify(scriptHash, group.signature, account.publicKey);

	console.log(`  Contract: ${contractPath}`);
	console.log(`  Script hash: ${scriptHash}`);
	console.log(`  Group: ${JSON.stringify(group)}`);
	console.log(`  Signature verified: ${isVerified}`);

	writeGroupToManifest(manifestFilePath, group);
	console.log(`  Group written to manifest file: ${manifestFilePath}`);
}

function main() {
	const contractProjectPaths = [
		'src/Flamingo.OrderBook',
		'src/Flamingo.Staking',
		'src/Flamingo.SwapFactory',
		'src/Flamingo.SwapPair',
		'src/Flamingo.SwapPairWhiteList',
		'src/Flamingo.SwapRouter',
	];

	// The smart contract nef and manifies is located in for example src/Flamingo.SwapFactory/bin/sc/Flamingo.SwapFactory.nef and src/Flamingo.SwapFactory/bin/sc/Flamingo.SwapFactory.manifest.json
	for (const contractProjectPath of contractProjectPaths) {
		// Find contracts ending with .manifest.json
		const contractFilesInDir = fs.readdirSync(path.join(__dirname, `../${contractProjectPath}/bin/sc`))
			.filter(file => file.endsWith(`.manifest.json`))
			.map(file => file.replace('.manifest.json', ''));

		for (const contractFile of contractFilesInDir) {
			const contractPath = `${contractProjectPath}/bin/sc/${contractFile}`;
			const contractFullPath = path.join(__dirname, `../${contractPath}`);

			console.log(`Adding group to contract: ${contractFullPath}`);
			addContractGroupToContract(contractPath, SUPER_ADMIN);
			console.log();
		}
	}
}

main();
