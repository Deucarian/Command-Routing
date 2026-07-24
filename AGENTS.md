# Deucarian Command Routing Agent Notes

Package ID: `com.deucarian.command-routing`
Repository: `Deucarian/Command-Routing`

Follow the canonical
[Deucarian Architecture Rules](https://github.com/Deucarian/Package-Registry/blob/main/ARCHITECTURE.md).

## Ownership

This package owns:

- Transport-independent command envelopes, handler strategies, dispatch,
  middleware, JSON protocol, redaction, bounded history, diagnostics, and its
  package-specific editor management surface.

Registered capabilities:

- `command-routing`
- `command-protocol`

This package must not own:

- Socket implementations, browser bridges, application commands, domain
  state, API endpoints, authentication state, or service location.

## Dependencies

- `com.deucarian.logging`: mandatory package logging.
- `com.deucarian.diagnostics`: mandatory operational diagnostics.
- `com.deucarian.editor`: mandatory shared editor shell.
- `com.unity.nuget.newtonsoft-json`: JSON protocol and typed payload mapping.

## Policies

- Dependencies are injected; do not add a service locator.
- Handler registration is explicit and immutable.
- Logs, diagnostics, history, and errors must redact secrets.
- Keep all editor actions in
  `Tools > Deucarian > Communication > Command Routing`.
- Do not add separate validation menu items.

## Validation

```powershell
python C:/Repositories/Package-Registry/Tools/deucarian_package_validator.py --registry-root C:/Repositories/Package-Registry --repository-root . --config deucarian-package.json
```

Run Unity EditMode tests and `git diff --check` before committing.
