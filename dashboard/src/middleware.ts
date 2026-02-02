import { defineMiddleware } from 'astro:middleware';
import { validateSession } from './lib/auth';

export const onRequest = defineMiddleware(async (context, next) => {
  // Always try to resolve the user session, but never block access
  const cookieHeader = context.request.headers.get('cookie');
  const user = await validateSession(cookieHeader);

  if (user) {
    context.locals.user = user;
  }

  return next();
});
