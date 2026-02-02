import type { APIRoute } from 'astro';
import { destroySession } from '../../../lib/auth';

export const POST: APIRoute = async ({ request }) => {
  const cookie = await destroySession(request.headers.get('cookie'));
  return new Response(JSON.stringify({ ok: true }), {
    headers: {
      'Content-Type': 'application/json',
      'Set-Cookie': cookie,
    },
  });
};
