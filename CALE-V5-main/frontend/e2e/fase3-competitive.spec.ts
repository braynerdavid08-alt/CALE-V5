import { Browser, BrowserContext, expect, Page, test } from '@playwright/test';
import * as fs from 'node:fs';
import * as path from 'node:path';

/**
 * Fase 3 — validación real en navegador (Chromium).
 * Login por `context.request` contra el proxy Angular para que Playwright
 * conserve cookies `cale_access`/`cale_refresh` en cada browser independiente.
 */
const API = process.env['E2E_API_URL'] || 'http://127.0.0.1:5000';
const ORIGIN = process.env['E2E_BASE_URL'] || 'http://127.0.0.1:4200';
/** Lab password from env only — never commit real secrets. See tools/SeedFase3Competitive/README.md */
const PASSWORD = process.env['FASE3_LAB_PASSWORD'] || '';
const TEACHER = {
  email: process.env['E2E_TEACHER_EMAIL'] || 'profesor@cale.local',
  password: PASSWORD
};
const DEFAULT_STUDENTS = [
  'estudiante@cale.local',
  'estudiante2.fase3@cale.local',
  'estudiante3.fase3@cale.local',
  'estudiante4.fase3@cale.local'
];
const STUDENTS = (process.env['E2E_STUDENT_EMAILS'] || DEFAULT_STUDENTS.join(','))
  .split(',')
  .map((e) => e.trim())
  .filter(Boolean)
  .map((email) => ({ email, password: PASSWORD }));

const EVIDENCE_DIR = path.resolve(process.cwd(), '..', 'docs', 'competitive', 'fase3-evidencias');

test.describe.configure({ mode: 'serial' });
test.setTimeout(8 * 60_000);

type ApiSession = { bearer: string; userId: number; email: string };

function ensureEvidenceDir() {
  fs.mkdirSync(EVIDENCE_DIR, { recursive: true });
}

async function shot(page: Page, name: string) {
  ensureEvidenceDir();
  const file = path.join(EVIDENCE_DIR, `${name}.png`);
  await page.screenshot({ path: file, fullPage: true });
}

function extractBearer(setCookies: string[]): string | null {
  for (const raw of setCookies) {
    const m = raw.match(/^\s*cale_access=([^;]+)/);
    if (m) return decodeURIComponent(m[1]);
  }
  return null;
}

async function authedContext(
  browser: Browser,
  email: string,
  password: string
): Promise<{ ctx: BrowserContext; session: ApiSession }> {
  const ctx = await browser.newContext({
    baseURL: ORIGIN,
    viewport: { width: 1366, height: 768 }
  });
  const res = await ctx.request.post(`${ORIGIN}/api/auth/login`, {
    data: { email, password },
    headers: { 'Content-Type': 'application/json' }
  });
  if (!res.ok()) {
    throw new Error(`Proxy login ${email} => ${res.status()} ${await res.text()}`);
  }
  const body = (await res.json()) as {
    userId: number;
    name: string;
    email: string;
    role: string;
    mustChangePassword?: boolean;
    usesCookieAuth?: boolean;
  };
  const headers = res.headersArray().filter((h) => h.name.toLowerCase() === 'set-cookie').map((h) => h.value);
  let bearer = extractBearer(headers);
  if (!bearer) {
    const cookies = await ctx.cookies();
    bearer = cookies.find((c) => c.name === 'cale_access')?.value ?? null;
  }
  if (!bearer) throw new Error(`No cale_access for ${email}`);

  // Cookie alone is not enough: SessionStore.restore() needs cale.session.v5.
  const storagePayload = JSON.stringify({
    user: {
      id: body.userId,
      name: body.name,
      email: body.email,
      role: body.role,
      mustChangePassword: !!body.mustChangePassword,
      schoolId: body.role === 'Teacher' || body.role === 'Student' ? 2 : null,
      isMembershipActive: true,
      planLabel: 'Mensual',
      freeAccess: true,
      photoUrl: null
    },
    cookieAuth: true,
    token: null
  });
  await ctx.addInitScript((raw) => {
    localStorage.setItem('cale.session.v5', raw);
    sessionStorage.setItem('cale.session.v5', raw);
  }, storagePayload);

  return {
    ctx,
    session: { bearer, userId: body.userId, email }
  };
}

async function apiJson<T>(session: ApiSession, method: string, url: string, body?: unknown): Promise<T> {
  let lastErr = '';
  for (let attempt = 0; attempt < 8; attempt++) {
    const res = await fetch(`${API}${url}`, {
      method,
      headers: {
        'Content-Type': 'application/json',
        Cookie: `cale_access=${session.bearer}`,
        Authorization: `Bearer ${session.bearer}`
      },
      body: body === undefined ? undefined : JSON.stringify(body)
    });
    if (res.ok) {
      if (res.status === 204) return undefined as T;
      return (await res.json()) as T;
    }
    lastErr = `${method} ${url} => ${res.status} ${await res.text()}`;
    // SQLite local can briefly lock under concurrent browser+API writes.
    if (res.status === 500 && /database table is locked|SQLITE_BUSY/i.test(lastErr)) {
      await new Promise((r) => setTimeout(r, 400 * (attempt + 1)));
      continue;
    }
    throw new Error(lastErr);
  }
  throw new Error(lastErr);
}

