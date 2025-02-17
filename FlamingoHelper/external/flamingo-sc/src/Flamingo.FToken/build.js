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
				for (const tokenConfig of config.tokens) {
					// The new c# class name
					const newContractClassName = formatClassName(tokenConfig.contractName);
					console.log(`Processing ${newContractClassName}...`);

					// NB: PAY ATTENTION TO SPECIAL CHARACTERS IN REGEX AND ESCAPE THEM!
					let content = fs.readFileSync(filePath, 'utf8');
					// If it's not a partial class skip because it doesn't have values to be substituted
					if (content.indexOf('partial class') === -1) continue;

					// Update contract class name
					content = content.replace(new RegExp('class FToken', 'g'), `class ${newContractClassName}`);
					// Update each mapping
					content = content.replace(new RegExp('DisplayName\\("Token Name"\\)', 'g'), `DisplayName("${tokenConfig.tokenName}")`);
					content = content.replace(new RegExp('get => "Test Token";', 'g'), `get => "${tokenConfig.tokenSymbol}";`);
					content = content.replace(new RegExp('Factor => 8;', 'g'), `Factor => ${tokenConfig.tokenDecimals};`);
					content = content.replace(
						new RegExp('owner = "NhGobEnuWX5rVdpnuZZAZExPoRs5J6D2Sb";', 'g'),
						`owner = "${config.owner}";`,
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
			const originalFiles = fs.readdirSync(BUILD_DIR).filter((file) => file.startsWith('Nep17Token'));
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
