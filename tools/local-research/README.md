# Local Research With Ollama

This tool uses local Ollama as a first-pass research assistant over narrow code
snippets. It should not write directly into trusted research docs.

Workflow:

1. Pick a topic from `tools/local-research/topics/`.
2. Run `tools/local-research/run-research.sh <topic>`.
3. Review `docs/research/inbox/<topic>.md`.
4. Verify important claims against source snippets.
5. Promote confirmed facts into the matching `docs/research/*.md` file.

Examples:

```bash
tools/local-research/run-research.sh cash-registers
tools/local-research/run-research.sh grow-tents
tools/local-research/run-research.sh npc-routing
```

If Ollama runs on Windows and Codex runs in WSL, the script defaults to the WSL
gateway address:

```bash
export OLLAMA_URL="http://$(ip route | awk '/default/ {print $3}'):11434"
curl "$OLLAMA_URL/api/tags"
```

Useful knobs:

```bash
CONTEXT_LINES=12 MAX_LINES=1200 tools/local-research/run-research.sh grow-tents
NUM_CTX=65536 tools/local-research/run-research.sh cash-registers
```

Keep prompts factual. Good output cites paths, class names, method names, fields,
and string literals. Anything not cited should be treated as speculation.
