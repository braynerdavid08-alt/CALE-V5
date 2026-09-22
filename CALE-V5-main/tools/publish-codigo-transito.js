/**
 * Publish filtered Transiteca dataset into the API content folder for the app.
 */
const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const src = path.join(root, 'data', 'codigo-transito-transiteca', 'articles.json');
const destDir = path.join(root, 'src', 'Cale.Api', 'SeedData', 'codigo-transito');

const raw = JSON.parse(fs.readFileSync(src, 'utf8'));
const articles = (raw.articles || []).filter(
  (a) => a.plainText && String(a.plainText).trim().length >= 40 && !(a.flags || []).includes('no_blocks')
);

articles.sort((a, b) => a.number - b.number);

const dataset = {
  meta: {
    ...raw.meta,
    articleCount: articles.length,
    publishedAt: new Date().toISOString(),
    note: 'Solo artículos con cuerpo extraído de Transiteca (sin placeholders vacíos).'
  },
  articles
};

const index = {
  meta: dataset.meta,
  articles: articles.map((a) => ({
    number: a.number,
    name: a.name,
    sourceUrl: a.sourceUrl,
    preview: String(a.plainText || '').slice(0, 200),
    notes: (a.notes || []).map((n) => n.variant).filter(Boolean),
    contentHash: a.contentHash
  }))
};

fs.mkdirSync(destDir, { recursive: true });
fs.writeFileSync(path.join(destDir, 'articles.json'), JSON.stringify(dataset), 'utf8');
fs.writeFileSync(path.join(destDir, 'index.json'), JSON.stringify(index), 'utf8');

// Refresh report summary
const reportPath = path.join(root, 'data', 'codigo-transito-transiteca', 'REPORT.md');
const report = [
  '# Reporte extracción Código de Tránsito (Transiteca)',
  '',
  `- Publicado: ${dataset.meta.publishedAt}`,
  `- Artículos con contenido: **${articles.length}** (Artículo ${articles[0]?.number} … ${articles[articles.length - 1]?.number})`,
  `- Fuente: ${raw.meta?.sourceBase}`,
  `- Extractor: ${raw.meta?.extractorVersion}`,
  '',
  '## Notas',
  '- Se excluyeron URLs sin bloques de contenido (placeholders / soft-404).',
  '- El texto se tomó literal de Transiteca (meta description / payload), sin parafrasear.',
  '- HTML crudo de auditoría en `data/codigo-transito-transiteca/html/`.',
  ''
].join('\n');
fs.writeFileSync(reportPath, report, 'utf8');
fs.writeFileSync(
  path.join(root, 'data', 'codigo-transito-transiteca', 'articles.published.json'),
  JSON.stringify(dataset, null, 2),
  'utf8'
);

console.log('Published', articles.length, 'articles →', destDir);
