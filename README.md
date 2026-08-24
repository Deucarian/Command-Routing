# Deucarian Command Routing

Transport-independent command dispatch for Unity applications, with JSON
protocol support, mandatory Deucarian logging and diagnostics, and a branded
editor management surface.

## Install

Use the Deucarian Package Installer, or add the Git URL directly:

```json
{
  "com.deucarian.command-routing": "https://github.com/Deucarian/Command-Routing.git#main"
}
```

Use `#develop` for the development channel.

## Runtime composition

Handlers are explicit strategies. They receive their application context
through construction and return a `CommandResult`; they never locate services.

```csharp
ICommandHandler<MyContext>[] handlers =
{
    new SelectItemCommandHandler(selectionService)
};

using var routing = new CommandRoutingRuntime<MyContext>(
    context,
    handlers);

CommandResult result = await routing.RouteJsonAsync(json);
```

The runtime registers a sanitized diagnostics provider, uses
`Deucarian.Logging`, rejects duplicate command names, and keeps a bounded
redacted history.

Every runtime also implements `ICommandRoutePort`. A composition root may
inject that port into `CommandRoutePortBehaviour` when a scene-owned local
ingress is useful—for example, an editor development profile can submit the
same `initialize_viewer` envelope as a browser transport without knowing the
application context type. The behaviour is not a registry and never locates
services; the owning composition root must initialize it explicitly.

## Editor

Open:

`Tools > Deucarian > Communication > Command Routing`

The single management window owns settings creation, live command testing,
runtime diagnostics, and validation. Its **Live Tester** tab can validate or
send a manual JSON envelope through the initialized scene route while the
application is in Play Mode.

Other packages can register an `ICommandTestCatalogSource`. Generated scenarios
then appear in the same tab and can be sent individually or executed as an
ordered automatic sequence. Command Routing owns dispatch and results; the
provider package remains responsible for command-specific examples and can
declare the endpoint that its running transport expects.

The window uses `com.deucarian.editor` for all visual styling.

## Protocol

```json
{
  "protocol_version": 1,
  "command_id": "client-generated-id",
  "command": "select_item",
  "payload": {
    "item_id": 42
  },
  "metadata": {
    "source": "python"
  }
}
```

Legacy envelopes containing only `command` and `payload` remain valid.

## Security

The default redactor masks property names containing `token`, `password`,
`secret`, `authorization`, `credential`, or `api_key`. Logs, diagnostics, and
outbound result envelopes use sanitized values.

## Architecture

This package follows the canonical
[Deucarian Architecture Rules](https://github.com/Deucarian/Package-Registry/blob/main/ARCHITECTURE.md).
Transport-independent command routing, JSON protocol, diagnostics, logging, and editor tooling for Unity.
