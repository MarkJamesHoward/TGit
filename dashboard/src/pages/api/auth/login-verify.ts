import type { APIRoute } from "astro";
import { verifyAuthenticationResponse } from "@simplewebauthn/server";
import {
  getCredentialByCredentialId,
  updateCredentialCounter,
  getChallenge,
  getUser,
} from "../../../lib/db";
import { createUserSession } from "../../../lib/auth";

const rpID = process.env.WEBAUTHN_RP_ID || "localhost";
const origin = process.env.WEBAUTHN_ORIGIN || "http://localhost:4321";

export const POST: APIRoute = async ({ request }) => {
  try {
    const { credential, challengeKey } = await request.json();

    const expectedChallenge = await getChallenge(`auth_${challengeKey}`);
    if (!expectedChallenge) {
      return new Response(
        JSON.stringify({ error: "Challenge expired or not found" }),
        { status: 400 },
      );
    }

    // Find the credential in our database
    const storedCred = await getCredentialByCredentialId(credential.id);
    if (!storedCred) {
      return new Response(JSON.stringify({ error: "Credential not found" }), {
        status: 400,
      });
    }

    const publicKeyBytes = Buffer.from(storedCred.publicKey, "base64url");

    const verification = await verifyAuthenticationResponse({
      response: credential,
      expectedChallenge,
      expectedOrigin: origin,
      expectedRPID: rpID,
      credential: {
        id: storedCred.credentialId,
        publicKey: publicKeyBytes,
        counter: storedCred.counter,
        transports: storedCred.transports as
          | AuthenticatorTransport[]
          | undefined,
      },
    });

    if (!verification.verified) {
      return new Response(JSON.stringify({ error: "Verification failed" }), {
        status: 400,
      });
    }

    // Update counter
    await updateCredentialCounter(
      storedCred.credentialId,
      storedCred.userId,
      verification.authenticationInfo.newCounter,
    );

    // Create session
    const { cookie } = await createUserSession(storedCred.userId);

    // Get user for lastTenant
    const user = await getUser(storedCred.userId);

    return new Response(
      JSON.stringify({ verified: true, lastTenant: user?.lastTenant }),
      {
        headers: {
          "Content-Type": "application/json",
          "Set-Cookie": cookie,
        },
      },
    );
  } catch (error: any) {
    console.error("login-verify error:", error);
    return new Response(JSON.stringify({ error: error.message }), {
      status: 500,
    });
  }
};
