# AI Log — Pre-phase: prompt engineering & context preparation

| | |
|---|---|
| **Date** | 2026-09-27 |
| **Tool** | Claude (Opus, Claude app — agentic session with file tools) |
| **Purpose** | Turn the two assessment documents into a reusable context + prompt pack for Claude Code before any code is written |
| **Inputs** | `DICEUS_Fullstack_Technical_Assessment.docx` (brief), `Claims_Module_Candidate_Specification.docx` (FRS) |
| **Outputs** | `CLAUDE.md`, `docs/PROMPTS.md`, `docs/spec/assessment-brief.md`, `docs/spec/claims-frs.md` |

## Why this phase exists
Brief §5 evaluates *how* AI is used: context loading, structured prompting, iterative refinement and architectural
orchestration. Rather than pasting the docs into a single prompt, I used a separate Claude session to:
1. Convert both `.docx` specs to Markdown so Claude Code can load them as repo context (`docs/spec/`).
2. Distil the non-negotiable rules into a persistent `CLAUDE.md`, which Claude Code loads in every session. This is the
   **context-loading strategy**.
3. Write a **phased prompt sequence** (`docs/PROMPTS.md`, Phase 0–8), matching brief §5.2, with a human review
   checkpoint at the end of every phase.
4. Make the first Claude Code phase **analysis-only**, so conflicts between the brief and the FRS are surfaced
   as explicit decisions (`DECISIONS.md`) instead of being resolved silently by the model.

## Key outputs of the analysis (from reading both docs)
- Found ~18 conflicts/gaps between the brief and the FRS. Examples: SLA "status" vs "do not change status";
  BR-C-06 defined differently in each doc; reserve component names (Appendix A.2 vs FRS §6.2); PUT vs POST reserve
  adjust API; "no gaps" claim numbers vs "a SEQUENCE is acceptable"; an implied but undefined ClaimValidationIssues entity;
  intake Critical issues (Draft) vs 422 responses; ~8 endpoints implied by the UI/rules but not listed.
  These became items D-01…D-18 in the Phase 0 prompt.
- Flagged licensing risk: MediatR ≥13 and AutoMapper ≥15 are commercial, so they are pinned to 12.x / 14.x (to verify on NuGet).
- Pre-identified likely AI failure modes to watch for in later phases: SEQUENCE-based claim numbers (gaps),
  check-then-act GL idempotency (race), validation in controllers, `DateTime.Now` in rules, ≤ vs < thresholds,
  an SLA job mutating status.

## What I reviewed / changed
- TODO (Vlad): note any edits you made to `CLAUDE.md` / `PROMPTS.md` / the D-xx recommendations before running
  Phase 0, and why.

---

## Transcript

*Exported from the session. Tool inputs/outputs are collapsed and truncated; file contents live in the repo.*

### 👤 User prompt 1

`DICEUS_Fullstack_Technical_Assessment.docx` `Claims_Module_Candidate_Specification.docx` I need you to generate a prompt for Claude Code in order to successfully finish a technical assessment. Read the DICEUS_Fullstack_Technical_Assessment.docx, pay attention to the PAS requirements and specification mentioned in the document in order to build a proper context for Claude with understanding what should be implemented especially considering section 5 "AI Workflow Expectations". A deeper specification for PAS described in the Claims_Module_Candidate_Specification.docx.

**🤖 Claude:**

I'll read both documents and then write the Claude Code prompt.

<details><summary>🔧 Tool call: Bash</summary>

Extract text from both assessment documents

```bash
cd /root/.claude/uploads/ca73cdbd-0d8f-5439-ba6c-ae56bb171d18/ && for f in *.docx; do echo "=== $f"; pandoc "$f" -t markdown 2>/dev/null || python3 -c "import docx,sys;d=docx.Document(sys.argv[1]);print('\n'.join(p.text for p in d.paragraphs));[print(' | '.join(c.text for c in r.cells)) for t in d.tables for r in t.rows]" "$f"; done
```

</details>

<details><summary>📄 Tool result</summary>

```
<persisted-output>
Output too large (117.8KB). Full output saved to: /root/.claude/projects/-home-claude/ca73cdbd-0d8f-5439-ba6c-ae56bb171d18/tool-results/bm1cp6iri.txt

Preview (first 2KB):
=== d97216f5-Claims_Module_Candidate_Specification.docx
TECHNICAL ASSESSMENT SPECIFICATION

**Claims Management System**

**FNOL Intake & Reserve Management**

Functional Specification · Candidate Reference Document

  ------------------ ----------------------------------------------------
  **Document         1.0
  Version**          

  **Date**           May 2026

  **Purpose**        Technical Assessment ---Fullstack Engineer

  **Domain**         Insurance Claims Management

  **Status**         For
… [truncated, 1532 more chars]
```

