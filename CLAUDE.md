# Spring Voyage V2 — Claude Code Configuration

Read `AGENTS.md` for project rules, architecture, build commands, key folders, and agent roles. Everything in `AGENTS.md` applies here.

Read `CONVENTIONS.md` for coding patterns, naming, testing, and Dapr conventions. Everything in `CONVENTIONS.md` is mandatory.

## Claude Code-Specific

- Specialized agent definitions in `v2/.claude/agents/`
- Custom commands in `v2/.claude/commands/` (`/build`, `/test`, `/lint`)
- Follow the git workflow from the root `CLAUDE.md` — always use worktrees, always create PRs against `main`, never push directly to `main`.

## Reading Guide

Before working on a v2 issue, read:
1. `v2/CONVENTIONS.md` — coding patterns (mandatory)
2. `v2/docs/SpringVoyage-v2-plan.md` — architecture (the relevant section for your issue)
