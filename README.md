# TurnAFile

> All-in-one media and document converter for Windows.
> Convert video, audio, images, documents, data, and more — no hassle.

---

## ✨ Features

### 🎬 Video Conversion
| Input | Output Formats |
|-------|---------------|
| MP4, AVI, MKV, WEBM, MOV, WMV, FLV | MP4, AVI, MKV, WEBM, MOV, **MP3**, **M4A** |

- Convert between video formats
- Extract audio from video (MP3, M4A)
- Adjustable quality: High / Medium / Low
- Batch conversion

### 🎵 Audio Conversion
| Input | Output Formats |
|-------|---------------|
| MP3, WAV, M4A, FLAC, OGG, AAC, WMA | MP3, WAV, M4A, FLAC, OGG |

- Convert between audio formats
- Adjustable quality: High / Medium / Low

### 🖼️ Image Conversion
| Input | Output Formats |
|-------|---------------|
| JPG, JPEG, PNG, WEBP, GIF, BMP, TIFF | JPG, PNG, WEBP, GIF, BMP, TIFF, **PDF**, **TXT (OCR)** |

- Resize with adjustable quality (Original / 75% / 50%)
- Image to PDF conversion
- **OCR**: Optical Character Recognition to TXT

### 📄 Document Conversion

| Input | Output Formats |
|-------|---------------|
| DOCX | DOCX, PDF, TXT, MD, HTML, EPUB |
| TXT | TXT, DOCX, HTML, **PDF** |
| HTML / HTM | HTML, DOCX, TXT, **PDF** |
| MD / Markdown | MD, DOCX, HTML, TXT |
| RTF | TXT, DOCX |
| ODT | TXT |
| PDF | TXT, DOCX, HTML, EPUB, MD |
| EPUB | TXT, HTML, EPUB, DOCX, PDF, MD |

- Preserves formatting in DOCX → PDF (headings, bold, italic, color, font, tables, images)
- HTML → PDF with syntax highlighting (VS Code style)
- EPUB → PDF with book-style rendered content

### 📊 Data Conversion

| Input | Output Formats |
|-------|---------------|
| XLSX | XLSX, CSV, JSON, **PDF** |
| CSV | CSV, XLSX, JSON, **PDF** |
| JSON | JSON, CSV, XLSX, **PDF** |

- PDF tables optimized with font scaling and column wrapping
- Support for large files with wide columns

---

## 🔧 How It Works

### Interface
- Single window with file selector (drag and drop)
- Side panel for format and quality selection
- Progress in separate window with cancellation
- Windows Explorer context menu — right-click any supported file to convert directly
- Light and dark themes
- Multi-language: English, Español, Français, Deutsch, 中文, 日本語, Português

### Batch Conversion
- Process multiple files simultaneously
- Each file with its own format and quality settings
- Progress window with cancellation
- Conversion log with results

### OCR
- Extract text from images using Tesseract OCR
- Export results to TXT
- Auto-detects system language

---

## 🚀 Installation

1. Download the latest version from [Releases](https://github.com/nocloudware/TurnAFile/releases)
2. Extract the ZIP file to a folder
3. Run `TurnAFile.exe`

**Requirements:** Windows 10/11 (64-bit), .NET 8.0 Runtime

---

## 🛠️ Technologies

| Component | Purpose | License |
|-----------|---------|---------|
| **.NET 8.0 WPF** | Application framework | MIT |
| **FFmpeg** | Video/audio conversion | LGPL/GPL |
| **QuestPDF** | PDF generation | MIT Community |
| **Open XML SDK** | DOCX read/write | MIT |
| **ClosedXML** | XLSX read/write | MIT |
| **Tesseract OCR** | Optical Character Recognition | Apache 2.0 |
| **PdfPig** | PDF text extraction | Apache 2.0 |
| **WPF-UI** | Modern UI controls library | MIT |

---

## 📝 License

TurnAFile is free software under the MIT License.
See [THIRD_PARTY_NOTICES.txt](THIRD_PARTY_NOTICES.txt) for third-party component licenses.
