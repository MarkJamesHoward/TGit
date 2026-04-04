import sql from "mssql";

let pool: sql.ConnectionPool | null = null;
let initialized = false;

async function getPool(): Promise<sql.ConnectionPool> {
  if (!pool) {
    const connectionString = process.env.SQL_CONNECTION_STRING;
    if (!connectionString) {
      throw new Error("SQL_CONNECTION_STRING environment variable is required");
    }

    const config = parseConnectionString(connectionString);
    pool = await sql.connect(config);
  }
  return pool;
}

function parseConnectionString(connStr: string): sql.config {
  const parts: Record<string, string> = {};
  for (const part of connStr.split(";")) {
    const [key, ...valueParts] = part.split("=");
    if (key && valueParts.length > 0) {
      parts[key.trim().toLowerCase()] = valueParts.join("=").trim();
    }
  }

  const useEntraAuth =
    parts["authentication"]?.includes("Active Directory") ?? false;

  const config: sql.config = {
    server: parts["server"] || parts["data source"] || "",
    database: parts["database"] || parts["initial catalog"] || "",
    options: {
      encrypt: parts["encrypt"]?.toLowerCase() !== "false",
      trustServerCertificate:
        parts["trustservercertificate"]?.toLowerCase() === "true",
    },
  };

  if (useEntraAuth) {
    config.authentication = {
      type: "azure-active-directory-default",
      options: {
        clientId: "",
      },
    };
  } else if (parts["user id"] || parts["user"]) {
    config.user = parts["user id"] || parts["user"];
    config.password = parts["password"] || "";
  }

  return config;
}

async function ensureInitialized(): Promise<void> {
  if (initialized) return;
  const p = await getPool();

  await p.request().query(`
    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='PasskeyUsers' AND xtype='U')
    CREATE TABLE PasskeyUsers (
      Id NVARCHAR(450) NOT NULL PRIMARY KEY,
      DisplayName NVARCHAR(256) NOT NULL,
      LastTenant NVARCHAR(128) NULL,
      CreatedAt NVARCHAR(64) NOT NULL
    )
  `);

  await p.request().query(`
    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='PasskeyCredentials' AND xtype='U')
    CREATE TABLE PasskeyCredentials (
      Id NVARCHAR(450) NOT NULL PRIMARY KEY,
      CredentialId NVARCHAR(1024) NOT NULL,
      UserId NVARCHAR(450) NOT NULL,
      PublicKey NVARCHAR(MAX) NOT NULL,
      Counter INT NOT NULL DEFAULT 0,
      Transports NVARCHAR(MAX) NULL,
      CreatedAt NVARCHAR(64) NOT NULL,
      CONSTRAINT FK_Credentials_Users FOREIGN KEY (UserId) REFERENCES PasskeyUsers(Id) ON DELETE CASCADE
    )
  `);

  await p.request().query(`
    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='PasskeySessions' AND xtype='U')
    CREATE TABLE PasskeySessions (
      Id NVARCHAR(450) NOT NULL PRIMARY KEY,
      UserId NVARCHAR(450) NOT NULL,
      ExpiresAt NVARCHAR(64) NOT NULL
    )
  `);

  await p.request().query(`
    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='PasskeyChallenges' AND xtype='U')
    CREATE TABLE PasskeyChallenges (
      Id NVARCHAR(450) NOT NULL PRIMARY KEY,
      Challenge NVARCHAR(MAX) NOT NULL,
      ExpiresAt NVARCHAR(64) NOT NULL
    )
  `);

  initialized = true;
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
  const p = await getPool();
  await p
    .request()
    .input("id", sql.NVarChar, user.id)
    .input("displayName", sql.NVarChar, user.displayName)
    .input("lastTenant", sql.NVarChar, user.lastTenant || null)
    .input("createdAt", sql.NVarChar, user.createdAt)
    .query(
      "INSERT INTO PasskeyUsers (Id, DisplayName, LastTenant, CreatedAt) VALUES (@id, @displayName, @lastTenant, @createdAt)",
    );
}

