# Deucarian Command Routing

## Asset selection and project defaults

Advanced → Routing settings starts with the bundled default asset. Choose searches installed packages and project assets, Create makes project settings, and Customize copies a selected asset for editing. Bundled settings remain read-only. Selecting settings in the editor is not an implicit runtime router replacement.

## Typed definition workflow

The payload contract is declared once. The startup component registers its real handler; no assembly scanning or domain state assets are needed.

Start with the [Definition Workflow walkthrough](Documentation~/DefinitionWorkflow.md).
Import **Definition Workflow** in Package Manager for a configured sample scene
and short caller scripts. The sample keeps typed contracts and service setup explicit, with reusable
components for scene callers.


For simple calls and setup, see [Simple usage](Documentation~/SimpleUsage.md).

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

Routing preserves a non-null synchronization context present at invocation.
After a pending handler or middleware pipeline completes, dispatch completion,
result encoding, route observers, and transport replies continue on that
context. Unity/browser transports must raise ingress on Unity's context; the
package does not discover or marshal calls to a global main thread. This also
supports context-bound desktop callers. With no synchronization context,
continuations remain context-free as before and do not capture a custom task
scheduler. Handlers and middleware remain responsible for their own awaits
and any thread-affine work they perform. Do not block synchronously on a route
while owning its synchronization context; await it instead.

Subscribe to `RouteCompleted` when a composition root needs one
transport-neutral observation point for every route outcome, including
protocol rejections that do not reach a handler. The event provides the
`CommandRouteOutcome`, effective transport and endpoint, and a bounded
duration. Subscriber failures never change routing results or prevent
other subscribers from being notified, and every subscriber receives a
defensive outcome snapshot so mutable JSON cannot affect another observer or
the caller. The dispatcher's existing `CommandCompleted` event remains the
handler-dispatch-specific contract; its established exception behavior is
preserved while the route completion still fires exactly once.

Every runtime also implements `ICommandRoutePort`. A composition root may
inject that port into `CommandRoutePortBehaviour` when a scene-owned local
ingress is useful—for example, an editor development profile can submit the
same `initialize_viewer` envelope as a browser transport without knowing the
application context type. The behaviour is not a registry and never locates
services; the owning composition root must initialize it explicitly.

## Editor

Open:

**Deucarian Control Center > Communication > Command Routing**

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
