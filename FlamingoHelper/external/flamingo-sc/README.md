# flamingo-sc-monorepo

## Requirements

- .NET 8
- nodejs >= 18

## Setup

### Syncing submodules

Syncing submodules so that we have the latest version of the neo-devpack-dotnet:

```bash
git submodule update --init --recursive
```

### Building neo-devpack-dotnet

To run contracts build commands you have to build the dotnet devpack. Go into **neo-devpack-dotnet** folder and execute:

```bash
dotnet build
```

### Building contracts

To build all the contracts, go into the root of the project and execute:

```bash
dotnet restore
```

and

```bash
dotnet build
```

### Installing node dependencies

To install node packages run:

```bash
npm install
```

## Build

Each contract project inside the src folder has a **build.js** file that is called during msbuild process while building the contract.

It contains 4 commands that are executed at different stages of contract build and allow for contract output customization for building and testing.

### Modify

Executed prior to nccs contract build. It allows contract code customization, like replacing some constant values, modifying defined contract values.

To avoid applying persistent changes to the code, it creates a backup of the original files in a temporary folder so that changes can be reverted after building process is executed.

### Revert

Clean modified contract using temporary backup folder and remove it.

### Clean

Apply further cleanups if needed. mainly in contract build output directory (/bin/sc).

### Copy

Copy the artifacts generated with the nccs build to the "TestingArtifact" folder so that they can be used for testing (.artifacts.cs and .nefdbgnfo files). It also do some file naming cleanup so that the .artifact.cs file generated is always a valid c# file.

While doing so it also generates a "_ + PROJECT_NAME + .artifacts.json" file containing info about last build execution. This file is used to have informations regarding last build and files built.

## Testing

The **FlamingoTestSuiteBase.cs** class inside **/tests/Flamingo.OrderBook.Tests** folder is used to initialize the testing environment, like creating and initializing all needed contracts, and execute tests on the orderbook.

In order to run the tests inside the file just run the command:

```bash
dotnet test
```

CreateTestEngine() method is called when initializing test engine and it's the place where contracts initialization should take place.

All the methods with the decorator **[TestMethod]** inside the **FlamingoTestSuiteBase.cs** class will be executed on the test engine during tests execution.

## Localnet

The project supports the starting of a localnet with automatic deploy of all needed contracts for the Flamingo frontend.

To use the localnet you can use the Alice and Bob wallets that are already configured with tokens amounts. You can find their data in the localnet.neo-express file.

### How to run

#### Start localnet

Start localnet at 0.0.0.0 at port 50012 based on neo-express file configuration:

```bash
npm run localnet:start
```

Init contracts:

```bash
npm run localnet:init
```

Remember, each time the localnet is started it clears the state of what was saved before doing a neoxp reset. In this way, each localnet session is always the same. If you want something to be always there when the localnet is prepared (start + init), add needed invocations to the localnet.neo-express.js file.

#### Stop localnet

```bash
npm run localnet:stop
```

### Use a wallet

Watch into localnet.neo-express file. There you can find the wallet with the 3 private keys of the 3 main accounts used on the localnet.

To use an account with your wallet, remember to add your localnet network with the data in the file at ip 0.0.0.0 and add the wallet account you need to your preferred neo wallet and you are good to go.

The config should be:

- **Name**: Localnet
- **RPC URL**: http://0.0.0.0:50012
- **Magic**: 3029459637

### Interact from a client

In order to use the localnet and connect via http, remember to use a local version of the web app you want to use.

For example, if you want to test some invocation with neonova.space, run neonova on your pc with 'npm run dev' command and interact from there.
