from __future__ import annotations

from dataclasses import replace
from typing import Dict, List, Optional

from .command_models import AgentCommand, CommandStatus


class CommandQueue:
    """Deterministic command queue with explicit state transitions for GUI control."""

    def __init__(self) -> None:
        self._commands: List[AgentCommand] = []
        self._running_command_id: Optional[str] = None

    def list_commands(self) -> List[AgentCommand]:
        return list(self._commands)

    def get(self, command_id: str) -> Optional[AgentCommand]:
        for command in self._commands:
            if command.command_id == command_id:
                return command
        return None

    def current(self) -> Optional[AgentCommand]:
        if self._running_command_id is None:
            return None
        return self.get(self._running_command_id)

    def enqueue(self, command: AgentCommand) -> AgentCommand:
        self._commands.append(command.with_status(CommandStatus.QUEUED))
        self._sort_queue()
        return next(item for item in self._commands if item.command_id == command.command_id)

    def edit(self, command_id: str, **changes) -> AgentCommand:
        index = self._index_of(command_id)
        self._commands[index] = replace(self._commands[index], **changes)
        self._sort_queue()
        return self._commands[index]

    def remove(self, command_id: str) -> AgentCommand:
        index = self._index_of(command_id)
        removed = self._commands.pop(index)
        if self._running_command_id == command_id:
            self._running_command_id = None
        return removed

    def reorder(self, command_id: str, new_index: int) -> None:
        index = self._index_of(command_id)
        command = self._commands.pop(index)
        bounded_index = max(0, min(new_index, len(self._commands)))
        self._commands.insert(bounded_index, command)

    def mark_running(self, command_id: str) -> AgentCommand:
        current = self.current()
        if current is not None and current.command_id != command_id:
            raise RuntimeError("Another command is already running.")
        index = self._index_of(command_id)
        updated = self._commands[index].with_status(CommandStatus.RUNNING)
        self._commands[index] = updated
        self._running_command_id = command_id
        return updated

    def start_next(self) -> Optional[AgentCommand]:
        if self.current() is not None:
            return self.current()
        for command in self._commands:
            if command.status in {CommandStatus.QUEUED, CommandStatus.PAUSED, CommandStatus.INTERRUPTED}:
                return self.mark_running(command.command_id)
        return None

    def pause(self, command_id: str) -> AgentCommand:
        return self._transition(command_id, CommandStatus.PAUSED)

    def resume(self, command_id: str) -> AgentCommand:
        return self._transition(command_id, CommandStatus.QUEUED)

    def cancel(self, command_id: str) -> AgentCommand:
        return self._transition(command_id, CommandStatus.CANCELLED)

    def complete(self, command_id: str) -> AgentCommand:
        return self._transition(command_id, CommandStatus.COMPLETED)

    def fail(self, command_id: str, reason: str) -> AgentCommand:
        return self._transition(command_id, CommandStatus.FAILED, last_error=reason)

    def interrupt(self, command_id: str, resume_token: Optional[str] = None) -> AgentCommand:
        index = self._index_of(command_id)
        command = self._commands[index].with_resume_token(resume_token).with_status(CommandStatus.INTERRUPTED)
        self._commands[index] = command
        if self._running_command_id == command_id:
            self._running_command_id = None
        return command

    def force_interrupt_current(self, resume_token: Optional[str] = None) -> Optional[AgentCommand]:
        current = self.current()
        if current is None:
            return None
        return self.interrupt(current.command_id, resume_token=resume_token)

    def snapshot(self) -> Dict[str, List[Dict[str, object]]]:
        return {
            "commands": [
                {
                    "command_id": command.command_id,
                    "action_type": command.action_type.value,
                    "status": command.status.value,
                    "priority": int(command.priority),
                    "last_error": command.last_error,
                }
                for command in self._commands
            ]
        }

    def replace_all(self, commands: List[AgentCommand]) -> None:
        self._commands = list(commands)
        self._running_command_id = None
        self._sort_queue()

    def _transition(self, command_id: str, status: CommandStatus, last_error: str = "") -> AgentCommand:
        index = self._index_of(command_id)
        updated = self._commands[index].with_status(status, last_error=last_error)
        self._commands[index] = updated
        if status in {CommandStatus.COMPLETED, CommandStatus.FAILED, CommandStatus.CANCELLED, CommandStatus.PAUSED, CommandStatus.INTERRUPTED}:
            if self._running_command_id == command_id:
                self._running_command_id = None
        return updated

    def _index_of(self, command_id: str) -> int:
        for index, command in enumerate(self._commands):
            if command.command_id == command_id:
                return index
        raise KeyError(f"Unknown command_id: {command_id}")

    def _sort_queue(self) -> None:
        if self.current() is not None:
            running_id = self._running_command_id
            running = [command for command in self._commands if command.command_id == running_id]
            others = [command for command in self._commands if command.command_id != running_id]
            others.sort(key=lambda item: (-int(item.priority), item.created_at_monotonic))
            self._commands = running + others
            return
        self._commands.sort(key=lambda item: (-int(item.priority), item.created_at_monotonic))
