import { renderAssistantMarkdown } from './assistant-markdown';

describe('renderAssistantMarkdown', () => {
  it('escapes html before formatting', () => {
    const html = renderAssistantMarkdown('Hola <script>alert(1)</script>\n\n- uno');
    expect(html).not.toContain('<script>');
    expect(html).toContain('&lt;script&gt;');
    expect(html).toContain('<ul>');
    expect(html).toContain('<li>uno</li>');
  });

  it('renders bold titles and nested bullets for agenda-like replies', () => {
    const source = [
      '**Clases programadas**',
      '',
      '**viernes, 30 de octubre**',
      '- **18:00 – 20:00** · Clase teórica',
      '  - Aula: 1',
      '  - Ocupación: 0 de 20 ocupados'
    ].join('\n');

    const html = renderAssistantMarkdown(source);
    expect(html).toContain('asst-title');
    expect(html).toContain('<strong>18:00 – 20:00</strong>');
    expect(html).toContain('asst-sub');
    expect(html).toContain('Ocupación: 0 de 20 ocupados');
  });

  it('keeps short replies as a single paragraph', () => {
    const html = renderAssistantMarkdown('Te faltan 4 horas de teoría.');
    expect(html).toBe('<p>Te faltan 4 horas de teoría.</p>');
  });
});
