# How to work with Claude on this project

A practical guide for you (the human). For agent-facing rules, see `.claude/rules/`
and `docs/workflow.md`.

## The flow you'll see

For a non-trivial change, I should:

1. **Graph first** — `semantic_search_nodes` / `query_graph` in your code.
2. **Subagent for big lookups** — Haiku via the `lookup-otc` or `lookup-assembly` skill.
3. **`ctx_read mode=signatures`** on a few project files.
4. **Explain the design** — wait for your approval.
5. **Code.**

If I jump straight to step 5, **stop me**. If I read 5+ files before designing,
**stop me**. Step 1 should always be the cheapest move.

## How to prompt me

### Good prompts

- *"Add a budtender that grows weed in the back room. Look at how OTC handles
  employees, but skip their multiplayer shortcuts."*
  → I have a goal, a reference point, and a constraint. Clear.

- *"Customers don't despawn after checkout closes. Find the bug, explain, fix."*
  → Specific symptom, explicit ordering (find → explain → fix).

- *"Show me how QueueSlots picks slot 0. Don't change anything."*
  → Read-only research request. Locks me out of editing.

### Bad prompts (and why)

- *"Make customers smarter."* → Too vague. I'll guess and over-build.
  Better: *"After checkout, customers should walk to the door instead of
  vanishing."*

- *"Refactor MogulNetwork."* → Open-ended. I'll over-reach.
  Better: *"Split MogulNetwork's persistence (SaveToDisk, LoadFromDisk, Commit)
  into a new MogulPersistence class. Keep transport in MogulNetwork."*

- *"Fix everything."* → I'll make 30 changes you didn't ask for.
  Better: pick one thing, do it, ship it.

### The single best habit

**Tell me the outcome you want, not the steps.** "Customers should despawn after
leaving the building" is better than "modify CustomerManager.OnCheckoutClosed
to schedule a despawn." I'll figure out the steps; you decide the outcome.

## How to correct me — phrases that work

Short, declarative, no question marks:

- **"Stop."** — halts me.
- **"Wrong path."** — I'll back up and try again.
- **"Use the graph."** / **"Use ctx_read."** — direct tool nudges.
- **"Smaller diff."** — I cut scope.
- **"Don't edit yet."** — research/explanation only.
- **"Show me the plan first."** — forces a written plan.
- **"Stay in scope."** — stops me wandering into adjacent files.
- **"No defensive code."** — kills unnecessary try/catch.
- **"Split it."** — when I bundle two ideas into one change.

What doesn't work:

- *"Are you sure?"* — I'll wobble but probably do the same thing.
- *"Maybe also..."* — I read this as scope expansion.
- Long explanations of why I should change behavior — just tell me what to do.

## Smells to watch for

| You see... | I'm doing... | Say... |
|---|---|---|
| Reading 5+ files before designing | Skipped the graph | "use the graph first" |
| `ctx_read mode=full` on every file | Lazy reads | "signatures only" |
| Writing code without explaining | Violating educational rule | "explain first" |
| Adding `// TODO` comments | Half-finishing work | "no half-measures" |
| Adding try/catch around safe code | Defensive over-engineering | "trust the call site" |
| "Let me also fix X" | Scope creep | "stay in scope" |
| Editing files outside the task | Drifting | "stay in scope" |
| Long preambles before tool calls | Narrating | "less talk, more action" |

## Session hygiene

Watch `/usage` (or `/context`):

- **0–40%**: green. Keep going.
- **40–60%**: bloating. Switching tasks? Start a new session.
- **60%+**: degradation. New session with a handoff.

**Don't auto-compact.** It silently drops fidelity. New sessions are honest.

**Handoff template** (save to `docs/handoff.md` before starting a new session):

```
Working on: <one-line task>
State: <what's done — files touched, design decisions>
Next: <the very next concrete step>
Open questions: <anything you need to decide>
```

## Subagents — when I should spawn one

I should spawn a Haiku subagent (via `lookup-otc` or `lookup-assembly` skill) when:

- The lookup spans 3+ files in `assembly/` or OTC.
- I only need the API surface, not full bodies.
- The result is a bounded brief (under 250 words).

If I'm reading those folders inline, I'm doing it wrong. Say:
**"use the lookup-otc skill"** or **"use the lookup-assembly skill."**

## What's enforced automatically

- **`assembly/` native Read is blocked** — a hook prevents it. You'll see a
  block message. That's working as designed.
- **Graph auto-updates** on every Edit/Write/Bash. Adding files in `Mogul/` is
  fine; the graph picks them up.

## What's not enforced

- Native Read on project files (it's allowed; I should *prefer* `ctx_read`
  but the hook doesn't force it).
- Whether I explain before writing — that's a habit I have to maintain.
  Call me on it.
- Whether I spawn a subagent — same story.

## Two things you can always say

1. **"Stop and explain."** — works in every situation.
2. **"Smaller diff."** — works whenever I'm about to over-reach.

That's most of what you need.
