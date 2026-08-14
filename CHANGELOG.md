# Changelog

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
