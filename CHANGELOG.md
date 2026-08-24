# Changelog

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
