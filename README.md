# Clara — Document Comparison System

**See exactly what changed between two versions of a PDF — not just that they are different.**

Clara is a self-hosted document comparison system that compares two PDF documents and visually highlights additions, deletions, and rewritten content directly on the original pages.

It is designed for situations where knowing that *something changed* is not enough. When documents such as reports, educational materials, policies, or other revision-heavy content are updated, Clara makes it possible to quickly see **what changed, where it changed, and how much the documents differ**.

Unlike a simple text diff, Clara preserves the spatial context of the original document. Changes are mapped back to their positions on the PDF page and displayed as visual overlays in the browser.

![Clara document comparison](docs/clara01.PNG)

![Clara document comparison](docs/clara02.PNG)

---

## Why Clara?

Comparing two PDF files manually can be surprisingly difficult.

A traditional text comparison can tell you that a sentence changed, but it may lose important information about where that change appeared in the original document. Comparing the PDFs visually, on the other hand, can require manually switching between versions and searching for differences.

Clara combines both approaches:

**PDF → structured text + positions → algorithmic comparison → visual changes**

This makes it possible to go from:

> "These two documents are different."

to:

> "These words were added on page 4, this sentence was rewritten on page 7, and these sections were removed."

The result is intended to make document revision and review faster and easier.

---

## Key Features

* Upload and compare two PDF documents
* View the documents directly in the browser
* Detect additions, deletions, and rewritten text
* Compare documents at the word level
* Preserve page and coordinate information during PDF extraction
* Highlight changes at their original positions on the page
* Calculate a quantitative word-frequency similarity score
* Use the Myers minimal edit-distance algorithm for document comparison
* Linear-space implementation of the comparison algorithm
* Self-hosted — no external services or cloud APIs required
* No database required
* Process documents locally

---

## How It Works

Clara's comparison process consists of several stages.

```text
             ┌──────────────────────┐
             │    Two PDF files     │
             └──────────┬───────────┘
                        │
                        ▼
             ┌──────────────────────┐
             │   PDF text extraction│
             │        PdfPig        │
             └──────────┬───────────┘
                        │
                        ▼
             ┌──────────────────────┐
             │ Words + page +       │
             │ positional data      │
             └──────────┬───────────┘
                        │
                        ▼
             ┌──────────────────────┐
             │   Myers diff         │
             │   algorithm          │
             └──────────┬───────────┘
                        │
                        ▼
             ┌──────────────────────┐
             │ Changed words mapped │
             │ to PDF coordinates   │
             └──────────┬───────────┘
                        │
                        ▼
             ┌──────────────────────┐
             │ Browser visualization│
             │       PDF.js         │
             └──────────────────────┘
```

### 1. PDF extraction

Each PDF is processed to extract its textual content at the word level.

For each extracted word, Clara retains positional information such as its page and location within the page.

This information is important because the final goal is not only to determine **which words changed**, but also **where those words appeared in the original document**.

### 2. Document comparison

The extracted word sequences are compared using the **Myers diff algorithm**.

The algorithm identifies the minimal sequence of insertions and deletions needed to transform one sequence into another.

Clara uses a linear-space implementation to reduce the memory required by the comparison process.

### 3. Mapping differences back to the document

The textual differences are associated with the positional information collected during PDF extraction.

This allows Clara to determine where a changed word belongs on the original page.

### 4. Browser visualization

The frontend renders the PDF using **PDF.js**.

The detected differences are then displayed as coordinate-based overlays on top of the document, allowing the user to see changes in their original visual context.

---

## The Comparison Algorithm

Clara uses the **Myers diff algorithm** to compare documents at the word level.

For example, given:

```text
Version A:
The system processes student submissions.

Version B:
The system automatically processes student submissions.
```

the comparison can identify the added word:

```text
The system [automatically] processes student submissions.
```

The important part is that Clara does not stop at identifying the changed text.

Because each word retains its original PDF coordinates, the detected change can subsequently be mapped back to the corresponding location on the document page.

### Why Myers?

Myers' algorithm is designed to find a minimal edit sequence between two sequences. This makes it suitable for identifying meaningful textual changes while avoiding unnecessary differences caused by simple sequence alignment.

Clara uses a **linear-space implementation** to avoid retaining the complete edit graph in memory.

---

## From Text Differences to Visual Changes

One of the main engineering challenges in Clara is connecting a textual diff with the original visual document.

A text-only comparison might produce:

```text
Old:
The system processes student submissions.

New:
The system automatically processes student submissions.
```

But the application ultimately needs to answer:

> Where is "automatically" located on the PDF page?

To support this, Clara preserves positional information during PDF extraction.

The process is therefore:

```text
PDF
 ↓
Words + coordinates
 ↓
Word sequence
 ↓
Myers diff
 ↓
Changed words
 ↓
Original coordinates
 ↓
Visual overlay
```

