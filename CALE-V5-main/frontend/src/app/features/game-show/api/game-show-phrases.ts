/** Random host-style lines for projector / host flash. */
const STEAL_LINES = [
  '¡Se calienta el tablero!',
  '¡Última oportunidad de robo!',
  '¡El banco está en juego!',
  '¡A robar se ha dicho!',
  '¡Tensión máxima en el aula!'
];

const STRIKE3_LINES = [
  '¡Tres strikes! ¡A robar!',
  '¡Se abre el robo!',
  '¡El control se tambalea!',
  '¡Ahora o nunca para el rival!',
  '¡Banco en peligro!'
];

const LIGHTNING_LINES = [
  '⚡ ¡Ronda relámpago! Puntos x2',
  '⚡ ¡Últimos segundos a doble!',
  '⚡ ¡Relámpago activado!'
];

function pick(lines: string[]): string {
  return lines[Math.floor(Math.random() * lines.length)] ?? lines[0]!;
}

export function phraseForSteal(): string {
  return pick(STEAL_LINES);
}

export function phraseForStrike3(): string {
  return pick(STRIKE3_LINES);
}

export function phraseForLightning(): string {
  return pick(LIGHTNING_LINES);
}
