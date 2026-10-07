# Clara Document Comparison System

**See exactly what changed between two PDFs — word by word, side by side, in seconds.**

Clara Document Comparison System is a lightweight, self-hosted document comparison tool built for anyone who needs to answer one question quickly: *what actually changed between these two versions?* Upload two PDFs and Clara renders them side by side with every addition, deletion, and rewrite highlighted in place — no manual proofreading, no guessing, no scrolling back and forth between two separate files.

## Why Clara

Comparing documents by eye is slow and unreliable — small wording changes get missed, and large ones are hard to summarize. Clara solves this with the same algorithm class that powers Git's diffing (Myers' minimal edit-distance algorithm), applied to document text instead of source code. The result is a diff that stays tight and readable: a single moved word is highlighted as a single moved word, not an entire repainted paragraph.

Alongside the visual diff, Clara calculates a **quantitative similarity score** — a word-frequency-based difference percentage — giving you both a fast at-a-glance number and a detailed, navigable breakdown of every change.

## Features

- **Side-by-side visual diff** — both PDFs rendered in the browser with synchronized scrolling and zoom
- **Color-coded highlighting** — additions, removals, and rewrites are each marked distinctly, directly on the page
- **Minimal-edit diffing** — powered by a linear-space implementation of Myers' algorithm, so highlights stay precise instead of over-flagging whole paragraphs
- **Change navigation** — jump between changes with keyboard shortcuts or a filterable change list
- **Quantitative difference score** — a word-frequency similarity percentage calculated independently of the visual diff
- **Self-hosted** — runs entirely on your own infrastructure; documents never leave your server
- **No database, no setup overhead** — drop in two PDFs and get results immediately

## How it works

1. Both PDFs are parsed server-side to extract every word along with its exact position on the page
2. The two word sequences are compared using a minimal edit-script algorithm, producing the smallest possible set of changes
3. Changes are grouped into highlight regions and sent to the browser as lightweight coordinates
4. The browser renders both PDFs natively and overlays the highlights — so page images never have to be transmitted, and highlights stay sharp at any zoom level

## Tech stack

- **Backend:** ASP.NET Core (.NET 9)
- **PDF parsing:** PdfPig (word-level extraction with positional data), iText (text extraction for the similarity score)
- **Frontend:** PDF.js for in-browser rendering, vanilla JavaScript for the diff viewer
- **No external services, no cloud dependencies** — everything runs locally

## Use cases

- Comparing revised lesson materials, reports, or course documents between versions or academic years
- Reviewing contract or policy redlines without relying on the original author to track changes
- Catching unintended or unauthorized edits between document revisions
- Any workflow where "what changed?" needs a fast, visual, trustworthy answer