async function playMatchViaApi(session: ApiSession, matchId: number) {
  for (let i = 0; i < 24; i++) {
    const view = await apiJson<{
      status: string;
      currentMatchQuestionId: number | null;
      currentQuestion: { options: { id: number }[] } | null;
    }>(session, 'GET', `/api/tournaments/matches/${matchId}`);
    if (view.status === 'finished' || view.status === 'bye') return;
    const mqId = view.currentMatchQuestionId;
    const options = view.currentQuestion?.options ?? [];
    if (!mqId || !options.length) {
      await new Promise((r) => setTimeout(r, 300));
      continue;
    }
    await apiJson(session, 'POST', `/api/tournaments/matches/${matchId}/answer`, {
      matchQuestionId: mqId,
      optionId: options[0].id
    });
    await new Promise((r) => setTimeout(r, 150));
  }
}

test('Fase 3 browser + SignalR + responsive', async ({ browser }) => {
  test.skip(
    !PASSWORD || PASSWORD.length < 8 || STUDENTS.length < 4,
    'Set FASE3_LAB_PASSWORD (and optionally E2E_STUDENT_EMAILS with 4 lab emails). See tools/SeedFase3Competitive/README.md'
  );

  ensureEvidenceDir();
  fs.writeFileSync(
    path.join(EVIDENCE_DIR, 'README.md'),
    [
      '# Evidencias Fase 3',
      '',
      'Capturas Playwright (Chromium) vs API SQLite local + `ng serve`.',
      'Dos contextos de navegador independientes (Student 1 / Student 2) para SignalR.',
      `Generado: ${new Date().toISOString()}`,
      ''
    ].join('\n')
  );

  const teacherAuth = await authedContext(browser, TEACHER.email, TEACHER.password);
  await new Promise((r) => setTimeout(r, 500));
  const studentAuths = [] as { ctx: BrowserContext; session: ApiSession }[];
  for (const s of STUDENTS) {
    studentAuths.push(await authedContext(browser, s.email, s.password));
    await new Promise((r) => setTimeout(r, 400));
  }

  const teacher = await teacherAuth.ctx.newPage();
  teacher.setDefaultTimeout(30_000);

  await teacher.goto('/teacher/tournaments');
  await expect(teacher.getByText(/Torneos Luz Verde/i).first()).toBeVisible({ timeout: 45_000 });
  await shot(teacher, '02-teacher-listado');

  await teacher.getByRole('link', { name: /Crear torneo/i }).click();
  await teacher.locator('#t-name').fill(`Fase3 Copa ${Date.now()}`);
  await teacher.locator('#t-desc').fill('Validación Fase 3 local');
  await teacher.locator('#t-cap').selectOption({ label: '4 jugadores' });
  await teacher.locator('#t-secs').fill('10');
  await shot(teacher, '01-teacher-crear-torneo');
  await teacher.getByRole('button', { name: /Crear torneo/i }).click();
  await teacher.waitForURL(/\/teacher\/tournaments\/\d+/, { timeout: 45_000 });
  const tournamentId = Number(teacher.url().match(/\/teacher\/tournaments\/(\d+)/)?.[1]);
  expect(tournamentId).toBeGreaterThan(0);
  await shot(teacher, '01b-teacher-lobby');

  for (const auth of studentAuths) {
    await apiJson(auth.session, 'POST', `/api/tournaments/${tournamentId}/join`);
    await new Promise((r) => setTimeout(r, 500));
  }

  const s1 = await studentAuths[0].ctx.newPage();
  const s2 = await studentAuths[1].ctx.newPage();
  s1.setDefaultTimeout(30_000);
  s2.setDefaultTimeout(30_000);

  await s1.goto(`/student/play/tournaments/${tournamentId}`);
  await expect(s1.getByText(/Participantes|inscritos|Estás inscrito/i).first()).toBeVisible({
    timeout: 45_000
  });
  await shot(s1, '03-student-lobby');

  await s2.goto(`/student/play/tournaments/${tournamentId}`);
  await expect(s2.getByText(/4\/4|Participantes \(4\)|4 inscritos/i).first()).toBeVisible({
    timeout: 45_000
  });
  await shot(s1, '04-dos-estudiantes-conectados-a');
  await shot(s2, '04-dos-estudiantes-conectados-b');
  await expect(s1.getByRole('button', { name: /Iniciar torneo/i })).toHaveCount(0);

  await teacher.goto(`/teacher/tournaments/${tournamentId}`);
  await shot(teacher, '01c-teacher-listo-para-iniciar');
  // Start via API to avoid SQLite lock races with live SignalR UI polls.
  await apiJson(teacherAuth.session, 'POST', `/api/tournaments/${tournamentId}/start`);
  await teacher.reload();
  await expect(teacher.getByText(/En juego|Ver llaves|Ronda|inscritos/i).first()).toBeVisible({
    timeout: 45_000
  });

  type BracketMatch = {
    id: number;
    roundNumber: number;
    status: string;
    player1UserId: number | null;
    player2UserId: number | null;
  };
  type Bracket = {
    championUserId?: number | null;
    matches: BracketMatch[];
  };

  const bracket = await apiJson<Bracket>(
    teacherAuth.session,
    'GET',
    `/api/tournaments/${tournamentId}/bracket`
  );

  await teacher.goto(`/teacher/tournaments/${tournamentId}/bracket`);
  await shot(teacher, '06-bracket-tras-inicio');

  const firstRound = Math.min(...(bracket.matches.map((m) => m.roundNumber) || [1]));
  const openMatches = bracket.matches.filter(
    (m) => m.roundNumber === firstRound && m.player1UserId && m.player2UserId
  );
  expect(openMatches.length).toBeGreaterThan(0);
  const matchFor = (userId: number) =>
    openMatches.find((m) => m.player1UserId === userId || m.player2UserId === userId);
  const m1 = matchFor(studentAuths[0].session.userId);
  const m2 = matchFor(studentAuths[1].session.userId);
  expect(m1?.id).toBeTruthy();

  await s1.goto(`/student/play/tournaments/match/${m1!.id}`);
  await expect(s1.getByText(/aciertos|VS|Enfrentamiento|pregunta/i).first()).toBeVisible({
    timeout: 45_000
  });
  await shot(s1, '05-partida-en-ejecucion-s1');
  await s2.goto(`/student/play/tournaments/match/${(m2 ?? m1)!.id}`);
  await shot(s2, '05-partida-en-ejecucion-s2');

  await studentAuths[1].ctx.setOffline(true);
  await s2.waitForTimeout(1000);
  await studentAuths[1].ctx.setOffline(false);
  await s2.reload();
  await expect(s2.locator('body')).toBeVisible();
  await shot(s2, '05b-reconexion-s2');

  const sessionByUser = new Map(studentAuths.map((a) => [a.session.userId, a.session]));
  for (let guard = 0; guard < 50; guard++) {
    const b = await apiJson<Bracket>(
      teacherAuth.session,
      'GET',
      `/api/tournaments/${tournamentId}/bracket`
    );
    if (b.championUserId) break;
    const playable = b.matches.filter((m) => m.status === 'active' || m.status === 'ready');
    if (!playable.length) {
      await new Promise((r) => setTimeout(r, 350));
      continue;
    }
    for (const match of playable) {
      for (const uid of [match.player1UserId, match.player2UserId]) {
        if (!uid) continue;
        const session = sessionByUser.get(uid);
        if (session) await playMatchViaApi(session, match.id).catch(() => undefined);
      }
    }
  }

  await teacher.goto(`/teacher/tournaments/${tournamentId}/bracket`);
  await shot(teacher, '06-bracket-actualizado');
  await s1.goto(`/student/play/tournaments/${tournamentId}`);
  await shot(s1, '07-pantalla-campeon-o-lobby');
  await s1.goto('/student/play/tournaments/profile');
  await expect(s1.getByText(/perfil competitivo|Trofeos|Nivel/i).first()).toBeVisible();
  await shot(s1, '08-trofeo-logros');
  await shot(s1, '10-perfil-competitivo');
  await s1.goto('/student/play/ranking');
  await expect(s1.getByText(/Ranking|clasificación|XP|general/i).first()).toBeVisible();
  await shot(s1, '09-ranking');

  for (const width of [1366, 768, 480, 375]) {
    await s1.setViewportSize({ width, height: width >= 768 ? 900 : 812 });
    await s1.goto('/student/play/tournaments');
    await shot(s1, `11-responsive-list-${width}`);
    await s1.goto(`/student/play/tournaments/${tournamentId}`);
    await shot(s1, `11-responsive-lobby-${width}`);
    await s1.goto(`/student/play/tournaments/${tournamentId}/bracket`);
    await shot(s1, `11-responsive-bracket-${width}`);
    await s1.goto('/student/play/ranking');
    await shot(s1, `11-responsive-ranking-${width}`);
    await s1.goto('/student/play/tournaments/profile');
    await shot(s1, `11-responsive-profile-${width}`);
  }
  await shot(s1, '11-vista-movil');

  const overflow = await s1.evaluate(
    () => document.documentElement.scrollWidth > document.documentElement.clientWidth + 2
  );
  expect(overflow).toBe(false);

  await teacherAuth.ctx.close();
  for (const a of studentAuths) await a.ctx.close();
});
