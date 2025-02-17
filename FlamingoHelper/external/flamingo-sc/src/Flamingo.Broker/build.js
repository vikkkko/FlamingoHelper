const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');
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
			// Check for uncommitted changes if target is "mainnet" or "testnet"
			if (target === 'mainnet' || target === 'testnet') {
				const hasUncommittedChanges = checkUncommittedChanges();
				if (hasUncommittedChanges) {
					console.error('Error: There are uncommitted changes in the repository. Commit or stash changes before proceeding.');
					process.exit(1);
				}
			}

			const config = loadConfig(target);
			const version = loadVersion();

			// Backup build files and get them back to apply modifications
			const csharpFiles = backupBuildFiles(PROJECT_DIR, BACKUP_DIR);

			// Define replacements
			const replacements = [
				{
					pattern: new RegExp('InitialOwner = "6288e8a5e3d92f6aa645e6f749b4cb6cc024b211";', 'g'),
					replacement: `InitialOwner = "${config.initialOwner}";`,
				},
				{
					pattern: new RegExp('InitialFeeCollector = "6288e8a5e3d92f6aa645e6f749b4cb6cc024b211";', 'g'),
					replacement: `InitialFeeCollector = "${config.initialFeeCollector}";`,
				},
				{
					pattern: new RegExp('InitialAMMRouter = "0x80841bd50d95007cebe45f9cb546f798641fe4c2";', 'g'),
					replacement: `InitialAMMRouter = "${config.ammRouter}";`,
				},
				{
					pattern: new RegExp('InitialAMMFactory = "0x875659fed1972b106ba3adbee9b2fd4f09948f25";', 'g'),
					replacement: `InitialAMMFactory = "${config.ammFactory}";`,
				},
				{
					pattern: new RegExp('ContractTrust\\("02738f9efdd954f8436d91ff5f373ae8af14641abc6511de3d1a2ab40665e9a21f"\\)', 'g'),
					replacement: `ContractTrust("${config.contractTrustGroup}")`,
				},
				{
					pattern: new RegExp('AMMRouterSwapFee = 2500;', 'g'),
					replacement: `AMMRouterSwapFee = ${config.ammRouterSwapFee};`,
				},
				{
					pattern: new RegExp('return "VERSION_PLACEHOLDER";', 'g'),
					replacement: `return "${version}";`,
				},
			];

			// Apply modifications
			const modifiedFiles = new Map(); // To store modified content temporarily
			const replacementCounters = new Map(replacements.map(({ pattern }) => [pattern, 0]));

			for (const file of csharpFiles) {
				const filePath = path.join(PROJECT_DIR, file);
				let content = fs.readFileSync(filePath, 'utf8');

				replacements.forEach(({ pattern, replacement }) => {
					const matches = content.match(pattern);
					if (matches) {
						replacementCounters.set(pattern, replacementCounters.get(pattern) + matches.length);
						content = content.replace(pattern, replacement);
					}
				});

				modifiedFiles.set(filePath, content); // Store modified content without writing
			}

			replacementCounters.forEach((count, pattern) => {
				if (count !== 1) {
					console.error(`Error: Expected exactly one replacement for pattern ${pattern}, but found ${count}.`);
					process.exit(1);
				}
			});

			// Write the files only after all checks pass
			modifiedFiles.forEach((content, filePath) => {
				fs.writeFileSync(filePath, content);
			});

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

function loadConfig(target) {
	const configPath = path.join(PROJECT_DIR, 'buildConfig', `${target}.config.js`);
	if (!fs.existsSync(configPath)) {
		throw new Error(`Config file not found: ${configPath}`);
	}
	return require(configPath);
}

function loadVersion() {
	try {
		const versionPath = path.join(PROJECT_DIR, 'VERSION');
		if (!fs.existsSync(versionPath)) {
			throw new Error('VERSION file not found in project directory.');
		}
		const version = fs.readFileSync(versionPath, 'utf8').trim();
		const commitHash = execSync('git rev-parse --short HEAD', { encoding: 'utf8' }).trim();
		return `${version}-${commitHash}`;
	} catch (error) {
		console.error('Error loading version:', error.message);
		process.exit(1);
	}
}

function checkUncommittedChanges() {
	try {
		const status = execSync('git status --porcelain', { encoding: 'utf8' });
		return status.trim().length > 0;
	} catch (error) {
		console.error('Error checking Git status:', error.message);
		process.exit(1);
	}
}
