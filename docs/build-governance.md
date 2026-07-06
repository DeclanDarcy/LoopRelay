# Build Governance

LoopRelay pins the .NET SDK through `global.json` and centralizes NuGet versions in
`Directory.Packages.props`. Project files should reference packages without local
`Version` attributes.

The prompt source generator is vendored as `src/LoopRelay.Prompts.Generator` and is
referenced as an analyzer by `LoopRelay.Core`. This keeps `*.prompt` files as the
compile-time source of truth without relying on a machine-local sibling checkout.

Package lock files are intentionally deferred for now. The repository is not yet a
published package or tool surface, and the current pre-refactor priority is a clean,
reproducible restore/build from the pinned SDK and centrally governed dependency
versions. Add lock files when release packaging or CI promotion needs deterministic
transitive dependency closure.
