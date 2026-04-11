# Spring Voyage V2 — Claude Code Configuration

Read `AGENTS.md` for project rules, architecture, and **extensibility requirements**. Read `CONVENTIONS.md` for coding patterns. Both are mandatory.

This is the **open-source core platform**. A private repository extends it via git submodule and DI. Every change must be designed for clean extension — read `AGENTS.md` § "Open-Source Platform & Extensibility" before starting work.

Before working on an issue, read the relevant architecture documents under `docs/architecture/` (see `docs/architecture/README.md` for the index). For execution status, see `docs/roadmap.md`.

## Claude Code-Specific

- Specialized agent definitions in `.claude/agents/`
- Custom skills in `.claude/commands/` (`/build`, `/test`, `/lint`)
- Always use worktrees for implementation work
