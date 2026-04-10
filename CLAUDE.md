# Spring Voyage V2 — Claude Code Configuration

Read `AGENTS.md` for project rules, architecture, build commands, key folders, and agent roles. Everything in `AGENTS.md` applies here.

Read `CONVENTIONS.md` for coding patterns, naming, testing, and Dapr conventions. Everything in `CONVENTIONS.md` is mandatory.

## Claude Code-Specific

- Specialized agent definitions in `.claude/agents/`
- Custom commands in `.claude/commands/` (`/build`, `/test`, `/lint`)
- Follow the git workflow from `AGENTS.md` — always use worktrees, always create PRs against `main`, never push directly to `main`.

## Reading Guide

Before working on an issue, read:
1. `CONVENTIONS.md` — coding patterns (mandatory)
2. `docs/SpringVoyage-v2-plan.md` — architecture (the relevant section for your issue)
