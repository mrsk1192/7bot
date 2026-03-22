# Architecture

## Phase 2 Summary

`mnetSevenDaysBridge` is a client-side localhost bridge for 7 Days to Die.  
Phase 2 keeps the Phase 1 transport and state APIs, then adds a broad action interface, a held-input state machine, and a main-thread input injection path.

## Why HTTP + Polling

- It is easy to inspect on Windows and Unity/Mono.
- It keeps the localhost-only security boundary explicit.
- It is stable enough for Phase 2 input dispatch without introducing WebSocket complexity yet.

## Mod Side Components

- `Lifecycle`
  Loads config, initializes logging, wires the runtime behaviour, and starts the HTTP server.
- `CommandQueue`
  Accepts input commands from the HTTP thread, rate-limits them, and hands them to the Unity main thread.
- `InputStateMachine`
  Owns held state, one-frame pulses, toggles, idempotent start/stop semantics, and `stop_all_input`.
- `InputAdapter`
  Drains the command queue, validates supported actions, updates the state machine, and applies state to the active backend.
- `InternalInputBackend`
  Uses `EntityPlayerLocal`, `OnValue_InputMoveVector`, `OnValue_InputSmoothLook`, and `PlayerActionsLocal` to inject supported controls.
- `OSInputBackend`
  Phase 2 placeholder only. It exists as an interface-compatible fallback design point but is not active.
- `GameStateCollector`
  Returns player/game state and the bridge-managed `input_state`, plus best-effort UI state.
- `HttpCommandReceiver`
  Exposes `GET` state APIs and `POST /api/command` for action dispatch.

## Threading Model

- HTTP requests are handled on a background listener thread.
- Input commands are enqueued immediately.
- Actual game input mutation happens in `BridgeRuntimeBehaviour.Update()` on the Unity main thread.
- `stop_all_input` and `emergency_neutral_state` always clear bridge-held state before the next frame apply.

## Python Side Components

- `client.py`
  Low-level HTTP client plus high-level action helpers.
- `commands.py`
  Maps human-readable actions such as `move_forward` or `attack_primary` to backend command names.
- `models.py`
  Dataclasses for state, capabilities, and action results.
- `examples/smoke_test.py`
  Basic connectivity and schema verification.
- `examples/phase2_actions_smoke_test.py`
  Sequential action test / example script for supported and unsupported Phase 2 actions.

## Expansion Path

- A future WebSocket backend can reuse the same `CommandQueue`, `InputStateMachine`, and backend abstractions.
- Additional control backends can plug into `IInputBackend`.
- More UI-aware strict open/close helpers can be added without changing the Python high-level API shape.