export async function getUser(id: string): Promise<PasskeyUser | null> {
  await ensureInitialized();
  const p = await getPool();
  const result = await p
    .request()
    .input("id", sql.NVarChar, id)
    .query<{
      Id: string;
      DisplayName: string;
      LastTenant: string | null;
      CreatedAt: string;
    }>(
      "SELECT Id, DisplayName, LastTenant, CreatedAt FROM PasskeyUsers WHERE Id = @id",
    );
  const row = result.recordset[0];
  if (!row) return null;
  return {
    id: row.Id,
    displayName: row.DisplayName,
    lastTenant: row.LastTenant || undefined,
    createdAt: row.CreatedAt,
  };
}

export async function getUserByDisplayName(
  displayName: string,
): Promise<PasskeyUser | null> {
  await ensureInitialized();
  const p = await getPool();
  const result = await p
    .request()
    .input("displayName", sql.NVarChar, displayName)
    .query<{
      Id: string;
      DisplayName: string;
      LastTenant: string | null;
      CreatedAt: string;
    }>(
      "SELECT Id, DisplayName, LastTenant, CreatedAt FROM PasskeyUsers WHERE DisplayName = @displayName",
    );
  const row = result.recordset[0];
  if (!row) return null;
  return {
    id: row.Id,
    displayName: row.DisplayName,
    lastTenant: row.LastTenant || undefined,
    createdAt: row.CreatedAt,
  };
}

export async function updateUserLastTenant(
  userId: string,
  tenant: string,
): Promise<void> {
  await ensureInitialized();
  const p = await getPool();
  await p
    .request()
    .input("userId", sql.NVarChar, userId)
    .input("lastTenant", sql.NVarChar, tenant)
    .query(
      "UPDATE PasskeyUsers SET LastTenant = @lastTenant WHERE Id = @userId",
    );
}

// Credentials
export async function createCredential(
  cred: PasskeyCredential,
): Promise<void> {
  await ensureInitialized();
  const p = await getPool();
  await p
    .request()
    .input("id", sql.NVarChar, cred.id)
    .input("credentialId", sql.NVarChar, cred.credentialId)
    .input("userId", sql.NVarChar, cred.userId)
    .input("publicKey", sql.NVarChar, cred.publicKey)
    .input("counter", sql.Int, cred.counter)
    .input(
      "transports",
      sql.NVarChar,
      cred.transports ? JSON.stringify(cred.transports) : null,
    )
    .input("createdAt", sql.NVarChar, cred.createdAt)
    .query(
      "INSERT INTO PasskeyCredentials (Id, CredentialId, UserId, PublicKey, Counter, Transports, CreatedAt) VALUES (@id, @credentialId, @userId, @publicKey, @counter, @transports, @createdAt)",
    );
}

export async function getCredentialsByUserId(
  userId: string,
): Promise<PasskeyCredential[]> {
  await ensureInitialized();
  const p = await getPool();
  const result = await p
    .request()
    .input("userId", sql.NVarChar, userId)
    .query<{
      Id: string;
      CredentialId: string;
      UserId: string;
      PublicKey: string;
      Counter: number;
      Transports: string | null;
      CreatedAt: string;
    }>(
      "SELECT Id, CredentialId, UserId, PublicKey, Counter, Transports, CreatedAt FROM PasskeyCredentials WHERE UserId = @userId",
    );
  return result.recordset.map(mapCredential);
}

export async function getCredentialByCredentialId(
  credentialId: string,
): Promise<PasskeyCredential | null> {
  await ensureInitialized();
  const p = await getPool();
  const result = await p
    .request()
    .input("credentialId", sql.NVarChar, credentialId)
    .query<{
      Id: string;
      CredentialId: string;
      UserId: string;
      PublicKey: string;
      Counter: number;
      Transports: string | null;
      CreatedAt: string;
    }>(
      "SELECT Id, CredentialId, UserId, PublicKey, Counter, Transports, CreatedAt FROM PasskeyCredentials WHERE CredentialId = @credentialId",
    );
  const row = result.recordset[0];
  if (!row) return null;
  return mapCredential(row);
}

export async function updateCredentialCounter(
  credentialId: string,
  _userId: string,
  counter: number,
): Promise<void> {
  await ensureInitialized();
  const p = await getPool();
  await p
    .request()
    .input("credentialId", sql.NVarChar, credentialId)
    .input("counter", sql.Int, counter)
    .query(
      "UPDATE PasskeyCredentials SET Counter = @counter WHERE CredentialId = @credentialId",
    );
}

