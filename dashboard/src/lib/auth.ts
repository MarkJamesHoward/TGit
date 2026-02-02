import type { AstroGlobal } from 'astro';
import { createSession, getSession, deleteSession, getUser, type PasskeyUser } from './db';

const SESSION_COOKIE = 'tgit_session';
const SESSION_DURATION_MS = 30 * 24 * 60 * 60 * 1000; // 30 days

function generateId(): string {
  const array = new Uint8Array(32);
  crypto.getRandomValues(array);
  return Array.from(array, b => b.toString(16).padStart(2, '0')).join('');
}

export async function createUserSession(userId: string): Promise<{ sessionId: string; cookie: string }> {
  const sessionId = generateId();
  const expiresAt = new Date(Date.now() + SESSION_DURATION_MS).toISOString();

  await createSession({
    id: sessionId,
    userId,
    expiresAt
  });

  const cookie = `${SESSION_COOKIE}=${sessionId}; Path=/; HttpOnly; SameSite=Lax; Max-Age=${SESSION_DURATION_MS / 1000}`;
  return { sessionId, cookie };
}

export async function validateSession(cookieHeader: string | null): Promise<PasskeyUser | null> {
  if (!cookieHeader) return null;

  const match = cookieHeader.match(new RegExp(`${SESSION_COOKIE}=([^;]+)`));
  if (!match) return null;

  const sessionId = match[1];
  const session = await getSession(sessionId);
  if (!session) return null;

  const user = await getUser(session.userId);
  return user;
}

export async function destroySession(cookieHeader: string | null): Promise<string> {
  if (cookieHeader) {
    const match = cookieHeader.match(new RegExp(`${SESSION_COOKIE}=([^;]+)`));
    if (match) {
      await deleteSession(match[1]);
    }
  }
  return `${SESSION_COOKIE}=; Path=/; HttpOnly; SameSite=Lax; Max-Age=0`;
}

export function getSessionIdFromCookie(cookieHeader: string | null): string | null {
  if (!cookieHeader) return null;
  const match = cookieHeader.match(new RegExp(`${SESSION_COOKIE}=([^;]+)`));
  return match ? match[1] : null;
}
