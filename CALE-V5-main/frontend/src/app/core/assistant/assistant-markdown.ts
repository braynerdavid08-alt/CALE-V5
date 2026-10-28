/**
 * Escape-first Markdown for assistant bubbles: headings, paragraphs, lists, bold.
 * Text is escaped before any tags are added, so content cannot inject HTML/scripts.
 */
export function renderAssistantMarkdown(source: string): string {
  const lines = source.replace(/\r\n?/g, '\n').split('\n');
  const html: string[] = [];
  let i = 0;

  while (i < lines.length) {
    const trimmed = lines[i].trim();

    if (!trimmed) {
      i++;
      continue;
    }

    const heading = /^(#{1,3})\s+(.*)$/.exec(trimmed);
    if (heading) {
      const level = Math.min(heading[1].length + 1, 4);
      html.push(`<h${level}>${inline(heading[2])}</h${level}>`);
      i++;
      continue;
    }

    // Bold-only title lines: **Agenda** or **Viernes, 30 de octubre**
    const boldTitle = /^\*\*(.+?)\*\*\s*$/.exec(trimmed);
    if (boldTitle && !trimmed.includes(' · ') && !trimmed.startsWith('-')) {
      html.push(`<p class="asst-title">${inline('**' + boldTitle[1] + '**')}</p>`);
      i++;
      continue;
    }

    const bullet = /^[-*•]\s+/;
    const numbered = /^\d+[.)]\s+/;
    const nested = /^\s{2,}[-*•]\s+/;
    if (bullet.test(trimmed) || numbered.test(trimmed) || nested.test(lines[i])) {
      const items: string[] = [];
      const ordered = numbered.test(trimmed);
      while (i < lines.length) {
        const raw = lines[i];
        const t = raw.trim();
        if (!t) {
          break;
        }
        if (nested.test(raw) || /^\s{2,}/.test(raw) && items.length > 0) {
          const nestedText = t.replace(/^[-*•]\s+/, '');
          if (items.length > 0) {
            items[items.length - 1] += `<ul class="asst-sub"><li>${inline(nestedText)}</li></ul>`;
          }
          i++;
          continue;
        }
        if (!(ordered ? numbered : bullet).test(t)) {
          break;
        }
        items.push(inline(t.replace(ordered ? numbered : bullet, '')));
        i++;
      }
      const lis = items.map((item) => `<li>${item}</li>`).join('');
      html.push(ordered ? `<ol>${lis}</ol>` : `<ul>${lis}</ul>`);
      continue;
    }

    const paragraph: string[] = [];
    while (
      i < lines.length
      && lines[i].trim()
      && !/^(#{1,3}\s|[-*•]\s|\d+[.)]\s|\*\*[^*]+\*\*\s*$)/.test(lines[i].trim())
      && !/^\s{2,}[-*•]\s+/.test(lines[i])
    ) {
      paragraph.push(lines[i].trim());
      i++;
    }
    html.push(`<p>${inline(paragraph.join(' '))}</p>`);
  }

  return html.join('\n');
}

function inline(text: string): string {
  return escapeHtml(text).replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
}

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}
