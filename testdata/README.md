# Test documents

[English](#english) · [Deutsch](#deutsch)

## English

Two versions of a fictional library regulation of the equally fictional town of Musterstadt, used by the tests and for trying out Synopio. All content was made up for this purpose; any resemblance to real regulations is coincidental.

| File | Content |
|---|---|
| `satzung-2025.*` | Old version |
| `satzung-2026-entwurf.*` | New draft with added, changed, deleted and split paragraphs |

The Markdown files are the source. `New-TestDocuments.ps1` creates the other formats from them with [pandoc](https://pandoc.org) and Microsoft Edge (headless):

- `.txt` – plain text with line breaks after 72 characters
- `.docx` – Word document
- `.pdf` – PDF printed by Edge, with running headers, footers, page numbers and hyphenated words; the two versions use different layouts on purpose
- `.word.pdf` – the `.docx` saved as PDF with Microsoft Word, as a second PDF producer (created manually)

```powershell
.\New-TestDocuments.ps1 -Pandoc "C:\Path\pandoc.exe"
```

`hyphenation.txt` lists the break points that are inserted as soft hyphens, because Edge does not hyphenate in headless mode. The layouts are defined in `layout-*.css`.

The tests check that all formats of the same version show no differences and that every format finds the same changes between the two versions.

## Deutsch

Zwei Fassungen einer erfundenen Benutzungssatzung für die Stadtbücherei der ebenso erfundenen Stadt Musterstadt, genutzt von den Tests und zum Ausprobieren von Synopio. Alle Inhalte wurden dafür ausgedacht; Ähnlichkeiten mit echten Satzungen sind zufällig.

| Datei | Inhalt |
|---|---|
| `satzung-2025.*` | Alte Fassung |
| `satzung-2026-entwurf.*` | Neuer Entwurf mit eingefügten, geänderten, gelöschten und aufgeteilten Absätzen |

Die Markdown-Dateien sind die Quelle. `New-TestDocuments.ps1` erzeugt daraus mit [pandoc](https://pandoc.org) und Microsoft Edge (headless) die anderen Formate:

- `.txt` – reiner Text mit Zeilenumbrüchen nach 72 Zeichen
- `.docx` – Word-Dokument
- `.pdf` – von Edge gedrucktes PDF mit Kopf- und Fußzeilen, Seitenzahlen und getrennten Wörtern; die beiden Fassungen haben bewusst unterschiedliche Layouts
- `.word.pdf` – die `.docx` mit Microsoft Word als PDF gespeichert, als zweiter PDF-Erzeuger (von Hand erstellt)

```powershell
.\New-TestDocuments.ps1 -Pandoc "C:\Pfad\pandoc.exe"
```

`hyphenation.txt` enthält die Trennstellen, die als weiche Trennzeichen eingefügt werden, weil Edge im Headless-Modus nicht selbst trennt. Die Layouts stehen in `layout-*.css`.

Die Tests prüfen, dass alle Formate derselben Fassung keine Unterschiede zeigen und dass jedes Format dieselben Änderungen zwischen den beiden Fassungen findet.
