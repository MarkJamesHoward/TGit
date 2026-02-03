import type { APIRoute } from "astro";
import { validateSession } from "../../../lib/auth";
import { setLastTenantForUser } from "../../../lib/tenantStore";

export const POST: APIRoute = async ({ request }) => {
  const user = await validateSession(request.headers.get("cookie"));
  if (!user) {
    return new Response(JSON.stringify({ error: "Unauthorized" }), {
      status: 401,
    });
  }

  const { tenant } = await request.json();
  if (!tenant || typeof tenant !== "string") {
    return new Response(JSON.stringify({ error: "tenant is required" }), {
      status: 400,
    });
  }

  await setLastTenantForUser(user.id, tenant.trim().toLowerCase());

  return new Response(JSON.stringify({ ok: true }), {
    headers: { "Content-Type": "application/json" },
  });
};
