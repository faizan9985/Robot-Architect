# Development credentials

Robot Architect does not require Meta DevAgent authentication for gameplay.
Keep DevAgent disabled and both token fields empty in
`Assets/Resources/DevAgentSettings.asset`. Preserve this safe configuration in Git.

Meta SDK 209's build processor injects the Editor's AgentBridge token even when
DevAgent is disabled. `DevAgentCredentialBuildGuard` runs afterwards, clears the
injected token, and rejects builds if DevAgent is enabled or a voice token is set.
Review this safeguard when updating the Meta SDK or adding build processors.

AgentBridge's own token storage uses user-specific Unity EditorPrefs, outside
the repository. If other tooling needs local credential files, use the ignored
root `.local-credentials/` directory or root `.env` files. Never place credentials
under Assets, Resources, StreamingAssets, Packages, or ProjectSettings.

Previously generated local builds and caches may contain the old token. They
remain excluded from Git and have not been deleted or rewritten. Regenerate the
AgentBridge server token through its Editor settings before sharing old builds
or enabling remote access. Git ignore rules do not protect previously tracked files.
