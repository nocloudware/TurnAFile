
# TurnAFile

> Conversor multimedia y de documentos todo-en-uno para Windows.  
> Convierte video, audio, imágenes, documentos, datos y más — sin complicaciones.

---

## ✨ Funcionalidades

### 🎬 Conversión de video
| Entrada | Formatos de salida |
|---------|-------------------|
| MP4, AVI, MKV, WEBM, MOV | MP4, AVI, MKV, WEBM, MOV, **MP3**, **M4A** |

- Conversión entre formatos de video
- Extracción de audio desde video (MP3, M4A)
- Calidad ajustable: Alta / Media / Baja
- Conversión por lotes

### 🎵 Conversión de audio
| Entrada | Formatos de salida |
|---------|-------------------|
| MP3, WAV, M4A, FLAC, OGG | MP3, WAV, M4A, FLAC, OGG |

- Conversión entre formatos de audio
- Calidad ajustable: Alta / Media / Baja

### 🖼️ Conversión de imágenes
| Entrada | Formatos de salida |
|---------|-------------------|
| JPG, PNG, WEBP, GIF, BMP | JPG, PNG, WEBP, GIF, BMP, **PDF**, **OCR** |

- Redimensionado con calidad ajustable (Original / 75% / 50%)
- Conversión de imagen a PDF
- **OCR**: Reconocimiento óptico de caracteres a TXT, DOCX, XLSX o PDF

### 📄 Conversión de documentos

| Entrada | Formatos de salida |
|---------|-------------------|
| DOCX | DOCX, PDF, TXT, MD, HTML, EPUB |
| TXT | TXT, DOCX, HTML, **PDF** |
| HTML / HTM | HTML, DOCX, TXT, **PDF** |
| MD / Markdown | MD, DOCX, HTML, TXT |
| RTF | TXT, DOCX |
| ODT | TXT |
| PDF | TXT, DOCX, HTML, EPUB, MD |
| EPUB | TXT, HTML, EPUB, DOCX, PDF, MD |

- Preserva formato en DOCX → PDF (headings, bold, italic, color, font, tablas, imágenes)
- HTML → PDF con resaltado de sintaxis (modo código VS Code)
- EPUB → PDF con contenido renderizado como libro

### 📊 Conversión de datos

| Entrada | Formatos de salida |
|---------|-------------------|
| XLSX / XLS | XLSX, CSV, JSON, **PDF** |
| CSV | CSV, XLSX, JSON, **PDF** |
| JSON | JSON, CSV, XLSX, **PDF** |

- Tablas optimizadas para PDF con escalado de fuente y columnas
- Soporte para archivos grandes con columnas anchas

---

## 🔧 Funcionamiento

### Interfaz
- Ventana única con selector de archivos (arrastrar y soltar)
- Panel lateral de formato y calidad
- Progreso en ventana separada con cancelación
- Menú contextual en el Explorador de Windows ("Convertir con TurnAFile")
- Temas claro y oscuro
- Multiidioma: Español, English, Français, Deutsch, 中文, 日本語

### Conversión por lotes
- Procesa múltiples archivos simultáneamente
- Cada archivo con su formato y calidad individual
- Ventana de progreso con cancelación
- Log de conversión con resultados

### OCR
- Extrae texto de imágenes usando Tesseract OCR
- Resultados exportables a TXT, DOCX, XLSX o PDF
- Detecta automáticamente el idioma del sistema

---

## 🚀 Instalación

1. Descarga la última versión desde [Releases](https://github.com/nocloudware/TurnAFile/releases)
2. Extrae el archivo ZIP en una carpeta
3. Ejecuta `TurnAFile.exe`

**Requisitos:** Windows 10/11 (64-bit), .NET 8.0 Runtime

---

## 🛠️ Tecnologías

| Componente | Propósito | Licencia |
|-----------|-----------|----------|
| **.NET 8.0 WPF** | Framework de la aplicación | MIT |
| **FFmpeg** | Conversión de video/audio | LGPL/GPL |
| **QuestPDF** | Generación de PDF | MIT Community |
| **Open XML SDK** | Lectura/escritura DOCX | MIT |
| **ClosedXML** | Lectura/escritura XLSX | MIT |
| **Tesseract OCR** | Reconocimiento óptico de caracteres | Apache 2.0 |
| **PdfPig** | Extracción de texto de PDF | Apache 2.0 |
| **WPF-UI** | Biblioteca de controles modernos | MIT |

---

## 📝 Licencia

TurnAFile es software gratuito bajo licencia MIT.  
Ver [THIRD_PARTY_NOTICES.txt](THIRD_PARTY_NOTICES.txt) para licencias de componentes de terceros.
