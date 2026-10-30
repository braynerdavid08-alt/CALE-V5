# -*- coding: utf-8 -*-
"""Generate distinctive SVG drawings for Colombian traffic signs (Manual INVIAS shapes).

These are schematic official shapes (octagon PARE, yield triangle, arrows, etc.),
not photo replacements. When a real exam photo exists, SignImageOverrides wins.
"""
from __future__ import annotations

import html
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CAT = ROOT / "src" / "Cale.Api" / "SeedData" / "senales-catalog.json"
OUT = ROOT / "src" / "Cale.Api" / "wwwroot" / "signals"
OUT.mkdir(parents=True, exist_ok=True)

W, H = 320, 360


def esc(s: str) -> str:
    return html.escape(s, quote=True)


def frame(inner: str, label: str, code: str) -> str:
    return f'''<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}" role="img" aria-label="{esc(label)}">
  <rect width="{W}" height="{H}" fill="#f3f5f7"/>
  {inner}
  <text x="160" y="348" text-anchor="middle" font-family="Segoe UI, Arial, sans-serif" font-size="13" fill="#546e7a">{esc(code)}</text>
</svg>
'''


def octagon_pare() -> str:
    # Flat-top stop-sign octagon
    pts = "110,45 210,45 255,90 255,190 210,235 110,235 65,190 65,90"
    return f'''
  <polygon points="{pts}" fill="#c62828" stroke="#7f0000" stroke-width="4"/>
  <text x="160" y="160" text-anchor="middle" font-family="Arial Black, Arial, sans-serif"
        font-size="42" font-weight="800" fill="#ffffff">PARE</text>
'''


def yield_triangle() -> str:
    return '''
  <polygon points="160,48 286,268 34,268" fill="#ffffff" stroke="#c62828" stroke-width="18"/>
  <polygon points="160,88 248,248 72,248" fill="#ffffff"/>
  <text x="160" y="220" text-anchor="middle" font-family="Segoe UI, Arial, sans-serif"
        font-size="18" font-weight="800" fill="#c62828">CEDA</text>
'''


def blue_circle(glyph: str) -> str:
    return f'''
  <circle cx="160" cy="150" r="112" fill="#1565c0" stroke="#0d47a1" stroke-width="6"/>
  {glyph}
'''


def red_ring(glyph: str, slash: bool = False) -> str:
    slash_el = (
        '<line x1="78" y1="230" x2="242" y2="70" stroke="#c62828" stroke-width="18" stroke-linecap="round"/>'
        if slash
        else ""
    )
    return f'''
  <circle cx="160" cy="150" r="112" fill="#ffffff" stroke="#c62828" stroke-width="16"/>
  {glyph}
  {slash_el}
'''


def yellow_diamond(glyph: str) -> str:
    return f'''
  <polygon points="160,28 292,160 160,292 28,160" fill="#ffeb3b" stroke="#212121" stroke-width="10"/>
  {glyph}
'''


def info_rect(glyph: str, fill: str = "#1565c0") -> str:
    return f'''
  <rect x="36" y="48" width="248" height="204" rx="14" fill="{fill}" stroke="#0d47a1" stroke-width="6"/>
  {glyph}
'''


def arrow_up(color: str = "#ffffff", x: int = 160, y: int = 150, s: float = 1.0) -> str:
    return f'''
  <g transform="translate({x},{y}) scale({s})">
    <polygon points="0,-70 -32,-18 -12,-18 -12,70 12,70 12,-18 32,-18" fill="{color}"/>
  </g>
'''


def arrow_left(color: str = "#ffffff", x: int = 160, y: int = 150, s: float = 1.0) -> str:
    return f'''
  <g transform="translate({x},{y}) scale({s})">
    <polygon points="-70,0 -18,-32 -18,-12 70,-12 70,12 -18,12 -18,32" fill="{color}"/>
  </g>
'''


