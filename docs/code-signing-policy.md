# Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io/), certificate by [SignPath Foundation](https://signpath.org/).

## What is signed

Only release builds of `deskbridge.exe` and `deskbridge-broker.exe`, built by GitHub Actions from this repository. Nothing built on a personal machine is signed.

## Who does what

| Role | Who |
|---|---|
| Committers and reviewers | Members of the Deskbridge GitHub repository with write access |
| Approvers (each signing request) | The project's maintainers, [@omarkhan2217](https://github.com/omarkhan2217) (repository owner) and [@alikhan866](https://github.com/alikhan866) |

Every change reaches `main` through review by the owner. Every account with write access uses multi-factor authentication on GitHub and SignPath.

## Privacy

Deskbridge makes no network connections of its own and sends no telemetry. The only network traffic is to remote MCP servers that the user adds and signs in to, each shown in Deskbridge's settings with its address. Logs stay on the user's PC.
