// Storage abstraction layer - supports JSON files or Cosmos DB
import fs from 'node:fs/promises';
import path from 'node:path';

// Types
export interface FileEditInfo {
  filePath: string;
  status: string;
  isStaged: boolean;
}

export interface GitActivity {
  timestamp: string;
  userName: string;
  userEmail: string;
  repoName: string;
  branch: string;
  remoteUrl?: string;
  modifiedFiles: FileEditInfo[];
  machineName: string;
  tenant?: string;
}

export interface RepoActivity {
  repoName: string;
  branch: string;
  remoteUrl?: string;
  modifiedFiles: FileEditInfo[];
  lastUpdated: string;
  machineName: string;
}

export interface UserStatus {
  id: string;
  userName: string;
  userEmail: string;
  lastActivity: string;
  activities: Record<string, RepoActivity>;
  tenant: string;
}

// Activity expiry time (30 minutes of inactivity = user is idle)
const ACTIVITY_EXPIRY_MS = 30 * 60 * 1000;

// Storage type configuration
const storageType = (import.meta.env.STORAGE_TYPE || process.env.STORAGE_TYPE || 'cosmos').toLowerCase();

// ============================================
// JSON File Storage Implementation
// ============================================

const DATA_DIR = import.meta.env.DATA_DIR || process.env.DATA_DIR || './data';

async function ensureDataDir(): Promise<void> {
  try {
    await fs.mkdir(DATA_DIR, { recursive: true });
  } catch (e) {
    // Directory exists
  }
}

function getUsersFilePath(tenant: string): string {
  const safeTenant = tenant.replace(/[^a-zA-Z0-9-_]/g, '_');
  return path.join(DATA_DIR, `users-${safeTenant}.json`);
}

async function loadUsersFromFile(tenant: string): Promise<UserStatus[]> {
  await ensureDataDir();
  const filePath = getUsersFilePath(tenant);
  try {
    const data = await fs.readFile(filePath, 'utf-8');
    return JSON.parse(data);
  } catch (e) {
    return [];
  }
}

async function saveUsersToFile(tenant: string, users: UserStatus[]): Promise<void> {
  await ensureDataDir();
  const filePath = getUsersFilePath(tenant);
  await fs.writeFile(filePath, JSON.stringify(users, null, 2));
}

async function loadAllUsersFromFiles(): Promise<UserStatus[]> {
  await ensureDataDir();
  try {
    const files = await fs.readdir(DATA_DIR);
    const userFiles = files.filter(f => f.startsWith('users-') && f.endsWith('.json'));
    
    const allUsers: UserStatus[] = [];
    for (const file of userFiles) {
      try {
        const data = await fs.readFile(path.join(DATA_DIR, file), 'utf-8');
        const users: UserStatus[] = JSON.parse(data);
        allUsers.push(...users);
      } catch (e) {
        // Skip invalid files
      }
    }
    return allUsers;
  } catch (e) {
    return [];
  }
}

// JSON storage functions
async function recordActivityJson(activity: GitActivity): Promise<void> {
  const tenant = (activity.tenant || 'default').toLowerCase();
  const users = await loadUsersFromFile(tenant);
  const id = `${tenant}::${activity.userEmail.toLowerCase()}`;
  
  let user = users.find(u => u.id === id);
  
  if (!user) {
    user = {
      id,
      userName: activity.userName,
      userEmail: activity.userEmail,
      lastActivity: activity.timestamp,
      activities: {},
      tenant
    };
    users.push(user);
  }
  
  // Update user info
  user.userName = activity.userName;
  user.lastActivity = activity.timestamp;
  
  // Key by repo+machine
  const activityKey = `${activity.repoName}::${activity.machineName}`;
  user.activities[activityKey] = {
    repoName: activity.repoName,
    branch: activity.branch,
    remoteUrl: activity.remoteUrl,
    modifiedFiles: activity.modifiedFiles,
    lastUpdated: activity.timestamp,
    machineName: activity.machineName
  };
  
  await saveUsersToFile(tenant, users);
}

async function getAllUsersJson(tenant?: string): Promise<UserStatus[]> {
  if (tenant) {
    const users = await loadUsersFromFile(tenant.toLowerCase());
    return users.sort((a, b) => 
      new Date(b.lastActivity).getTime() - new Date(a.lastActivity).getTime()
    );
  }
  
  const allUsers = await loadAllUsersFromFiles();
  return allUsers.sort((a, b) => 
    new Date(b.lastActivity).getTime() - new Date(a.lastActivity).getTime()
  );
}

// ============================================
// Cosmos DB Storage Implementation
// ============================================

import { CosmosClient, Container } from "@azure/cosmos";

