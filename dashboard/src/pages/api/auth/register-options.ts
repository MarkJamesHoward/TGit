import type { APIRoute } from "astro";
import { generateRegistrationOptions } from "@simplewebauthn/server";
import {
  getUserByDisplayName,
  getCredentialsByUserId,
  storeChallenge,
} from "../../../lib/db";

const rpName = process.env.WEBAUTHN_RP_NAME || "TGit Dashboard";
const rpID = process.env.WEBAUTHN_RP_ID || "localhost";

export const POST: APIRoute = async ({ request }) => {
  try {
    const { displayName } = await request.json();
    if (!displayName || typeof displayName !== "string") {
      return new Response(
        JSON.stringify({ error: "displayName is required" }),
        { status: 400 },
      );
    }

    const trimmed = displayName.trim();
    const existingUser = await getUserByDisplayName(trimmed);

    // Get existing credentials to exclude
    const excludeCredentials = existingUser
      ? (await getCredentialsByUserId(existingUser.id)).map((c) => ({
          id: c.credentialId,
          transports: c.transports as AuthenticatorTransport[] | undefined,
        }))
      : [];

    const userId = existingUser?.id || crypto.randomUUID();

    const options = await generateRegistrationOptions({
      rpName,
      rpID,
      userName: trimmed,
      userID: new TextEncoder().encode(userId),
      attestationType: "none",
      excludeCredentials,
      authenticatorSelection: {
        residentKey: "preferred",
        userVerification: "preferred",
      },
    });

    // Store challenge for verification
    await storeChallenge(`reg_${userId}`, options.challenge);

    return new Response(JSON.stringify({ options, userId }), {
      headers: { "Content-Type": "application/json" },
    });
  } catch (error: any) {
    console.error("register-options error:", error);
    return new Response(JSON.stringify({ error: error.message }), {
      status: 500,
    });
  }
};
