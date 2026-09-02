# Changelog

## [0.2.6] - 2026-09-02

### Added

- Added one transport-neutral route-completion event covering protocol
  rejections and every dispatched command outcome without duplicating history.
- Exposed the completed outcome, effective transport and endpoint, and a
  bounded duration while isolating subscriber failures.
- Preserved exact-once route completion when a legacy dispatch-completion
  subscriber throws, without swallowing or changing that legacy exception.
- Isolated every route-completion subscriber with a defensive outcome snapshot
  so mutable JSON payloads cannot alter another observer or the caller result.

## [0.2.5] - 2026-08-31

- Registered the package workflow and a bounded, sanitized local-state card with Deucarian Control Center.
- Removed normal `Tools/Deucarian` menu exposure while preserving the standalone open API.
- Updated the shared Editor dependency to 1.2.0.
- Aligned Diagnostics to 0.1.6 and Logging to 1.0.4.

## [0.2.4] - 2026-08-26

### Changed

- Derived the editor workflow footer from installed package metadata instead
  of a hardcoded package version.
- Updated the exact Diagnostics, Editor, and Logging dependencies for the
  coordinated editor UX release.

## [0.2.3] - 2026-08-25

### Added

- Added a generic catalog-change notification so package-provided command
  scenarios can refresh live when their source data changes.

## [0.2.2] - 2026-08-25

### Changed

- Made the live tester center on one editable, exact command envelope with a
  single send action and a compact viewer connection state.
- Moved generated sequences behind an optional automated-checks foldout and
  let catalogs nominate the example loaded into the command editor.

## [0.2.1] - 2026-08-25

### Fixed

- Let generated command catalogs declare the runtime endpoint used by the live
  Editor route, while preserving the generic local endpoint as the default.

## [0.2.0] - 2026-08-24

### Added

- Added a live Unity Editor command tester that routes manual envelopes through
  the initialized scene command port.
- Added package-extensible generated scenario catalogs with revision token
  replacement, expected outcomes, and automatic sequence execution.

## [0.1.2] - 2026-08-19

### Added

- Added a non-generic `ICommandRoutePort` implemented by every command runtime.
- Added an explicitly injected scene route-port behaviour so editor tooling and
  local adapters can submit the same protocol messages as external transports.

## [0.1.1] - 2026-08-14

### Fixed

- Kept transport shutdown retryable when the underlying transport throws.
- Made bridge disposal terminal and exception-safe while still canceling the
  active dispatch generation and disposing an owned transport.

## [0.1.0] - 2026-07-24

### Added

- Explicit command-handler strategies and immutable alias-aware registry.
- JSON command protocol with typed payload mapping.
- Middleware pipeline and structured command results.
- Mandatory sanitized logging, diagnostics, and bounded history.
- Deucarian-styled editor settings, simulator, diagnostics, and validation.