def arrow_right(color: str = "#ffffff", x: int = 160, y: int = 150, s: float = 1.0) -> str:
    return f'''
  <g transform="translate({x},{y}) scale({s})">
    <polygon points="70,0 18,32 18,12 -70,12 -70,-12 18,-12 18,-32" fill="{color}"/>
  </g>
'''


def arrow_u_turn(color: str = "#ffffff") -> str:
    return f'''
  <path d="M120 210 V110 A40 40 0 0 1 200 110 V150" fill="none" stroke="{color}" stroke-width="18" stroke-linecap="round"/>
  <polygon points="200,150 230,150 200,190" fill="{color}"/>
'''


def car_simple(color: str = "#212121", slash_ready: bool = False) -> str:
    return f'''
  <g transform="translate(160,150)">
    <rect x="-46" y="-18" width="92" height="36" rx="8" fill="{color}"/>
    <rect x="-30" y="-34" width="60" height="22" rx="6" fill="{color}"/>
    <circle cx="-28" cy="22" r="10" fill="{color}"/>
    <circle cx="28" cy="22" r="10" fill="{color}"/>
  </g>
'''


def pedestrian(color: str = "#212121") -> str:
    return f'''
  <g transform="translate(160,150)" fill="{color}">
    <circle cx="0" cy="-48" r="14"/>
    <rect x="-10" y="-30" width="20" height="46" rx="6"/>
    <rect x="-28" y="-18" width="18" height="10" rx="3"/>
    <rect x="10" y="-18" width="18" height="10" rx="3"/>
    <rect x="-16" y="16" width="12" height="40" rx="4"/>
    <rect x="4" y="16" width="12" height="40" rx="4"/>
  </g>
'''


def bike(color: str = "#212121") -> str:
    return f'''
  <g transform="translate(160,155)" fill="none" stroke="{color}" stroke-width="8" stroke-linecap="round">
    <circle cx="-40" cy="10" r="28"/>
    <circle cx="40" cy="10" r="28"/>
    <path d="M-40 10 L0 -10 L40 10 M0 -10 L0 -36 L22 -36"/>
  </g>
'''


def motorcycle(color: str = "#212121") -> str:
    return f'''
  <g transform="translate(160,155)" fill="none" stroke="{color}" stroke-width="8" stroke-linecap="round">
    <circle cx="-42" cy="12" r="26"/>
    <circle cx="42" cy="12" r="26"/>
    <path d="M-42 12 L-10 -8 L30 -8 L42 12 M-10 -8 L0 -36 L28 -28"/>
  </g>
'''


def bus(color: str = "#212121") -> str:
    return f'''
  <g transform="translate(160,150)" fill="{color}">
    <rect x="-58" y="-34" width="116" height="56" rx="10"/>
    <rect x="-46" y="-24" width="28" height="18" rx="3" fill="#fff"/>
    <rect x="-10" y="-24" width="28" height="18" rx="3" fill="#fff"/>
    <rect x="26" y="-24" width="22" height="18" rx="3" fill="#fff"/>
    <circle cx="-34" cy="30" r="10"/>
    <circle cx="34" cy="30" r="10"/>
  </g>
'''


def parking_p(color: str = "#ffffff") -> str:
    return f'''
  <text x="160" y="175" text-anchor="middle" font-family="Arial Black, Arial, sans-serif"
        font-size="110" font-weight="800" fill="{color}">P</text>
'''


def speed_number(n: str = "60") -> str:
    return f'''
  <text x="160" y="175" text-anchor="middle" font-family="Arial Black, Arial, sans-serif"
        font-size="72" font-weight="800" fill="#212121">{esc(n)}</text>
'''


def text_badge(lines: list[str], color: str = "#212121", size: int = 22) -> str:
    n = len(lines)
    start = 150 - (n - 1) * 14
    nodes = []
    for i, line in enumerate(lines):
        nodes.append(
            f'<text x="160" y="{start + i * 28}" text-anchor="middle" '
            f'font-family="Segoe UI, Arial, sans-serif" font-size="{size}" font-weight="800" fill="{color}">{esc(line)}</text>'
        )
    return "\n".join(nodes)


