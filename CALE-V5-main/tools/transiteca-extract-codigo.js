const fs = require('fs');
const path = require('path');
const https = require('https');
const http = require('http');

const UA = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 CALE-TransitecaExtractor/1.0';
const BASE = 'https://transiteca.app/codigo-de-transito';

function fetchText(url) {
  return new Promise((resolve, reject) => {
    const lib = url.startsWith('https') ? https : http;
    const req = lib.get(url, { headers: { 'User-Agent': UA, Accept: 'text/html' } }, (res) => {
      if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
        fetchText(new URL(res.headers.location, url).href).then(resolve, reject);
        return;
      }
      const chunks = [];
      res.on('data', (c) => chunks.push(c));
      res.on('end', () => {
        const buf = Buffer.concat(chunks);
        resolve({ status: res.statusCode || 0, body: buf.toString('utf8'), bytes: buf.length });
      });
    });
    req.on('error', reject);
    req.setTimeout(45000, () => {
      req.destroy(new Error('timeout'));
    });
  });
}

function decodeHtmlEntities(s) {
  return s
    .replace(/&quot;/g, '"')
    .replace(/&#x27;/g, "'")
    .replace(/&#39;/g, "'")
    .replace(/&amp;/g, '&')
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&#(\d+);/g, (_, n) => String.fromCharCode(Number(n)))
    .replace(/&#x([0-9a-fA-F]+);/g, (_, h) => String.fromCharCode(parseInt(h, 16)));
}

function extractMetaDescriptionJson(html) {
  const m = html.match(/name="description"\s+content="([^"]*)"/i);
  if (!m) return null;
  const raw = decodeHtmlEntities(m[1]);
  try {
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed : null;
  } catch {
    return null;
  }
}

function extractTitle(html) {
  const t = html.match(/<title>([^<]*)<\/title>/i);
  const titleTag = t ? decodeHtmlEntities(t[1].replace(/\s+/g, ' ').trim()) : null;
  let name = null;
  if (titleTag) {
    const m = titleTag.match(/^Artículo\s+(\d+)\s*[-–—]\s*([^|]+)/i);
    if (m) name = m[2].trim();
  }
  if (!name) {
    const h1 = html.match(/<h1[^>]*>([\s\S]*?)<\/h1>/i);
    if (h1) {
      name = decodeHtmlEntities(h1[1].replace(/<[^>]+>/g, ' ').replace(/\s+/g, ' ').trim());
      name = name
        .replace(/^Código de tránsito\s*-\s*Artículo\s+\d+/i, '')
        .replace(/Código de tránsito\s*-\s*Ley[\s\S]*$/i, '')
        .trim();
    }
  }
  return { titleTag, heading: name || null };
}

/** Collect candidate JSON arrays of content blocks from RSC flight payloads. */
function extractBlocksFromRsc(html) {
  const candidates = [];
  // Match sequences that look like [{"type":"paragraph"...}] possibly with escaping
  const patterns = [
    /(\[{"type":"(?:note|paragraph|list|heading|ordered_list|bullet_list|item)[\s\S]*?\}\])/g,
  ];

  // Unescape common RSC double-encoding inside script strings
  const unescaped = html
    .replace(/\\"/g, '"')
    .replace(/\\\\/g, '\\');

  for (const re of patterns) {
    let m;
    while ((m = re.exec(unescaped)) !== null) {
      const slice = m[1];
      if (slice.length < 20 || slice.length > 2_000_000) continue;
      try {
        const parsed = JSON.parse(slice);
        if (Array.isArray(parsed) && parsed.some((b) => b && typeof b.type === 'string')) {
          candidates.push(parsed);
        }
      } catch {
        /* ignore */
      }
    }
  }
  // Prefer longest array with most text
  candidates.sort((a, b) => textLen(b) - textLen(a));
  return candidates[0] || null;
}

function textLen(blocks) {
  return blocks.reduce((n, b) => n + blocksToPlain([b]).length, 0);
}

function blocksToPlain(blocks) {
  const lines = [];
  for (const b of blocks || []) {
    if (!b || typeof b !== 'object') continue;
    if (b.type === 'note') {
      const t = inlineText(b.content);
      if (t) lines.push(t);
    } else if (b.type === 'paragraph' || b.type === 'heading') {
      const t = inlineText(b.content);
      if (t) lines.push(t);
    } else if (b.type === 'list' || b.type === 'ordered_list' || b.type === 'bullet_list') {
      for (const item of b.content || b.items || []) {
        const t = typeof item === 'string' ? item : inlineText(item.content || item);
        if (t) lines.push(t);
      }
    } else {
      const t = inlineText(b.content);
      if (t) lines.push(t);
    }
  }
  return lines.join('\n\n');
}

function inlineText(content) {
  if (!content) return '';
  if (typeof content === 'string') return content;
  if (!Array.isArray(content)) return '';
  return content
    .map((c) => {
      if (!c) return '';
      if (typeof c === 'string') return c;
      if (typeof c.text === 'string') return c.text;
      if (Array.isArray(c.content)) return inlineText(c.content);
      return '';
    })
    .join('');
}

function normalizeArticle(n, html, sourceUrl) {
  const { titleTag, heading } = extractTitle(html);
  let blocks = extractMetaDescriptionJson(html);
  let source = 'meta.description';
  const rsc = extractBlocksFromRsc(html);
  if (rsc && textLen(rsc) > textLen(blocks || [])) {
    blocks = rsc;
    source = 'rsc.payload';
  }
  const plain = blocksToPlain(blocks || []);
  const nameFromTitle = null; // handled in extractTitle

  const flags = [];
  if (!blocks || blocks.length === 0) flags.push('no_blocks');
  if (plain.trim().length < 40) flags.push('suspiciously_short');
  // Meta descriptions over ~1500 chars are unusual; keep flag only if RSC was empty.
  if (source === 'meta.description' && plain.length > 8000) flags.push('very_long_meta');

  return {
    number: n,
    name: heading || null,
    titleTag: titleTag || null,
    sourceUrl,
    contentSource: source,
    blocks: blocks || [],
    plainText: plain,
    notes: (blocks || [])
      .filter((b) => b.type === 'note')
      .map((b) => ({
        variant: b.variant || null,
        text: inlineText(b.content)
      })),
    flags,
    contentHash: hash(plain),
    fetchedAt: new Date().toISOString()
  };
}

function hash(s) {
  const crypto = require('crypto');
  return crypto.createHash('sha256').update(s, 'utf8').digest('hex');
}

async function sleep(ms) {
  return new Promise((r) => setTimeout(r, ms));
}

async function discoverMaxArticle(start = 1, hardCap = 250) {
  let lastOk = 0;
  let emptyStreak = 0;
  for (let n = start; n <= hardCap; n++) {
    const url = `${BASE}/articulo-${n}`;
    try {
      const { status, body } = await fetchText(url);
      const art = status === 200 ? normalizeArticle(n, body, url) : null;
      const ok = !!art && art.plainText.trim().length >= 40 && !(art.flags || []).includes('no_blocks');
      if (ok) {
        lastOk = n;
        emptyStreak = 0;
      } else {
        emptyStreak++;
        if (lastOk > 0 && emptyStreak >= 3) break;
      }
    } catch {
      emptyStreak++;
      if (lastOk > 0 && emptyStreak >= 3) break;
    }
    await sleep(150);
  }
  return lastOk;
}

async function main() {
  const root = path.resolve(__dirname, '..');
  const outDir = path.join(root, 'data', 'codigo-transito-transiteca');
  const htmlDir = path.join(outDir, 'html');
  fs.mkdirSync(htmlDir, { recursive: true });

  const args = process.argv.slice(2);
  const spikeOnly = args.includes('--spike');
  const maxArg = args.find((a) => a.startsWith('--max='));
  const maxForced = maxArg ? Number(maxArg.split('=')[1]) : null;

  console.log('Discovering article range...');
  const maxN = maxForced || (spikeOnly ? 5 : await discoverMaxArticle());
  console.log('max article =', maxN);

  const articles = [];
  const report = {
    source: BASE,
    startedAt: new Date().toISOString(),
    maxArticle: maxN,
    ok: 0,
    failed: [],
    flagged: [],
    extractorVersion: '1.0.0'
  };

  const list = spikeOnly ? [1, 2, 3, 50, Math.min(100, maxN)] : Array.from({ length: maxN }, (_, i) => i + 1);

  for (const n of list) {
    const url = `${BASE}/articulo-${n}`;
    process.stdout.write(`#${n} `);
    try {
      const { status, body, bytes } = await fetchText(url);
      if (status !== 200) {
        report.failed.push({ number: n, error: `http_${status}` });
        console.log('FAIL http', status);
        await sleep(250);
        continue;
      }
      fs.writeFileSync(path.join(htmlDir, `articulo-${n}.html`), body, 'utf8');
      const art = normalizeArticle(n, body, url);
      art.htmlBytes = bytes;
      articles.push(art);
      if (art.flags.length) report.flagged.push({ number: n, flags: art.flags });
      report.ok++;
      console.log('OK', art.contentSource, 'chars', art.plainText.length, art.flags.join(',') || '-');
    } catch (e) {
      report.failed.push({ number: n, error: String(e.message || e) });
      console.log('ERR', e.message || e);
    }
    await sleep(spikeOnly ? 200 : 350);
  }

  report.finishedAt = new Date().toISOString();
  report.articleCount = articles.length;

  const dataset = {
    meta: {
      title: 'Código Nacional de Tránsito — dataset Transiteca',
      sourceBase: BASE,
      lawHint: 'Ley 769 de 2002 (según Transiteca)',
      extractedAt: report.finishedAt,
      extractorVersion: report.extractorVersion,
      articleCount: articles.length,
      attribution:
        'Contenido extraído literalmente desde transiteca.app para uso educativo en CALE. No parafraseado.'
    },
    articles: articles.map((a) => ({
      number: a.number,
      name: a.name,
      sourceUrl: a.sourceUrl,
      contentSource: a.contentSource,
      blocks: a.blocks,
      plainText: a.plainText,
      notes: a.notes,
      flags: a.flags,
      contentHash: a.contentHash,
      fetchedAt: a.fetchedAt
    }))
  };

  fs.writeFileSync(path.join(outDir, 'articles.json'), JSON.stringify(dataset, null, 2), 'utf8');
  fs.writeFileSync(path.join(outDir, 'report.json'), JSON.stringify(report, null, 2), 'utf8');

  const md = [
    '# Reporte extracción Código de Tránsito (Transiteca)',
    '',
    `- Inicio: ${report.startedAt}`,
    `- Fin: ${report.finishedAt}`,
    `- Artículos OK: ${report.ok}`,
    `- Fallidos: ${report.failed.length}`,
    `- Con flags: ${report.flagged.length}`,
    `- Max N: ${report.maxArticle}`,
    '',
    '## Fallidos',
    ...(report.failed.length
      ? report.failed.map((f) => `- Art. ${f.number}: ${f.error}`)
      : ['- (ninguno)']),
    '',
    '## Flags',
    ...(report.flagged.length
      ? report.flagged.map((f) => `- Art. ${f.number}: ${f.flags.join(', ')}`)
      : ['- (ninguno)']),
    ''
  ].join('\n');
  fs.writeFileSync(path.join(outDir, 'REPORT.md'), md, 'utf8');

  // Slim index for the app UI
  const index = {
    meta: dataset.meta,
    articles: dataset.articles.map((a) => ({
      number: a.number,
      name: a.name,
      sourceUrl: a.sourceUrl,
      preview: a.plainText.slice(0, 180),
      flags: a.flags,
      contentHash: a.contentHash
    }))
  };
  fs.writeFileSync(path.join(outDir, 'index.json'), JSON.stringify(index, null, 2), 'utf8');

  console.log('\nWrote', outDir);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
