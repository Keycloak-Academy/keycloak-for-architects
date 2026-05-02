# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Repository Is

A Keycloak training curriculum project. The primary artifact is `lab-template.md`, a canonical template for authoring hands-on Keycloak labs aimed at architects and advanced practitioners. New labs are written as Markdown files following that template.

## Lab Authoring Conventions

### Structure

Every lab follows this exact section order (from `lab-template.md`):

1. **Title block** — module cross-reference + one paragraph outcome statement (no step descriptions)
2. **Prerequisites** — checkbox list of observable environment state; link to prior lab if unmet
3. **Background** — concept tables, protocol diagrams, "why this matters"; no tasks live here
4. **Tasks** (1–4) — each with Goal, Observable outcome, 1–2 Hints, and a Solution
5. **Lab Checkpoint** — checkable observable states + optional CLI verification
6. **Going Further** — extension ideas with no hints/solutions

### Task writing rules

- **Goal** must start with an action verb and describe a configuration state or observable outcome, not steps.
- **Observable outcome** lists what the learner sees (UI / response body / logs), what changed, and optionally what a wrong answer looks like.
- **Hint 1** gives a conceptual nudge toward the right area of the product — no exact UI paths or field values.
- **Hint 2** (if needed) covers a different dimension (e.g., Hint 1 = "where", Hint 2 = "what value and why").
- **Solution** gives exact UI paths, field names, curl commands, and a verification step. Ends with a cleanup/restore note if the task mutates shared configuration.
- All hints and solutions use `<details><summary>` collapsible blocks.

### Voice and framing

- The intro paragraph describes the problem and what the learner will have *demonstrated* — never describes steps or says "you will learn how to do X by following these steps."
- Background is pure reference; learners return to it after tasks to consolidate understanding.
- Estimated time and tooling (admin console / curl / OIDC playground) appear in the task header blockquote.

### Lab metadata

- Labs are numbered within modules: `Lab [N] — Module [M]: [Title]`.
- Prerequisites reference specific observable state (e.g., Keycloak running at `http://localhost:8080`, realm accessible with named user) rather than generic "have Keycloak installed".