def curve_arrow(left: bool = True) -> str:
    # Arc suggesting a bend
    if left:
        d = "M210 220 C210 120, 180 70, 90 70"
        head = '<polygon points="90,70 120,55 115,90" fill="#212121"/>'
    else:
        d = "M110 220 C110 120, 140 70, 230 70"
        head = '<polygon points="230,70 200,55 205,90" fill="#212121"/>'
    return f'<path d="{d}" fill="none" stroke="#212121" stroke-width="16" stroke-linecap="round"/>{head}'


def crossroads() -> str:
    return '''
  <rect x="148" y="70" width="24" height="160" rx="4" fill="#212121"/>
  <rect x="80" y="138" width="160" height="24" rx="4" fill="#212121"/>
'''


def t_junction() -> str:
    return '''
  <rect x="148" y="70" width="24" height="100" rx="4" fill="#212121"/>
  <rect x="80" y="170" width="160" height="24" rx="4" fill="#212121"/>
'''


def roundabout() -> str:
    return '''
  <circle cx="160" cy="150" r="48" fill="none" stroke="#212121" stroke-width="14"/>
  <polygon points="160,78 172,100 148,100" fill="#212121"/>
  <polygon points="222,175 198,185 210,158" fill="#212121"/>
  <polygon points="98,175 110,158 122,185" fill="#212121"/>
'''


def traffic_light() -> str:
    return '''
  <rect x="130" y="70" width="60" height="160" rx="12" fill="#212121"/>
  <circle cx="160" cy="105" r="16" fill="#c62828"/>
  <circle cx="160" cy="150" r="16" fill="#fbc02d"/>
  <circle cx="160" cy="195" r="16" fill="#2e7d32"/>
'''


def bump() -> str:
    return '''
  <path d="M60 200 Q160 80 260 200" fill="none" stroke="#212121" stroke-width="16" stroke-linecap="round"/>
'''


def hill_down() -> str:
    return '''
  <polygon points="60,220 260,80 260,220" fill="#212121"/>
  <polygon points="210,120 240,120 225,150" fill="#ffeb3b"/>
'''


def hill_up() -> str:
    return '''
  <polygon points="60,80 260,220 60,220" fill="#212121"/>
  <polygon points="90,150 120,150 105,120" fill="#ffeb3b"/>
'''


def narrow_bridge() -> str:
    return '''
  <path d="M70 80 L120 150 L70 220" fill="none" stroke="#212121" stroke-width="14" stroke-linecap="round"/>
  <path d="M250 80 L200 150 L250 220" fill="none" stroke="#212121" stroke-width="14" stroke-linecap="round"/>
  <rect x="130" y="130" width="60" height="40" rx="4" fill="#212121"/>
'''


def children() -> str:
    return '''
  <g transform="translate(120,150)" fill="#212121">
    <circle cx="0" cy="-40" r="12"/>
    <rect x="-8" y="-24" width="16" height="36" rx="4"/>
    <rect x="-14" y="12" width="10" height="32" rx="3"/>
    <rect x="4" y="12" width="10" height="32" rx="3"/>
  </g>
  <g transform="translate(200,160)" fill="#212121">
    <circle cx="0" cy="-32" r="10"/>
    <rect x="-7" y="-20" width="14" height="30" rx="4"/>
    <rect x="-12" y="10" width="9" height="28" rx="3"/>
    <rect x="3" y="10" width="9" height="28" rx="3"/>
  </g>
'''


def animals() -> str:
    return '''
  <g transform="translate(160,160)" fill="#212121">
    <ellipse cx="0" cy="0" rx="50" ry="28"/>
    <circle cx="48" cy="-18" r="18"/>
    <rect x="-40" y="20" width="12" height="36" rx="3"/>
    <rect x="-10" y="20" width="12" height="36" rx="3"/>
    <rect x="16" y="20" width="12" height="36" rx="3"/>
    <rect x="40" y="16" width="12" height="40" rx="3"/>
  </g>
'''