const endpoint = import.meta.env.COSMOS_ENDPOINT || process.env.COSMOS_ENDPOINT;
const key = import.meta.env.COSMOS_KEY || process.env.COSMOS_KEY;
const databaseId = import.meta.env.COSMOS_DATABASE || process.env.COSMOS_DATABASE || "tgit";
const containerId = import.meta.env.COSMOS_CONTAINER || process.env.COSMOS_CONTAINER || "users";

let client: CosmosClient | null = null;
let container: Container | null = null;

async function getContainer(): Promise<Container> {
  if (container) return container;
  
  if (!endpoint || !key) {
    throw new Error("Cosmos DB not configured. Set COSMOS_ENDPOINT and COSMOS_KEY environment variables.");
  }
  
  client = new CosmosClient({ endpoint, key });
  const { database } = await client.databases.createIfNotExists({ id: databaseId });
  const { container: cont } = await database.containers.createIfNotExists({
    id: containerId,
    partitionKey: { paths: ["/userEmail"] }
  });
  
  container = cont;
  return container;
}

async function recordActivityCosmos(activity: GitActivity): Promise<void> {
  const cont = await getContainer();
  const tenant = (activity.tenant || "default").toLowerCase();
  const id = `${tenant}::${activity.userEmail.toLowerCase()}`;
  
  let user: UserStatus;
  try {
    const { resource } = await cont.item(id, activity.userEmail.toLowerCase()).read<UserStatus>();
    if (resource) {
      user = resource;
    } else {
      user = {
        id,
        userName: activity.userName,
        userEmail: activity.userEmail,
        lastActivity: activity.timestamp,
        activities: {},
        tenant
      };
    }
  } catch (error: any) {
    if (error.code === 404) {
      user = {
        id,
        userName: activity.userName,
        userEmail: activity.userEmail,
        lastActivity: activity.timestamp,
        activities: {},
        tenant
      };
    } else {
      throw error;
    }
  }
  
  user.userName = activity.userName;
  user.lastActivity = activity.timestamp;
  
  const activityKey = `${activity.repoName}::${activity.machineName}`;
  user.activities[activityKey] = {
    repoName: activity.repoName,
    branch: activity.branch,
    remoteUrl: activity.remoteUrl,
    modifiedFiles: activity.modifiedFiles,
    lastUpdated: activity.timestamp,
    machineName: activity.machineName
  };
  
  await cont.items.upsert(user);
}

async function getAllUsersCosmos(tenant?: string): Promise<UserStatus[]> {
  const cont = await getContainer();
  
  let query = "SELECT * FROM c";
  const parameters: { name: string; value: string }[] = [];
  
  if (tenant) {
    query += " WHERE c.tenant = @tenant";
    parameters.push({ name: "@tenant", value: tenant.toLowerCase() });
  }
  
  query += " ORDER BY c.lastActivity DESC";
  
  const { resources } = await cont.items
    .query<UserStatus>({ query, parameters })
    .fetchAll();
  
  return resources;
}

// ============================================
// Public API - Routes to appropriate backend
// ============================================

export function getStorageType(): 'json' | 'cosmos' {
  return storageType === 'json' ? 'json' : 'cosmos';
}

export function isStorageConfigured(): boolean {
  if (storageType === 'json') {
    return true; // JSON is always available
  }
  return !!(endpoint && key);
}

export async function recordActivity(activity: GitActivity): Promise<void> {
  if (storageType === 'json') {
    return recordActivityJson(activity);
  }
  return recordActivityCosmos(activity);
}

export async function getAllUsers(tenant?: string): Promise<UserStatus[]> {
  if (storageType === 'json') {
    return getAllUsersJson(tenant);
  }
  return getAllUsersCosmos(tenant);
}

export async function getActiveUsers(tenant?: string): Promise<UserStatus[]> {
  const users = await getAllUsers(tenant);
  const now = Date.now();
  
  return users.filter(user => {
    const lastActivityTime = new Date(user.lastActivity).getTime();
    return (now - lastActivityTime) < ACTIVITY_EXPIRY_MS;
  });
}

export function isUserActive(user: UserStatus): boolean {
  const now = Date.now();
  const lastActivityTime = new Date(user.lastActivity).getTime();
  return (now - lastActivityTime) < ACTIVITY_EXPIRY_MS;
}

export function getTimeSinceActivity(timestamp: string): string {
  const now = Date.now();
  const activityTime = new Date(timestamp).getTime();
  const diffMs = now - activityTime;
  
  const minutes = Math.floor(diffMs / 60000);
  const hours = Math.floor(diffMs / 3600000);
  const days = Math.floor(diffMs / 86400000);
  
  if (minutes < 1) return "just now";
  if (minutes < 60) return `${minutes}m ago`;
  if (hours < 24) return `${hours}h ago`;
  return `${days}d ago`;
}

// For backward compatibility
export function isCosmosConfigured(): boolean {
  return isStorageConfigured();
}
