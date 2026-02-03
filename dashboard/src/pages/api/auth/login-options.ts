import type { APIRoute } from "astro";
import { generateAuthenticationOptions } from "@simplewebauthn/server";
import {
  getUserByDisplayName,
  getCredentialsByUserId,
  storeChallenge,
} from "../../../lib/db";

const rpID = process.env.WEBAUTHN_RP_ID || "localhost";

export const POST: APIRoute = async ({ request }) => {
  try {
    const body = await request.json();
    const { displayName } = body;

    let allowCredentials: any[] = [];
    let userId: string | null = null;

    if (displayName) {
      const user = await getUserByDisplayName(displayName.trim());
      if (!user) {
        return new Response(JSON.stringify({ error: "User not found" }), {
          status: 404,
        });
      }
      userId = user.id;
      const creds = await getCredentialsByUserId(user.id);
      allowCredentials = creds.map((c) => ({
        id: c.credentialId,
        transports: c.transports,
      }));
    }

    const options = await generateAuthenticationOptions({
      rpID,
      allowCredentials,
      userVerification: "preferred",
    });

    // Store challenge keyed by a temporary ID
    const challengeKey = userId || `anon_${crypto.randomUUID()}`;
    await storeChallenge(`auth_${challengeKey}`, options.challenge);

    return new Response(JSON.stringify({ options, challengeKey }), {
      headers: { "Content-Type": "application/json" },
    });
  } catch (error: any) {
    console.error("login-options error:", error);
    return new Response(JSON.stringify({ error: error.message }), {
      status: 500,
    });
  }
};