</details>

<details><summary>🔧 Tool call: Read</summary>

```json
{
 "file_path": "/root/.claude/projects/-home-claude/ca73cdbd-0d8f-5439-ba6c-ae56bb171d18/tool-results/bm1cp6iri.txt"
}
```

</details>

<details><summary>📄 Tool result</summary>

```
1	=== d97216f5-Claims_Module_Candidate_Specification.docx
2	TECHNICAL ASSESSMENT SPECIFICATION
3	
4	**Claims Management System**
5	
6	**FNOL Intake & Reserve Management**
7	
8	Functional Specification · Candidate Reference Document
9	
10	  ------------------ ----------------------------------------------------
11	  **Document         1.0
12	  Version**          
13	
14	  **Date**           May 2026
15	
16	  **Purpose**        Technical Assessment ---Fullstack Engineer
17	
18	  **Domain**         Insurance Claims Management
19	
20	  **Status**         For Implementation
21	  ------------------ ----------------------------------------------------
22	
23	*This document is self-contained. No add
… [truncated, 75724 more chars]
```

</details>

<details><summary>🔧 Tool call: Read</summary>

```json
{
 "file_path": "/root/.claude/projects/-home-claude/ca73cdbd-0d8f-5439-ba6c-ae56bb171d18/tool-results/bm1cp6iri.txt",
 "offset": 1645,
 "limit": 1300
}
```

</details>

<details><summary>📄 Tool result</summary>

```
1645	
1646	The ClaimAuditLog table is an immutable append-only event log. It is the
1647	single source of truth for all business actions taken on a claim.
1648	
1649	## 14.1 Events That Must Be Logged
1650	
1651	  ------------------------ -----------------------------------------------
1652	  **EventType**            **Trigger**
1653	
1654	  CLAIM_CREATED            New claim successfully created via POST
1655	                           /api/claims
1656	
1657	  STATUS_CHANGED           Claim status transition applied; OldValue =
1658	                           previous status, NewValue = new status
1659	
1660	  PARTY_ADDED              New ClaimParty added to the claim
1661	
1662	  PARTY_REM
… [truncated, 57040 more chars]
```

</details>

<details><summary>🔧 Tool call: Bash</summary>

Convert both specs to Markdown for Claude Code context

```bash
mkdir -p /home/claude/diceus-kit/docs/spec && cd /root/.claude/uploads/ca73cdbd-0d8f-5439-ba6c-ae56bb171d18/ && pandoc d97216f5-Claims_Module_Candidate_Specification.docx -t gfm -o /home/claude/diceus-kit/docs/spec/claims-frs.md && pandoc fa1717cc-DICEUS_Fullstack_Technical_Assessment.docx -t gfm -o /home/claude/diceus-kit/docs/spec/assessment-brief.md && wc -w /home/claude/diceus-kit/docs/spec/*.md && grep -c '<table' /home/claude/diceus-kit/docs/spec/*.md
```

</details>

<details><summary>📄 Tool result</summary>

```
  5344 /home/claude/diceus-kit/docs/spec/assessment-brief.md
  8124 /home/claude/diceus-kit/docs/spec/claims-frs.md
 13468 total
/home/claude/diceus-kit/docs/spec/assessment-brief.md:0
/home/claude/diceus-kit/docs/spec/claims-frs.md:0
```

</details>

<details><summary>🔧 Tool call: Bash</summary>

Check Markdown table rendering quality

```bash
cd /home/claude/diceus-kit/docs/spec && sed -n '/Authority Thresholds/,/RULE/p' claims-frs.md | head -20
```

</details>

<details><summary>📄 Tool result</summary>

```
## 6.3 Authority Thresholds

Reserve amounts are validated against authority thresholds at the time
of creation or adjustment. The threshold applies to the amount of the
individual transaction (not the running total).

|                             |                              |                                                                                           |
|-----------------------------|------------------------------|-------------------------------------------------------------------------------------------|
| **Transaction Amount**      | **Authority Level Required** | **System Behaviour**                                                                      |
| ≤ \$10,000    
… [truncated, 1076 more chars]
```

</details>

**🤖 Claude:**

Now the persistent project context file (CLAUDE.md), which Claude Code loads automatically in every session:

<details><summary>🔧 Tool call: Write</summary>

Wrote `CLAUDE.md` (11229 chars — full content is in the repo)

</details>

<details><summary>📄 Tool result</summary>

```
File created successfully at: /home/claude/diceus-kit/CLAUDE.md (file state is current in your context — no need to Read it back)
```

</details>

