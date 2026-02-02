import { CosmosClient, type Container } from '@azure/cosmos';

const endpoint = import.meta.env.COSMOS_ENDPOINT || process.env.COSMOS_ENDPOINT || '';
const key = import.meta.env.COSMOS_KEY || process.env.COSMOS_KEY || '';
const databaseId = import.meta.env.COSMOS_DATABASE || process.env.COSMOS_DATABASE || 'tgit-dashboard';

let client: CosmosClient | null = null;
let initialized = false;

function getClient(): CosmosClient {
  if (!client) {
    if (!endpoint || !key) {
      throw new Error('COSMOS_ENDPOINT and COSMOS_KEY environment variables are required');
    }
    client = new CosmosClient({ endpoint, key });
  }
  return client;
}

async function ensureInitialized(): Promise<void> {
  if (initialized) return;
  const c = getClient();
  const { database } = await c.databases.createIfNotExists({ id: databaseId });
  await database.containers.createIfNotExists({ id: 'passkey_users', partitionKey: '/id' });
  await database.containers.createIfNotExists({ id: 'passkey_credentials', partitionKey: '/userId' });
  await database.containers.createIfNotExists({ id: 'passkey_sessions', partitionKey: '/id' });
  await database.containers.createIfNotExists({ id: 'passkey_challenges', partitionKey: '/id' });
  initialized = true;
}

function container(name: string): Container {
  return getClient().database(databaseId).container(name);
}

export interface PasskeyUser {
  id: string;
  displayName: string;
  lastTenant?: string;
  createdAt: string;
}

export interface PasskeyCredential {
  id: string;
  credentialId: string;
  userId: string;
  publicKey: string; // base64url encoded
  counter: number;
  transports?: string[];
  createdAt: string;
}

export interface PasskeySession {
  id: string;
  userId: string;
  expiresAt: string;
}

// Users
export async function createUser(user: PasskeyUser): Promise<void> {
  await ensureInitialized();
  await container('passkey_users').items.create(user);
}

export async function getUser(id: string): Promise<PasskeyUser | null> {
  await ensureInitialized();
  try {
    const { resource } = await container('passkey_users').item(id, id).read<PasskeyUser>();
    return resource || null;
  } catch (e: any) {
    if (e.code === 404) return null;
    throw e;
  }
}

export async function getUserByDisplayName(displayName: string): Promise<PasskeyUser | null> {
  await ensureInitialized();
  const { resources } = await container('passkey_users').items
    .query({ query: 'SELECT * FROM c WHERE c.displayName = @name', parameters: [{ name: '@name', value: displayName }] })
    .fetchAll();
  return resources[0] || null;
}

export async function updateUserLastTenant(userId: string, tenant: string): Promise<void> {
  await ensureInitialized();
  const user = await getUser(userId);
  if (user) {
    await container('passkey_users').item(userId, userId).replace({ ...user, lastTenant: tenant });
  }
}

// Credentials
export async function createCredential(cred: PasskeyCredential): Promise<void> {
  await ensureInitialized();
  await container('passkey_credentials').items.create(cred);
}

export async function getCredentialsByUserId(userId: string): Promise<PasskeyCredential[]> {
  await ensureInitialized();
  const { resources } = await container('passkey_credentials').items
    .query({ query: 'SELECT * FROM c WHERE c.userId = @uid', parameters: [{ name: '@uid', value: userId }] })
    .fetchAll();
  return resources;
}

export async function getCredentialByCredentialId(credentialId: string): Promise<PasskeyCredential | null> {
  await ensureInitialized();
  const { resources } = await container('passkey_credentials').items
    .query({ query: 'SELECT * FROM c WHERE c.credentialId = @cid', parameters: [{ name: '@cid', value: credentialId }] })
    .fetchAll();
  return resources[0] || null;
}

export async function updateCredentialCounter(credentialId: string, userId: string, counter: number): Promise<void> {
  await ensureInitialized();
  const cred = await getCredentialByCredentialId(credentialId);
  if (cred) {
    await container('passkey_credentials').item(cred.id, cred.userId).replace({ ...cred, counter });
  }
}

// Sessions
export async function createSession(session: PasskeySession): Promise<void> {
  await ensureInitialized();
  await container('passkey_sessions').items.create(session);
}

export async function getSession(id: string): Promise<PasskeySession | null> {
  await ensureInitialized();
  try {
    const { resource } = await container('passkey_sessions').item(id, id).read<PasskeySession>();
    if (!resource) return null;
    if (new Date(resource.expiresAt) < new Date()) {
      await deleteSession(id);
      return null;
    }
    return resource;
  } catch (e: any) {
    if (e.code === 404) return null;
    throw e;
  }
}

export async function deleteSession(id: string): Promise<void> {
  await ensureInitialized();
  try {
    await container('passkey_sessions').item(id, id).delete();
  } catch (e: any) {
    if (e.code === 404) return;
    throw e;
  }
}

// Challenges (temporary storage for WebAuthn ceremonies)
export async function storeChallenge(id: string, challenge: string): Promise<void> {
  await ensureInitialized();
  await container('passkey_challenges').items.upsert({
    id,
    challenge,
    expiresAt: new Date(Date.now() + 5 * 60 * 1000).toISOString()
  });
}

export async function getChallenge(id: string): Promise<string | null> {
  await ensureInitialized();
  try {
    const { resource } = await container('passkey_challenges').item(id, id).read<any>();
    if (!resource) return null;
    // Clean up after reading
    await container('passkey_challenges').item(id, id).delete();
    if (new Date(resource.expiresAt) < new Date()) return null;
    return resource.challenge;
  } catch (e: any) {
    if (e.code === 404) return null;
    throw e;
  }
}

// List all users (for discovery during login)
export async function getAllUsers(): Promise<PasskeyUser[]> {
  await ensureInitialized();
  const { resources } = await container('passkey_users').items
    .query('SELECT * FROM c')
    .fetchAll();
  return resources;
}
