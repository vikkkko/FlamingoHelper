const fs = require('fs');
const path = require('path');
const yargs = require('yargs/yargs');
const { hideBin } = require('yargs/helpers');
const { backupBuildFiles, revertBuildFiles, copyArtifactFiles, safeDeleteFolder } = require('../../utils/build-utils');

// Build the contract with specified info
const BUILD_CONFIG = {
	// The hash of the initial owner
	initialOwner: 'NMXY5eaTH1jBTMW8DinT4sRX8oSJ2RrNdK',
	// The public key of the group that the contract trusts
	contractTrustGroup: '02738f9efdd954f8436d91ff5f373ae8af14641abc6511de3d1a2ab40665e9a21f',
	// The timestamp in ms for staking
	startStakingTimeStamp: 1601114400,
	// The timestamp in ms for claiming
	startClaimTimeStamp: 1601269200,
};

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
		'modify',
		'Modify the source files',
		() => {},
		() => {
			// Backup build files and get them back to apply modifications
			const csharpFiles = backupBuildFiles(PROJECT_DIR, BACKUP_DIR);

			// Do modifications needed cyclying through each csharp file
			// NB: Each file is saved along with original one to have both in the build
			for (const file of csharpFiles) {
				// Get file path to extract its data
				const filePath = path.join(PROJECT_DIR, file);

				// NB: PAY ATTENTION TO SPECIAL CHARACTERS IN REGEX AND ESCAPE THEM!
				let content = fs.readFileSync(filePath, 'utf8');
				// Update each mapping
				content = content.replace(
					new RegExp('InitialOwner = "NaBUWGCLWFZTGK4V9f4pecuXmEijtGXMNX";', 'g'),
					`InitialOwner = "${BUILD_CONFIG.initialOwner}";`,
				);
				content = content.replace(
					new RegExp('uint StartStakingTimeStamp = 1601114400;', 'g'),
					`uint StartStakingTimeStamp = ${BUILD_CONFIG.startStakingTimeStamp};`,
				);
				content = content.replace(
					new RegExp('uint StartClaimTimeStamp = 1601269200;', 'g'),
					`uint StartClaimTimeStamp = ${BUILD_CONFIG.startClaimTimeStamp};`,
				);
				content = content.replace(
					new RegExp('ContractTrust\\("02738f9efdd954f8436d91ff5f373ae8af14641abc6511de3d1a2ab40665e9a21f"\\)', 'g'),
					`ContractTrust("${BUILD_CONFIG.contractTrustGroup}")`,
				);

				// Write the new defined file overwriting original one
				fs.writeFileSync(filePath, content);
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
			// Nothing for this contract
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
