const fs = require('fs');
const path = require('path');
const yargs = require('yargs/yargs');
const { hideBin } = require('yargs/helpers');
const { backupBuildFiles, revertBuildFiles, copyArtifactFiles, safeDeleteFolder } = require('../../utils/build-utils');

// Script directory
const PROJECT_DIR = path.resolve(__dirname);
// Backup directory
const BACKUP_DIR = path.join(PROJECT_DIR, 'backup');
// Build directory
const BUILD_DIR = path.join(PROJECT_DIR, 'bin', 'sc');
// Artifact directory
const ARTIFACT_DIR = path.join(PROJECT_DIR, '../', '../', 'tests', 'Flamingo.OrderBook.Tests', 'TestingArtifacts');

// NB: Main assumption, as is today, modifications are done only in root .cs files
yargs(hideBin(process.argv))
	.command(
		'modify [target]',
		'Modify the source files',
		(yargs) => {
			yargs.positional('target', {
				describe: 'The target build configuration',
				type: 'string',
				default: 'unittests',
			});
		},
		({ target }) => {
			const config = loadConfig(target);

			// Backup build files and get them back to apply modifications
			const csharpFiles = backupBuildFiles(PROJECT_DIR, BACKUP_DIR);

			// Do modifications needed cyclying through each csharp file
			for (const file of csharpFiles) {
				// Get file path to extract its data
				const filePath = path.join(PROJECT_DIR, file);

				// Create a file for each build config object and apply modifications
				for (const liquidityPoolConfig of config.liquidityPools) {
					// The new c# class name
					const newContractClassName = formatClassName(liquidityPoolConfig.contractName);

					// NB: PAY ATTENTION TO SPECIAL CHARACTERS IN REGEX AND ESCAPE THEM!
					let content = fs.readFileSync(filePath, 'utf8');
					// If it's not a partial class skip because it doesn't have values to be substituted
					if (content.indexOf('partial class') === -1) continue;

					// Update contract class name
					content = content.replace(new RegExp('class FlamingoSwapPairContract', 'g'), `class ${newContractClassName}`);
					content = content.replace(
						new RegExp('\\[DisplayName\\("Flamingo Swap-Pair Contract"\\)\\]', 'g'),
						`[DisplayName("${newContractClassName}")]`,
					);
					// Update each mapping
					content = content.replace(
						new RegExp('superAdmin = "NdDvLrbtqeCVQkaLstAwh3md8SYYwqWRaE";', 'g'),
						`superAdmin = "${config.superAdmin}";`,
					);
					content = content.replace(
						new RegExp('WhiteListContract = "0xfb75a5314069b56e136713d38477f647a13991b4";', 'g'),
						`WhiteListContract = "${config.whiteListContract}";`,
					);
					content = content.replace(
						new RegExp('Symbol\\(\\) => "FLP-fWBTC-fUSDT"', 'g'),
						`Symbol() => "${liquidityPoolConfig.tokenSymbol}"`,
					);
					content = content.replace(
						new RegExp('TokenA = "0x0000000000000000000000000000000000000000";', 'g'),
						`TokenA = "${liquidityPoolConfig.tokenA}";`,
					);
					content = content.replace(
						new RegExp('TokenB = "0x0000000000000000000000000000000000000000";', 'g'),
						`TokenB = "${liquidityPoolConfig.tokenB}";`,
					);
					content = content.replace(new RegExp('Decimals\\(\\) => 8', 'g'), `Decimals() => ${liquidityPoolConfig.tokenDecimals}`);
					content = content.replace(
						new RegExp('const long MINIMUM_LIQUIDITY = 1000;', 'g'),
						`const long MINIMUM_LIQUIDITY = ${liquidityPoolConfig.minimumLiquidity};`,
					);
					content = content.replace(
						new RegExp('ContractTrust\\("02738f9efdd954f8436d91ff5f373ae8af14641abc6511de3d1a2ab40665e9a21f"\\)', 'g'),
						`ContractTrust("${config.contractTrustGroup}")`,
					);

					// Create new filename based on contract name
					const newFileName = replaceFirstWord(file, newContractClassName);
					// Write the new defined file
					fs.writeFileSync(path.join(PROJECT_DIR, newFileName), content);
				}
			}

			// Delete build folder so that everything is always clear for the new build
			safeDeleteFolder(BUILD_DIR);
		},
	)
	.command(
		'revert',
		'Revert the modifications',
		() => {},
		() => revertBuildFiles(PROJECT_DIR, BACKUP_DIR),
	)
	.command(
		'clean',
		'Do final further cleanups',
		() => {},
		() => {
			// Delete original file that has been built but is useless
			// It has been built because otherwise msbuild trigger an error not finding original file
			const originalFiles = fs.readdirSync(BUILD_DIR).filter((file) => file.startsWith('Flamingo Swap-Pair Contract'));
			originalFiles.forEach((file) => fs.unlinkSync(path.join(BUILD_DIR, file)));
		},
	)
	.command(
		'copy',
		'Copy the artifacts to testing folder',
		() => {},
		() => copyArtifactFiles(BUILD_DIR, ARTIFACT_DIR, path.basename(PROJECT_DIR)),
	)
	.demandCommand(1, 'You must provide a valid command.')
	.help()
	.parse();

function formatClassName(input) {
	// Remove any non-alphanumeric characters and spaces
	return input.replace(/[^a-zA-Z0-9_]/g, '').replace(/\s+/g, '');
}

function replaceFirstWord(originalString, newValue) {
	// Split the string into an array of words
	let parts = originalString.split('.');
	// Remove the first element of the array and replace it
	parts.shift();
	parts.unshift(newValue);
	// Join the parts back into a single string with periods
	return parts.join('.');
}

function loadConfig(target) {
	const configPath = path.join(PROJECT_DIR, 'buildConfig', `${target}.config.js`);
	if (!fs.existsSync(configPath)) {
		throw new Error(`Config file not found: ${configPath}`);
	}
	return require(configPath);
}
