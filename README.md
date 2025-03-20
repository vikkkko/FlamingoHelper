# Flamingo 合约部署工具

这是一个便捷部署 Flamingo 合约的工具。由于 Flamingo 合约的部署需要多个合约的配合，此工具可以简化整个部署流程。

## 常用命令

| 命令 | 说明 |
|------|------|
| `dotnet run mainnet execute deploy broker` | 部署 Broker 合约 |
| `dotnet run mainnet execute deploy router` | 部署 Router 合约 |
| `dotnet run mainnet execute deploy factory` | 部署 Factory 合约 |
| `dotnet run mainnet execute deploy pair 1` | 部署 Pair 合约 <br><sub>- 需要先配置好 `helper.mainnet.json` 或 `helper.testnet.json` 文件<br>- 参数 `1` 代表 pairId，需要与配置文件中的 pairId 保持一致</sub> |
| `dotnet run mainnet execute addLiquidity 1 1000000000000000000 1000000000000000000` | 添加流动性 <br><sub>- 需要指定 pairId 和两个 token 的数量<br>- 第一个数字是 baseToken 的数量<br>- 第二个数字是 quoteToken 的数量<br>- 具体数值在 config 中配置</sub> |
| `dotnet run mainnet execute registPair 1` | 注册 Pair <br><sub>需要提前在 config 中配置 treeBitLength 和 pricePrecision</sub> |


