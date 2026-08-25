# -*- coding: utf-8 -*-
"""Generate SVG placeholders for Colombian traffic signals by family."""
import json
import html
from pathlib import Path

CAT = Path(r"d:\APPS programacion\CALE-V5-main\CALE-V5-main\src\Cale.Api\SeedData\senales-catalog.json")
OUT = Path(r"d:\APPS programacion\CALE-V5-main\CALE-V5-main\src\Cale.Api\wwwroot\signals")
OUT.mkdir(parents=True, exist_ok=True)

FAMILY_STYLE = {
    "Señales reglamentarias": {
        "bg": "#ffffff",
        "border": "#c62828",
        "accent": "#c62828",
        "shape": "circle",
    },
    "Señales preventivas": {
        "bg": "#ffeb3b",
        "border": "#212121",
        "accent": "#212121",
        "shape": "diamond",
    },
    "Señales informativas": {
        "bg": "#1565c0",
        "border": "#0d47a1",
        "accent": "#ffffff",
        "shape": "rect",
    },
}


def wrap_text(text, max_chars=18, max_lines=4):
    words = text.split()
    lines, cur = [], ""
    for w in words:
        trial = (cur + " " + w).strip()
        if len(trial) <= max_chars:
            cur = trial
        else:
            if cur:
                lines.append(cur)
            cur = w
        if len(lines) >= max_lines:
            break
    if cur and len(lines) < max_lines:
        lines.append(cur)
    if len(lines) == max_lines and words:
        # truncate last
        if len(lines[-1]) > max_chars - 1:
            lines[-1] = lines[-1][: max_chars - 1] + "…"
    return lines


def svg_for(item):
    family = item["family"]
    style = FAMILY_STYLE.get(family, FAMILY_STYLE["Señales informativas"])
    code = html.escape(item["code"])
    name = item["name"]
    lines = wrap_text(name.upper())
    escaped_lines = [html.escape(l) for l in lines]

    if style["shape"] == "circle":
        shape = f'<circle cx="160" cy="150" r="110" fill="{style["bg"]}" stroke="{style["border"]}" stroke-width="14"/>'
        text_y0 = 140
    elif style["shape"] == "diamond":
        shape = f'<polygon points="160,28 292,160 160,292 28,160" fill="{style["bg"]}" stroke="{style["border"]}" stroke-width="10"/>'
        text_y0 = 145
    else:
        shape = f'<rect x="40" y="60" width="240" height="180" rx="18" fill="{style["bg"]}" stroke="{style["border"]}" stroke-width="8"/>'
        text_y0 = 140

    line_h = 22
    start_y = text_y0 - (len(escaped_lines) - 1) * line_h / 2
    text_nodes = []
    for i, line in enumerate(escaped_lines):
        y = start_y + i * line_h
        text_nodes.append(
            f'<text x="160" y="{y:.1f}" text-anchor="middle" font-family="Segoe UI, Arial, sans-serif" '
            f'font-size="16" font-weight="700" fill="{style["accent"]}">{line}</text>'
        )

    return f'''<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" width="320" height="360" viewBox="0 0 320 360" role="img" aria-label="{html.escape(name)}">
  <rect width="320" height="360" fill="#f5f5f5"/>
  {shape}
  {"".join(text_nodes)}
  <text x="160" y="340" text-anchor="middle" font-family="Segoe UI, Arial, sans-serif" font-size="14" fill="#616161">{code}</text>
</svg>
'''


def main():
    items = json.loads(CAT.read_text(encoding="utf-8"))
    for item in items:
        path = OUT / f"{item['code']}.svg"
        path.write_text(svg_for(item), encoding="utf-8")
    print(f"Wrote {len(items)} SVGs to {OUT}")


if __name__ == "__main__":
    main()
