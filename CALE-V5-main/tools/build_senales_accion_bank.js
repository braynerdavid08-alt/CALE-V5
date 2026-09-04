/**
 * Banco "Señales: qué debes hacer" a partir de senales-catalog.json.
 * Misma disciplina de calidad: posición y longitud de la correcta variables.
 */
const fs = require('fs');
const path = require('path');

const ROOT = path.join(__dirname, '..');
const CATALOG = path.join(ROOT, 'src', 'Cale.Api', 'SeedData', 'senales-catalog.json');
const OUT = path.join(ROOT, 'src', 'Cale.Api', 'SeedData', 'banco-senales-accion.json');

function mulberry32(a) {
  return function () {
    let t = (a += 0x6d2b79f5);
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function shuffle(arr, rand) {
  const a = [...arr];
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(rand() * (i + 1));
    [a[i], a[j]] = [a[j], a[i]];
  }
  return a;
}

function actionFor(sign, index) {
  const n = (sign.name || '').toUpperCase();
  const code = sign.code || '';

  const poolWrongLong = [
    'Acelerar para pasar antes de que la situación se complique, sin evaluar el entorno.',
    'Ignorar la señal porque no hay autoridad presente en ese momento en el sitio.',
    'Detenerse en mitad del carril contrario para “interpretar mejor” la señal.'
  ];
  const poolWrongShort = ['Seguir igual.', 'Acelerar.', 'No mirar.', 'Usar solo el claxon.'];

  let correct;
  let wrongs;
  let difficulty = 'Media';

  if (n.includes('PARE') && !n.includes('PARQUEO') && !n.includes('ESTACION')) {
    correct = 'Detenerse por completo y continuar solo cuando sea seguro.';
    wrongs = [
      'Reducir un poco y seguir sin detenerse del todo.',
      'Pasar si no se ve a nadie a simple vista.',
      poolWrongLong[0]
    ];
    difficulty = 'Baja';
  } else if (n.includes('CEDA')) {
    correct = 'Ceder el paso a quien tenga prioridad y avanzar sin obligarlos a frenar.';
    wrongs = [
      'Detenerse 10 segundos siempre, aunque la vía esté libre.',
      'Acelerar para incorporarse primero.',
      poolWrongLong[1]
    ];
  } else if (n.includes('NO PASE') || n === 'PROHIBIDO EL PASO' || n.includes('VIA CERRADA') || n.includes('VÍA CERRADA')) {
    correct = 'No continuar por ese sentido o tramo; buscar la ruta permitida.';
    wrongs = ['Pasar “un momento” si conoce la zona.', 'Entrar con balizas.', poolWrongShort[0]];
  } else if (n.includes('PROHIBIDO GIRAR') || n.includes('NO GIRAR')) {
    correct = 'No realizar el giro prohibido; seguir de frente u otra maniobra permitida.';
    wrongs = [
      'Girar igual si no viene nadie.',
      'Girar con el claxon como aviso.',
      poolWrongLong[2]
    ];
  } else if (n.includes('GIRO') && n.includes('SOLAMENTE')) {
    correct = 'Realizar únicamente el giro indicado desde el carril correcto.';
    wrongs = [
      'Seguir de frente aunque la flecha obligue el giro.',
      'Girar al lado contrario “por atajo”.',
      poolWrongShort[1]
    ];
  } else if (n.includes('PROHIBIDO') || n.startsWith('NO ')) {
    correct = `Respetar la prohibición (${sign.name}): no hacer esa conducta.`;
    wrongs = [
      'Hacerla solo de noche.',
      'Hacerla con luces de emergencia.',
      'Hacerla si el tráfico está liviano.'
    ];
  } else if (n.includes('VELOCIDAD') || n.includes('MÁXIMA') || n.includes('MAXIMA') || /SR-3\d|SR-2\d/.test(code) && n.includes('KM')) {
    correct = 'No superar el límite indicado y adecuar la velocidad a la condición de la vía.';
    wrongs = [
      'Tomar el número como velocidad mínima obligatoria exacta.',
      'Ignorar el límite si otros van más rápido.',
      poolWrongLong[0]
    ];
  } else if (n.includes('ESTACION') || n.includes('PARQUEO') || n.includes('DETENER')) {
    if (n.includes('PROHIBIDO') || n.includes('NO ')) {
      correct = 'No estacionar ni detenerse en ese tramo.';
      wrongs = ['Bajar “un segundo” en doble fila.', 'Estacionar con balizas.', poolWrongShort[2]];
    } else {
      correct = 'Estacionar o detenerse solo donde la señal lo permita y de forma segura.';
      wrongs = ['Ocupar andén o ciclorruta.', 'Dejar el carro obstruyendo.', poolWrongLong[1]];
    }
  } else if (code.startsWith('SP') || (sign.family || '').toLowerCase().includes('prevent')) {
    correct = 'Reducir velocidad y extremar precaución ante el peligro o condición advertida.';
    wrongs = [
      'Acelerar para “pasar rápido el peligro”.',
      'Ignorar la advertencia si conoce la vía.',
      poolWrongShort[1]
    ];
    if (n.includes('CURVA')) {
      correct = 'Reducir antes de la curva y mantener su carril con firmeza.';
      wrongs = [
        'Entrar a la curva acelerando.',
        'Cortar la curva invadiendo el sentido contrario.',
        poolWrongLong[2]
      ];
    } else if (n.includes('PEATON') || n.includes('ESCOLAR') || n.includes('NIÑOS')) {
      correct = 'Bajar la velocidad y prepararse para ceder o detenerse ante peatones.';
      wrongs = ['Mantener el máximo urbano sin más.', 'Tocar el claxon agresivo.', poolWrongShort[0]];
    } else if (n.includes('RESALTO') || n.includes('BADÉN') || n.includes('BADEN') || n.includes('REDUCTOR')) {
      correct = 'Reducir oportunamente para atravesar el reductor sin riesgo.';
      wrongs = ['Pasarlo a fondo.', 'Frenar en seco encima del resalto.', poolWrongLong[0]];
    } else if (n.includes('ANIMAL') || n.includes('GANADO')) {
      correct = 'Reducir y estar listo para detenerse si hay animales en la vía.';
      wrongs = ['Acelerar para ahuyentarlos.', 'Tocar el claxon a tope sin bajar velocidad.', poolWrongShort[3]];
    }
    difficulty = 'Media';
  } else if (code.startsWith('SI') || (sign.family || '').toLowerCase().includes('inform')) {
    correct = 'Usar la información para orientarse, sin incumplir señales reglamentarias ni semáforos.';
    wrongs = [
      'Creer que la informativa autoriza a violar un PARE o un límite.',
      'Detenerse en el carril a leer con calma el texto largo.',
      poolWrongShort[2]
    ];
    difficulty = 'Baja';
  } else {
    correct = 'Obedecer el mensaje de la señal y adaptar la conducción con seguridad.';
    wrongs = [
      'Ignorarla si no hay cámaras.',
      'Hacer lo contrario “para probar”.',
      poolWrongLong[1]
    ];
  }

  // Variar longitud: alargar incorrectas con frecuencia para que la correcta no sea la más larga.
  if (index % 2 === 0) {
    const pad =
      index % 4 === 0
        ? ' Esa conducta no está respaldada por la señalización vial aplicable.'
        : ' Además, esa idea no corresponde al mensaje de la señal.';
    const wi = index % 3;
    if (wrongs[wi].length <= correct.length + 8) {
      wrongs[wi] = `${wrongs[wi]}${pad}`;
    }
  } else if (index % 5 === 0 && wrongs[1].length < correct.length) {
    wrongs[1] = `${wrongs[1]} En la práctica eso aumenta el riesgo para otros actores viales.`;
  }

  return { correct, wrongs, difficulty };
}

function build() {
  const catalog = JSON.parse(fs.readFileSync(CATALOG, 'utf8'));
  const questions = catalog.map((sign, index) => {
    const { correct, wrongs, difficulty } = actionFor(sign, index);
    const rand = mulberry32(5000 + index * 131);
    let options = [
      { text: correct, isCorrect: true },
      { text: wrongs[0], isCorrect: false },
      { text: wrongs[1], isCorrect: false },
      { text: wrongs[2], isCorrect: false }
    ];
    options = shuffle(options, rand);
    const preferSlot = index % 4;
    const correctIdx = options.findIndex((o) => o.isCorrect);
    if (correctIdx !== preferSlot) {
      const tmp = options[preferSlot];
      options[preferSlot] = options[correctIdx];
      options[correctIdx] = tmp;
    }

    // Garantizar que la correcta no sea sistemáticamente la más larga.
    const ci = options.findIndex((o) => o.isCorrect);
    const maxLen = Math.max(...options.map((o) => o.text.length));
    if (options[ci].text.length === maxLen) {
      const wi = options.findIndex((o, i) => !o.isCorrect && i !== ci);
      if (wi >= 0) {
        options[wi] = {
          ...options[wi],
          text: `${options[wi].text} Esa opción no refleja lo que indica la señal en la vía.`
        };
      }
    }

    const family = sign.family || 'Señales';
    return {
      subject: 'Señales — acción',
      topic: sign.code,
      subtopic: family,
      difficulty,
      type: 'Seleccion multiple',
      text: `Ante la señal "${sign.name}" (${sign.code}), ¿qué debe hacer usted?`,
      imageUrl: sign.imageUrl,
      explanation: `La señal ${sign.code} (${sign.name}) indica una conducta concreta: ${correct}`,
      source: 'Manual de Señalización Vial de Colombia 2024 — interpretación conductual CEA',
      options
    };
  });

  const pos = [0, 0, 0, 0];
  let correctLongest = 0;
  for (const q of questions) {
    const ci = q.options.findIndex((o) => o.isCorrect);
    pos[ci]++;
    const lengths = q.options.map((o) => o.text.length);
    if (q.options[ci].text.length === Math.max(...lengths)) correctLongest++;
  }

  const payload = {
    bankName: 'Señales: qué debes hacer',
    description:
      'Banco conductual CEA: ante cada señal, la acción correcta (no solo el nombre). Posición/longitud de la respuesta variadas.',
    blockName: 'Señales de tránsito — acción',
    replaceExisting: true,
    questions
  };

  fs.writeFileSync(OUT, JSON.stringify(payload, null, 2), 'utf8');
  console.log('Wrote', OUT);
  console.log('Questions:', questions.length);
  console.log('pos', pos, 'correctLongest', correctLongest);
}

build();
