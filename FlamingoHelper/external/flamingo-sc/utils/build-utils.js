const fs = require('fs');
const path = require('path');

function backupBuildFiles(projectDir, backupDir) {
	// Create backup folder
	if (!fs.existsSync(backupDir)) fs.mkdirSync(backupDir, { recursive: true });
	// Find all c# original files
	const csharpFiles = fs.readdirSync(projectDir).filter((file) => file.endsWith('.cs'));
	// Backup all c# files to backup folder with backup extension to avoid having them built
	csharpFiles.forEach((file) => fs.copyFileSync(path.join(projectDir, file), path.join(backupDir, file + '.backup')));
	// Return files
	return csharpFiles;
}

function revertBuildFiles(projectDir, backupDir) {
	// Delete all .cs files in the folder, to be sure everything is clean
	const csharpFiles = fs.readdirSync(projectDir).filter((file) => file.endsWith('.cs'));
	csharpFiles.forEach((file) => fs.unlinkSync(path.join(projectDir, file)));

	// Find backup files
	const backupFiles = fs.readdirSync(backupDir).filter((file) => file.endsWith('.backup'));
	// Copy all backup files back to root folder back with original .cs extension removing backup one
	backupFiles.forEach((file) =>
		fs.copyFileSync(path.join(backupDir, file), path.join(projectDir, file.substring(0, file.lastIndexOf('.')))),
	);

	// Clean up backup directory
	fs.rmdirSync(backupDir, { recursive: true });
}

function copyArtifactFiles(buildDir, targetDir, projectName) {
	// The json file describing testing artifacts
	let artifactConfig = { builtOn: 0, files: [] };
	// Read artifact .json file with _ in front so that it's at the beginning
	const artifactConfigPath = path.join(targetDir, '_' + projectName + '.artifacts.json');
	if (fs.existsSync(artifactConfigPath)) {
		artifactConfig = JSON.parse(fs.readFileSync(artifactConfigPath, 'utf8'));
	}

	// Delete all old artifact files
	artifactConfig.files.forEach((file) => {
		const targetFilePath = path.join(targetDir, file);
		if (fs.existsSync(targetFilePath)) fs.unlinkSync(targetFilePath);
	});

	// Get all files to copy from build folder
	const artifactFiles = fs
		.readdirSync(buildDir)
		.filter((file) => file.endsWith('.nefdbgnfo') || file.endsWith('.artifacts.cs'));
	// Copy them in target folder
	artifactFiles.forEach((file) => fs.copyFileSync(path.join(buildDir, file), path.join(targetDir, file)));

	// Write updated artifact config file
	fs.writeFileSync(
		artifactConfigPath,
		JSON.stringify({ builtOn: formatUnixTimestamp(Date.now()), files: [...artifactFiles] }, null, 2),
		'utf8',
	);
}

function safeDeleteFolder(targetDir) {
	if (fs.existsSync(targetDir)) fs.rmdirSync(targetDir, { recursive: true });
}

function formatUnixTimestamp(unixTimestampMS) {
	const date = new Date(unixTimestampMS);
	const options = {
		hour: '2-digit',
		minute: '2-digit',
		second: '2-digit',
		day: '2-digit',
		month: '2-digit',
		year: 'numeric',
		timeZone: 'Europe/Rome',
		timeZoneName: 'short',
	};
	// Format the date and time with options
	return date.toLocaleString('en-GB', options).replace(',', '');
}

module.exports = {
	backupBuildFiles,
	revertBuildFiles,
	copyArtifactFiles,
	safeDeleteFolder,
};