This allows the application to display changes directly where they occur in the document.

---

## Similarity Score

In addition to the visual diff, Clara calculates a quantitative similarity score based on word-frequency comparison.

The score provides a high-level indication of how similar two documents are.

The two mechanisms serve different purposes:

* **Similarity score:** provides a quick overall indication of how similar the documents are.
* **Visual diff:** shows exactly where their content differs.

This makes it possible to quickly identify whether two documents are broadly similar and then inspect the specific changes when necessary.

---

## Architecture

Clara uses a lightweight client-server architecture.

```text
┌─────────────────────────────────────────────┐
│                  Browser                    │
│                                             │
│  JavaScript + PDF.js                       │
│  PDF rendering                             │
│  Diff visualization                        │
│  Coordinate overlays                       │
└──────────────────────┬──────────────────────┘
                       │ HTTP
                       ▼
┌─────────────────────────────────────────────┐
│             ASP.NET Core Backend            │
│                    .NET 9                   │
│                                             │
│  Document upload                            │
│  PDF processing                             │
│  Document comparison                        │
│  Similarity calculation                     │
└───────────────┬─────────────────┬───────────┘
                │                 │
                ▼                 ▼
       ┌────────────────┐  ┌────────────────┐
       │     PdfPig     │  │     iText      │
       │                │  │                │
       │ Word extraction│  │ Similarity /   │
       │ + coordinates  │  │ text analysis  │
       └────────────────┘  └────────────────┘
```

The application does not require a database or external cloud services.

Document processing is performed by the application itself, making Clara suitable for self-hosted environments where documents should remain within the organization's infrastructure.

---

## Technology

### Backend

* **C#**
* **ASP.NET Core**
* **.NET 9**

### PDF Processing

* **PdfPig** — word-level PDF extraction and positional information
* **iText** — text/document analysis used for the similarity calculation

### Document Comparison

* **Myers minimal edit-distance algorithm**
* Linear-space implementation
* Word-level comparison

### Frontend

* **JavaScript**
* **PDF.js**
* Coordinate-based visual overlays

### Deployment

* Self-hosted
* No external cloud services
* No external APIs required
* No database required

---

## Engineering Decisions

### Word-level comparison instead of raw PDF comparison

PDF files contain much more than visible text, including formatting information, metadata, and internal document structures.

Two PDFs can therefore differ at the file level without having a meaningful difference in their visible content.

Clara compares extracted document content instead of comparing raw PDF files byte-by-byte.

### Preserving positional information

A text-only diff is not enough for a document comparison tool.

Words are therefore retained together with their page and coordinate information so that detected changes can later be rendered at their original location.

### Self-hosted processing

Clara does not depend on external services or cloud APIs for document processing.

This keeps the architecture lightweight and allows the application to be deployed within an organization's own environment.

### Linear-space diff

The Myers algorithm can be implemented in ways that retain substantial intermediate state.

Clara uses a linear-space implementation to reduce memory requirements during document comparison.

---

## Example Use Cases

Clara can be useful wherever documents go through repeated revisions and the differences need to be reviewed efficiently.

### Educational materials

Compare different versions of:

* course materials
* lesson documents
* assignments
* reports
* instructional PDFs

For example, an instructor can compare the current version of a lesson with its previous version and immediately see which explanations or sections were changed.

### Policies and procedures

Organizations can compare revised versions of internal policies or procedures and quickly identify changes.

### Reports and documentation

Compare revised reports or technical documents while preserving the visual context of the original pages.

### Revision and quality assurance

Clara can be used as a review tool when documents are edited by multiple people and changes need to be inspected before publication.

---

## Running Locally

### Requirements

* .NET 9 SDK
* A modern web browser

### Clone the repository

```bash
git clone <repository-url>
cd Clara
```

### Run the application

```bash
dotnet run
```

Then open the URL displayed by the ASP.NET Core application in your browser.

---

## Limitations

PDF documents are complex and can contain layouts and structures that make reliable text extraction difficult.

As a result, comparison quality can depend on the structure and characteristics of the input PDFs.

Clara is primarily designed around text-based PDF documents where the textual content can be extracted reliably.

Scanned documents containing only images may require OCR before meaningful word-level comparison is possible.

---

## Future Improvements

Possible future improvements include:

* improved handling of complex PDF layouts
* OCR support for scanned documents
* additional diff visualization options
* more detailed comparison statistics
* improved handling of tables and multi-column layouts
* automated tests for additional PDF structures and edge cases
* packaging and deployment improvements

---

## Project Goal

The goal of Clara is simple:

> **Make document changes easy to see.**

Instead of asking users to manually compare two versions of a document, Clara combines document parsing, sequence comparison, positional information, and browser-based visualization to show where the content changed.
