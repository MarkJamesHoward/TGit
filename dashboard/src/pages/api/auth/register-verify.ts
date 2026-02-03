import type { APIRoute } from "astro";
import { verifyRegistrationResponse } from "@simplewebauthn/server";
import {
  createUser,
  getUser,
  createCredential,
  getChallenge,
} from "../../../lib/db";
import { createUserSession } from "../../../lib/auth";

const rpID = process.env.WEBAUTHN_RP_ID || "localhost";
const origin = process.env.WEBAUTHN_ORIGIN || "http://localhost:4321";

export const POST: APIRoute = async ({ request }) => {
  try {
    const { credential, userId, displayName } = await request.json();

    const expectedChallenge = await getChallenge(`reg_${userId}`);
    if (!expectedChallenge) {
      return new Response(
        JSON.stringify({ error: "Challenge expired or not found" }),
        { status: 400 },
      );
    }

    const verification = await verifyRegistrationResponse({
      response: credential,
      expectedChallenge,
      expectedOrigin: origin,
      expectedRPID: rpID,
    });

    if (!verification.verified || !verification.registrationInfo) {
      return new Response(JSON.stringify({ error: "Verification failed" }), {
        status: 400,
      });
    }

    const {
      credential: regCred,
      credentialDeviceType,
      credentialBackedUp,
    } = verification.registrationInfo;

    // Create user if doesn't exist
    const existingUser = await getUser(userId);
    if (!existingUser) {
      await createUser({
        id: userId,
        displayName: displayName.trim(),
        createdAt: new Date().toISOString(),
      });
    }

    // Store credential
    // Convert Uint8Array to base64url string for storage
    const publicKeyBase64 = Buffer.from(regCred.publicKey).toString(
      "base64url",
    );

    await createCredential({
      id: crypto.randomUUID(),
      credentialId: regCred.id,
      userId,
      publicKey: publicKeyBase64,
      counter: regCred.counter,
      transports: credential.response.transports,
      createdAt: new Date().toISOString(),
    });

    // Create session
    const { cookie } = await createUserSession(userId);

    return new Response(JSON.stringify({ verified: true }), {
      headers: {
        "Content-Type": "application/json",
        "Set-Cookie": cookie,
      },
    });
  } catch (error: any) {
    console.error("register-verify error:", error);
    return new Response(JSON.stringify({ error: error.message }), {
      status: 500,
    });
  }
};
