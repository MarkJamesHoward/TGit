import { defineMiddleware } from "astro:middleware";
import { validateSession } from "./lib/auth";
import { getLastTenantForUser } from "./lib/tenantStore";

export const onRequest = defineMiddleware(async (context, next) => {
  // Always try to resolve the user session, but never block access
  const cookieHeader = context.request.headers.get("cookie");
  const user = await validateSession(cookieHeader);

  if (user) {
    try {
      const lastTenant = await getLastTenantForUser(user.id);
      if (lastTenant) {
        (user as { lastTenant?: string }).lastTenant = lastTenant;
      }
    } catch (error) {
      console.warn("Failed to load tenant preference:", error);
    }
    context.locals.user = user;
  }

  return next();
});