def railroad() -> str:
    return '''
  <rect x="148" y="60" width="24" height="180" fill="#212121"/>
  <rect x="100" y="90" width="120" height="12" fill="#212121"/>
  <rect x="100" y="130" width="120" height="12" fill="#212121"/>
  <rect x="100" y="170" width="120" height="12" fill="#212121"/>
  <circle cx="110" cy="70" r="14" fill="#c62828"/>
  <circle cx="210" cy="70" r="14" fill="#c62828"/>
'''


def two_way_arrows() -> str:
    return f'''
  {arrow_up("#212121", 125, 150, 0.65)}
  <g transform="translate(195,150) scale(0.65)">
    <polygon points="0,70 -32,18 -12,18 -12,-70 12,-70 12,18 32,18" fill="#212121"/>
  </g>
'''


def default_reglamentaria(name: str) -> str:
    short = name[:28] + ("…" if len(name) > 28 else "")
    words = short.split()
    lines = []
    cur = ""
    for w in words:
        t = (cur + " " + w).strip()
        if len(t) <= 12:
            cur = t
        else:
            if cur:
                lines.append(cur)
            cur = w
        if len(lines) >= 3:
            break
    if cur and len(lines) < 3:
        lines.append(cur)
    prohib = "PROHIB" in name.upper() or name.upper().startswith("NO ")
    return red_ring(text_badge(lines or [name[:10]], "#212121", 18), slash=prohib)


def default_preventiva(name: str) -> str:
    words = name.upper().replace("“", "").replace("”", "").split()
    lines, cur = [], ""
    for w in words[:8]:
        t = (cur + " " + w).strip()
        if len(t) <= 11:
            cur = t
        else:
            if cur:
                lines.append(cur)
            cur = w
        if len(lines) >= 3:
            break
    if cur and len(lines) < 3:
        lines.append(cur)
    return yellow_diamond(text_badge(lines or ["?"], "#212121", 16))


def default_informativa(name: str) -> str:
    words = name.upper().split()
    lines, cur = [], ""
    for w in words[:8]:
        t = (cur + " " + w).strip()
        if len(t) <= 14:
            cur = t
        else:
            if cur:
                lines.append(cur)
            cur = w
        if len(lines) >= 3:
            break
    if cur and len(lines) < 3:
        lines.append(cur)
    return info_rect(text_badge(lines or ["INFO"], "#ffffff", 18))


