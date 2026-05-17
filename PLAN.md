# RAG-A-Muffin — Feature Plan

## Completed

### Phase 1 — Core RAG
- [x] Gmail ingestion (inbox + sent, OAuth)
- [x] Text chunking (word-based, configurable size/overlap)
- [x] Embedding via Ollama (nomic-embed-text, 768-dim)
- [x] Qdrant vector store with scalar quantization (Int8)
- [x] Streaming RAG query (SSE, llama3)
- [x] Source-type filtering in queries
- [x] Citation deduplication + display (clickable cards)

### Phase 2 — Multi-Source Connectors
- [x] Google Drive connector (Docs, Sheets, PDF, DOCX)
- [x] Google Calendar connector
- [x] RSS / Atom feed connector
- [x] Web page scraper connector
- [x] File upload endpoint (PDF, DOCX, TXT, MD)
- [x] Watch folder (drop files, auto-ingest)
- [x] Email parser (strip replies, signatures, junk)
- [x] Per-document deduplication by content hash / source ID

### Phase 3 — Polish & Ops
- [x] Sources panel — manage RSS feeds and web URLs from the UI
- [x] Status panel — connector health, sync intervals
- [x] Logs panel — in-memory log viewer
- [x] Sync All button — manually trigger all connectors
- [x] Dev Tools panel — Restart and Rebuild from the browser
- [x] Citation sort (newest-first)
- [x] Person-aware search ("from X", "to Y" query extraction)
- [x] Toast notifications with correct z-index
- [x] Volume-mounted data persistence (`./data/` on host)
- [x] Docker project name locked (`name: rag-a-muffin`)
- [x] Helper-container rebuild (survives container self-exit)

---

## Proposed

### Phase 4 — Document Management & Visibility ✓

The index is currently a black box. You can put things in but can't see or manage what's there.

- [x] **Document browser** — panel listing all indexed documents with source type, title, date, chunk count. Searchable/filterable.
- [x] **Delete document** — remove a specific document and all its chunks from Qdrant by document ID.
- [x] **Delete by source type** — bulk-wipe all documents of a given type (e.g. clear all RSS chunks).
- [x] **Qdrant stats widget** — total vectors, breakdown by source type, shown in Index panel header.
- [ ] **Re-index document** — force a fresh scrape/sync for a specific web URL or file without clearing others.
- [x] **Index health indicator** — header badge showing total document count; turns amber if index is empty.

---

### Phase 5 — Conversation Memory

Each query is currently stateless. Multi-turn context would make the assistant far more useful.

- [ ] **Session conversation history** — carry the last N turns of Q&A into the LLM prompt so follow-up questions work ("what about the one from last week?" refers to the previous answer).
- [ ] **Persist chat sessions to disk** — save conversations to `./data/chats/` as JSON; load on page refresh.
- [ ] **Chat history browser** — sidebar or panel listing past sessions; click to restore.
- [ ] **Clear session** — button to start a fresh conversation without page reload.

---

### Phase 6 — Search Quality

- [ ] **Hybrid search** — combine Qdrant vector search with BM25 sparse vectors for better keyword recall. Especially useful for names, product IDs, and exact phrases.
- [ ] **Cross-encoder re-ranking** — after vector retrieval, re-rank the top-K chunks with a local cross-encoder model (e.g. `bge-reranker` via Ollama) before sending to the LLM.
- [ ] **Date-range filter** — UI chip or inline syntax (`since:2026-01`) to restrict results to a time window.
- [ ] **Query rewriting** — optionally rephrase the user's question before embedding (improves recall on vague queries).
- [ ] **Larger context window** — surface up to 16 chunks instead of 8; let the user configure topK per query.

---

### Phase 7 — Model Flexibility

Models are currently hardcoded. Users have different hardware and preferences.

- [ ] **Model selector UI** — fetch available models from `GET /api/tags` on Ollama; let user pick LLM and embedding model from dropdowns in Settings.
- [ ] **Persist model choice** — save selected models to `./data/settings.json`; apply on next query.
- [ ] **Chunk size tuning** — expose chunk size and overlap as UI sliders in Settings.
- [ ] **Configurable sync interval** — set sync frequency from the UI instead of editing `appsettings.json`.
- [ ] **Per-connector toggles** — enable/disable individual connectors (Gmail, Drive, Calendar, RSS, Web) from the Sources panel without removing entries.

---

### Phase 8 — Additional Connectors

- [ ] **Gmail attachment indexing** — when an email has attachments, download and index them (PDF, DOCX). Currently flagged but skipped.
- [ ] **Gmail label filter** — only sync emails with specific labels (e.g. `STARRED`, `INBOX`, custom labels) rather than all mail.
- [ ] **Obsidian vault** — watch a local Obsidian vault directory; index markdown notes with frontmatter metadata.
- [ ] **Notion** — sync pages from a Notion workspace via the Notion API.
- [ ] **Local directory connector** — index an arbitrary host path (bind-mounted into the container), useful for code docs, wikis, etc.

---

### Phase 9 — UX & Mobile

- [ ] **Mobile layout** — current max-width 860px layout doesn't adapt well to small screens; add responsive breakpoints.
- [ ] **Light mode** — toggle between dark and light themes; persist preference in localStorage.
- [ ] **Keyboard shortcuts** — `Cmd/Ctrl+K` to focus input, `/` to open source filter, `Esc` to close all panels.
- [ ] **Answer copy button** — one-click copy of the full LLM response.
- [ ] **Source preview improvement** — highlight the specific chunk text that matched the query (not just the full document preview).
- [ ] **Inline citations** — mark citation numbers `[1]`, `[2]` inline in the streamed answer instead of (or in addition to) cards below.

---

## Notes

- Phases are suggestions, not fixed sprints. Pick any feature from any phase.
- Qdrant's scroll API can back the document browser (Phase 4) without schema changes.
- Hybrid search (Phase 6) requires adding Qdrant sparse vector support — non-trivial but high-value.
- Conversation history (Phase 5) is the single biggest UX win relative to effort.