**🤖 Claude:**

Now the main prompt: the kickoff prompt plus the follow-up prompts for each phase.

<details><summary>🔧 Tool call: Write</summary>

Wrote `docs/PROMPTS.md` (21380 chars — full content is in the repo)

</details>

<details><summary>📄 Tool result</summary>

```
File created successfully at: /home/claude/diceus-kit/docs/PROMPTS.md (file state is current in your context — no need to Read it back)
```

</details>

<details><summary>🔧 Tool call: Bash</summary>

Package the prompt kit into a zip

```bash
cd /home/claude && rm -f diceus-claude-code-kit.zip && zip -rq diceus-claude-code-kit.zip diceus-kit && unzip -l diceus-claude-code-kit.zip
```

</details>

<details><summary>📄 Tool result</summary>

```
Archive:  diceus-claude-code-kit.zip
  Length      Date    Time    Name
---------  ---------- -----   ----
        0  2026-09-27 12:11   diceus-kit/
    11285  2026-09-27 12:11   diceus-kit/CLAUDE.md
        0  2026-09-27 12:13   diceus-kit/docs/
        0  2026-09-27 12:10   diceus-kit/docs/spec/
    44788  2026-09-27 12:10   diceus-kit/docs/spec/assessment-brief.md
    89157  2026-09-27 12:10   diceus-kit/docs/spec/claims-frs.md
    21532  2026-09-27 12:13   diceus-kit/docs/PROMPTS.md
---------                     -------
   166762                     7 files
```

</details>

<details><summary>🔧 Tool call: SendUserFile</summary>

```json
{
 "files": [
  "/home/claude/diceus-claude-code-kit.zip",
  "/home/claude/diceus-kit/docs/PROMPTS.md",
  "/home/claude/diceus-kit/CLAUDE.md"
 ],
 "caption": "Unzip into an empty repo root, start Claude Code in plan mode, and paste the Kickoff prompt from PROMPTS.md.",
 "status": "normal"
}
```

</details>

<details><summary>📄 Tool result</summary>

```
3 files delivered to user.
  /home/claude/diceus-claude-code-kit.zip → file_uuid: 41997217-8f7d-4a85-aa3a-48a72e42ff31
  /home/claude/diceus-kit/docs/PROMPTS.md → file_uuid: ede4c225-b6ee-46fa-b37b-3b762d37f395
  /home/claude/diceus-kit/CLAUDE.md → file_uuid: 4a519f8f-07a0-486f-9501-58023743b207
```

</details>

**🤖 Claude:**

I wrote the prompt as a small kit rather than one long paste. That way the workflow itself shows the "context loading, prompt sequencing, iterative refinement" approach that section 5 of the brief says it evaluates.

- **`CLAUDE.md`** goes in the repo root, and Claude Code loads it automatically in every session. It holds the fixed stack, the Clean Architecture layout and dependency rule, the engineering rules (validation in the MediatR pipeline, append-only audit log, event-sourced reserves, EF conventions), a short summary of the domain rules, and how Claude should work with you. That includes stopping after each phase, logging your corrections to `docs/ai-log/`, and building up answers for the live review in `REVIEW-PREP.md`.
- **`docs/PROMPTS.md`** has the kickoff prompt plus one prompt per phase. The phases follow section 5.2 of the brief (analysis, skeleton, domain and database, backend, reserves and Hangfire, documents, Angular, Azure and CI/CD, final review), and each one ends at a checkpoint where you review before moving on.
- **`docs/spec/*.md`**: both Word documents converted to Markdown, since Claude Code can't read `.docx` directly.

**The kickoff only analyses; it writes no code.** It has Claude produce a requirements matrix and a draft architecture plan. It also produces a list of decisions starting from 18 contradictions and gaps I found between the brief and the detailed specification. Some examples:
- The brief says the SLA job flags a status; the specification says it must not change the status.
- BR-C-06 means two different things in the two documents.
- The reserve component names don't match.
- A claim number that is "gap-free" conflicts with allowing a database sequence, because sequences can skip numbers.
- A table for validation issues is implied but never defined.
- About eight endpoints are missing that the UI and rules depend on, such as assigning a handler, linking a policy and setting the manager override.

Reviewers are likely to ask about exactly these, so having written decisions for them is worth a lot.

**Check before adding packages:** the prompt pins MediatR to 12.x and AutoMapper to 14.x. My understanding is that newer versions require a commercial licence, but confirm that on NuGet before you add them.

**Your AI-workflow report:** after each phase, run `/export` in Claude Code and write down every place where you corrected Claude. The "what AI got wrong" section of `AI-WORKFLOW.md` should come from real corrections, and the kit tells Claude not to make any up.