def glyph_for(code: str, family: str, name: str) -> str:
    u = name.upper()
    c = code.upper()

    # Explicit codes first (Colombian catalog)
    special = {
        "SR-01": octagon_pare(),
        "SR-02": yield_triangle(),
        "SR-03": blue_circle(arrow_up()),
        "SR-04": red_ring(text_badge(["NO", "PASE"], "#212121", 28), slash=True),
        "SR-05": blue_circle(arrow_left()),
        "SR-06": red_ring(arrow_left("#212121", 160, 150, 0.85), slash=True),
        "SR-07": blue_circle(arrow_right()),
        "SR-08": red_ring(arrow_right("#212121", 160, 150, 0.85), slash=True),
        "SR-09": blue_circle(arrow_u_turn()),
        "SR-10": red_ring(arrow_u_turn("#212121"), slash=True),
        "SR-11": blue_circle(two_way_arrows().replace("#212121", "#ffffff")),
        "SR-16": red_ring(car_simple(), slash=True),
        "SR-18": red_ring(bus(), slash=True),
        "SR-20": red_ring(pedestrian(), slash=True),
        "SR-22": red_ring(bike(), slash=True),
        "SR-23": red_ring(motorcycle(), slash=True),
        "SR-26": red_ring(two_way_arrows(), slash=True),
        "SR-28": red_ring(parking_p("#1565c0"), slash=True),
        "SR-28A": red_ring(parking_p("#1565c0") + text_badge(["Y DETENERSE"], "#c62828", 14), slash=True),
        "SR-30": red_ring(speed_number("60")),
        "SR-30A": blue_circle(speed_number("40").replace("#212121", "#ffffff")),
        "SR-30B": red_ring(speed_number("40")),
        "SR-34": blue_circle(parking_p() + text_badge(["TAXI"], "#ffffff", 14)),
        "SR-38": blue_circle(arrow_up()),
        "SR-39": blue_circle(two_way_arrows().replace("#212121", "#ffffff")),
        "SR-48": red_ring(two_way_arrows() + text_badge(["FIN"], "#2e7d32", 16), slash=False),
        "SR-52": red_ring(bus(), slash=True),
        "SP-01": yellow_diamond(curve_arrow(True)),
        "SP-02": yellow_diamond(curve_arrow(False)),
        "SP-03": yellow_diamond(curve_arrow(True)),
        "SP-04": yellow_diamond(curve_arrow(False)),
        "SP-11": yellow_diamond(crossroads()),
        "SP-14": yellow_diamond(t_junction()),
        "SP-20": yellow_diamond(roundabout()),
        "SP-23": yellow_diamond(traffic_light()),
        "SP-25": yellow_diamond(bump()),
        "SP-25A": yellow_diamond(bump()),
        "SP-27": yellow_diamond(hill_down()),
        "SP-27A": yellow_diamond(hill_up()),
        "SP-29": yellow_diamond(
            '<polygon points="160,78 200,78 222,100 222,140 200,162 160,162 120,162 98,140 98,100 120,78" fill="#c62828"/>'
            '<text x="160" y="132" text-anchor="middle" font-family="Arial Black, Arial, sans-serif" font-size="18" font-weight="800" fill="#fff">PARE</text>'
        ),
        "SP-33": yellow_diamond(
            '<polygon points="160,90 230,210 90,210" fill="#fff" stroke="#c62828" stroke-width="10"/>'
        ),
        "SP-36": yellow_diamond(narrow_bridge()),
        "SP-39": yellow_diamond(two_way_arrows()),
        "SP-46": yellow_diamond(children()),
        "SP-47": yellow_diamond(pedestrian()),
        "SP-50": yellow_diamond(animals()),
        "SP-51": yellow_diamond(railroad()),
    }
    if c in special:
        return special[c]

    # Heuristics by name
    if "PARE" in u and "PROXIM" in u:
        return yellow_diamond(
            '<polygon points="160,70 220,70 250,100 250,140 220,170 160,170 100,170 70,140 70,100 100,70" fill="#c62828"/>'
            + text_badge(["PARE"], "#fff", 18)
        )
    if u.strip() == "PARE" or (c == "SR-01"):
        return octagon_pare()
    if "CEDA" in u:
        return yield_triangle() if "reglament" in family.lower() else yellow_diamond(
            '<polygon points="160,90 230,210 90,210" fill="#fff" stroke="#c62828" stroke-width="10"/>'
        )
    if "VELOCIDAD MÁXIMA" in u or "VELOCIDAD MAXIMA" in u:
        return red_ring(speed_number("60"))
    if "VELOCIDAD MÍNIMA" in u or "VELOCIDAD MINIMA" in u:
        return blue_circle(speed_number("40").replace("#212121", "#ffffff"))
    if "PROHIBIDO PARQUEAR" in u or "PROHIBIDO ESTACIONAR" in u:
        return red_ring(parking_p("#1565c0"), slash=True)
    if "ZONA DE ESTACIONAMIENTO" in u or (u.startswith("PARQUEO") or " ESTACIONAMIENTO" in u):
        return blue_circle(parking_p())
    if "PROHIBIDO GIRAR A LA IZQUIERDA" in u:
        return red_ring(arrow_left("#212121", 160, 150, 0.85), slash=True)
    if "PROHIBIDO GIRAR A LA DERECHA" in u:
        return red_ring(arrow_right("#212121", 160, 150, 0.85), slash=True)
    if "GIRO A LA IZQUIERDA" in u:
        return blue_circle(arrow_left())
    if "GIRO A LA DERECHA" in u:
        return blue_circle(arrow_right())
    if "GIRAR EN" in u and "U" in u and "PROHIB" in u:
        return red_ring(arrow_u_turn("#212121"), slash=True)
    if "GIRAR EN" in u and "U" in u:
        return blue_circle(arrow_u_turn())
    if "SIGA DE FRENTE" in u or "SENTIDO ÚNICO" in u or "SENTIDO UNICO" in u:
        return blue_circle(arrow_up())
    if "SEMÁFORO" in u or "SEMAFORO" in u:
        return yellow_diamond(traffic_light())
    if "GLORIETA" in u:
        return yellow_diamond(roundabout())
    if "INTERSECCIÓN" in u or "INTERSECCION" in u:
        return yellow_diamond(crossroads())
    if "CURVA" in u and ("IZQUIERDA" in u or "IZQ" in u):
        return yellow_diamond(curve_arrow(True))
    if "CURVA" in u and ("DERECHA" in u or "DER" in u):
        return yellow_diamond(curve_arrow(False))
    if "RESALTO" in u or "RIZADA" in u or "POMPEYANO" in u:
        return yellow_diamond(bump())
    if "PENDIENTE" in u and "DESCENSO" in u:
        return yellow_diamond(hill_down())
    if "PENDIENTE" in u and "ASCENSO" in u:
        return yellow_diamond(hill_up())
    if "NIÑO" in u or "ESCOLA" in u or "COLEGIO" in u:
        return yellow_diamond(children())
    if "PEATON" in u and "PROHIB" in u:
        return red_ring(pedestrian(), slash=True)
    if "PEATON" in u:
        return yellow_diamond(pedestrian()) if "prevent" in family.lower() else blue_circle(
            pedestrian().replace("#212121", "#ffffff")
        )
    if "BICICLETA" in u or "CICLISTA" in u:
        return red_ring(bike(), slash="PROHIB" in u) if "PROHIB" in u else yellow_diamond(bike())
    if "MOTOCICLETA" in u or "MOTOCICLO" in u:
        return red_ring(motorcycle(), slash=True)
    if "BUS" in u or "BUSES" in u:
        return red_ring(bus(), slash="PROHIB" in u) if "PROHIB" in u else info_rect(bus().replace("#212121", "#ffffff"))
    if "FERROCARRIL" in u or "CRUCE DE VÍA FÉRREA" in u or "FERREA" in u or "FÉRREA" in u:
        return yellow_diamond(railroad())
    if "ANIMAL" in u or "GANADO" in u or "CABALG" in u:
        return yellow_diamond(animals()) if "prevent" in family.lower() else red_ring(animals(), slash="PROHIB" in u)
    if "TÚNEL" in u or "TUNEL" in u:
        return yellow_diamond(
            '<path d="M70 220 V120 A90 90 0 0 1 250 120 V220" fill="none" stroke="#212121" stroke-width="14"/>'
        )
    if "PUENTE" in u:
        return yellow_diamond(narrow_bridge())
    if "AMBOS SENTIDOS" in u or "DOS SENTIDOS" in u:
        return yellow_diamond(two_way_arrows()) if "prevent" in family.lower() else blue_circle(
            two_way_arrows().replace("#212121", "#ffffff")
        )
    if "NO ADELANTAR" in u or "ADELANTAR" in u:
        return red_ring(two_way_arrows(), slash=True)

    fam = family.lower()
    if "reglament" in fam:
        return default_reglamentaria(name)
    if "prevent" in fam:
        return default_preventiva(name)
    return default_informativa(name)


def svg_for(item: dict) -> str:
    code = item["code"]
    name = item["name"]
    family = item["family"]
    inner = glyph_for(code, family, name)
    return frame(inner, name, code)


def main() -> None:
    items = json.loads(CAT.read_text(encoding="utf-8"))
    for item in items:
        # Normalize imageUrl to /signals/{code}.svg if missing
        path = OUT / f"{item['code']}.svg"
        path.write_text(svg_for(item), encoding="utf-8")
    print(f"Wrote {len(items)} SVGs to {OUT}")


if __name__ == "__main__":
    main()
