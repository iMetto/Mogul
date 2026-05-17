# Tool Discipline

## Search / Read Strategy
- Prefer `rg` for text search and `rg --files` for file discovery.
- Read only focused line windows around search hits, especially in generated or decompiled code.
- Do not open huge generated files wholesale unless absolutely necessary.
- Before scanning `assembly/` or external refs, check `docs/references.md` and `docs/research/`.

## lean-ctx
- Use lean-ctx for large reads, repeated reads, broad searches, and generated/decompiled files.
- Use compressed/focused modes:
  - file map/signatures before full reads
  - exact line ranges around hits
  - cached rereads when possible
- Do not use lean-ctx for tiny known-file edits unless it saves context.

## code-review-graph
- Use code-review-graph only for structural questions:
  - what calls this?
  - what owns this behavior?
  - what depends on this class/function?
- Do not run graph builds or indexing as a routine step.
- If graph lookup fails on generated/decompiled assembly, fall back to `rg` plus focused reads.

## Editing / Safety
- Never use git commands unless explicitly asked.
- Do not revert unrelated user changes.
- Use patch-style edits, keep changes scoped, then run the narrowest relevant build/tests.
- Summarize changed files and verification results at the end.