// Sessions
export async function createSession(session: PasskeySession): Promise<void> {
  await ensureInitialized();
  const p = await getPool();
  await p
    .request()
    .input("id", sql.NVarChar, session.id)
    .input("userId", sql.NVarChar, session.userId)
    .input("expiresAt", sql.NVarChar, session.expiresAt)
    .query(
      "INSERT INTO PasskeySessions (Id, UserId, ExpiresAt) VALUES (@id, @userId, @expiresAt)",
    );
}

export async function getSession(
  id: string,
): Promise<PasskeySession | null> {
  await ensureInitialized();
  const p = await getPool();
  const result = await p
    .request()
    .input("id", sql.NVarChar, id)
    .query<{ Id: string; UserId: string; ExpiresAt: string }>(
      "SELECT Id, UserId, ExpiresAt FROM PasskeySessions WHERE Id = @id",
    );
  const row = result.recordset[0];
  if (!row) return null;
  if (new Date(row.ExpiresAt) < new Date()) {
    await deleteSession(id);
    return null;
  }
  return { id: row.Id, userId: row.UserId, expiresAt: row.ExpiresAt };
}

export async function deleteSession(id: string): Promise<void> {
  await ensureInitialized();
  const p = await getPool();
  await p
    .request()
    .input("id", sql.NVarChar, id)
    .query("DELETE FROM PasskeySessions WHERE Id = @id");
}

// Challenges (temporary storage for WebAuthn ceremonies)
export async function storeChallenge(
  id: string,
  challenge: string,
): Promise<void> {
  await ensureInitialized();
  const p = await getPool();
  const expiresAt = new Date(Date.now() + 5 * 60 * 1000).toISOString();
  await p
    .request()
    .input("id", sql.NVarChar, id)
    .input("challenge", sql.NVarChar, challenge)
    .input("expiresAt", sql.NVarChar, expiresAt)
    .query(
      `MERGE PasskeyChallenges AS target
       USING (SELECT @id AS Id) AS source ON target.Id = source.Id
       WHEN MATCHED THEN UPDATE SET Challenge = @challenge, ExpiresAt = @expiresAt
       WHEN NOT MATCHED THEN INSERT (Id, Challenge, ExpiresAt) VALUES (@id, @challenge, @expiresAt);`,
    );
}

export async function getChallenge(id: string): Promise<string | null> {
  await ensureInitialized();
  const p = await getPool();
  const result = await p
    .request()
    .input("id", sql.NVarChar, id)
    .query<{ Challenge: string; ExpiresAt: string }>(
      "SELECT Challenge, ExpiresAt FROM PasskeyChallenges WHERE Id = @id",
    );
  const row = result.recordset[0];
  if (!row) return null;

  // Clean up after reading
  await p
    .request()
    .input("id", sql.NVarChar, id)
    .query("DELETE FROM PasskeyChallenges WHERE Id = @id");

  if (new Date(row.ExpiresAt) < new Date()) return null;
  return row.Challenge;
}

// List all users (for discovery during login)
export async function getAllUsers(): Promise<PasskeyUser[]> {
  await ensureInitialized();
  const p = await getPool();
  const result = await p.request().query<{
    Id: string;
    DisplayName: string;
    LastTenant: string | null;
    CreatedAt: string;
  }>("SELECT Id, DisplayName, LastTenant, CreatedAt FROM PasskeyUsers");
  return result.recordset.map((row) => ({
    id: row.Id,
    displayName: row.DisplayName,
    lastTenant: row.LastTenant || undefined,
    createdAt: row.CreatedAt,
  }));
}

function mapCredential(row: {
  Id: string;
  CredentialId: string;
  UserId: string;
  PublicKey: string;
  Counter: number;
  Transports: string | null;
  CreatedAt: string;
}): PasskeyCredential {
  return {
    id: row.Id,
    credentialId: row.CredentialId,
    userId: row.UserId,
    publicKey: row.PublicKey,
    counter: row.Counter,
    transports: row.Transports ? JSON.parse(row.Transports) : undefined,
    createdAt: row.CreatedAt,
  };
}
