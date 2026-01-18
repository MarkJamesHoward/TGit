// Azure Cosmos DB client and data layer
import { CosmosClient, Container, Database } from "@azure/cosmos";

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
  id: string; // Cosmos DB requires an id field (we'll use email)
  userName: string;
  userEmail: string;
  lastActivity: string;
  activities: Record<string, RepoActivity>; // keyed by repoName::machineName
}

// Activity expiry time (30 minutes of inactivity = user is idle)
const ACTIVITY_EXPIRY_MS = 30 * 60 * 1000;

// Cleanup threshold (7 days)
const CLEANUP_THRESHOLD_MS = 7 * 24 * 60 * 60 * 1000;

// Cosmos DB configuration from environment variables
const endpoint = import.meta.env.COSMOS_ENDPOINT || process.env.COSMOS_ENDPOINT;
const key = import.meta.env.COSMOS_KEY || process.env.COSMOS_KEY;
const databaseId = import.meta.env.COSMOS_DATABASE || process.env.COSMOS_DATABASE || "tgit";
const containerId = import.meta.env.COSMOS_CONTAINER || process.env.COSMOS_CONTAINER || "users";

let client: CosmosClient | null = null;
let container: Container | null = null;

// Check if Cosmos DB is configured
export function isCosmosConfigured(): boolean {
  return !!(endpoint && key);
}

// Initialize Cosmos DB client
async function getContainer(): Promise<Container> {
  if (container) return container;
  
  if (!endpoint || !key) {
    throw new Error("Cosmos DB not configured. Set COSMOS_ENDPOINT and COSMOS_KEY environment variables.");
  }
  
  client = new CosmosClient({ endpoint, key });
  
  // Create database if it doesn't exist
  const { database } = await client.databases.createIfNotExists({ id: databaseId });
  
  // Create container if it doesn't exist (partition key is /userEmail)
  const { container: cont } = await database.containers.createIfNotExists({
    id: containerId,
    partitionKey: { paths: ["/userEmail"] }
  });
  
  container = cont;
  return container;
}

export async function recordActivity(activity: GitActivity): Promise<void> {
  const cont = await getContainer();
  const id = activity.userEmail.toLowerCase();
  
  // Try to get existing user
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
        activities: {}
      };
    }
  } catch (error: any) {
    if (error.code === 404) {
      user = {
        id,
        userName: activity.userName,
        userEmail: activity.userEmail,
        lastActivity: activity.timestamp,
        activities: {}
      };
    } else {
      throw error;
    }
  }
  
  // Update user info
  user.userName = activity.userName;
  user.lastActivity = activity.timestamp;
  
  // Key by repo+machine so each machine's state is tracked separately
  const activityKey = `${activity.repoName}::${activity.machineName}`;
  
  // Completely replace the activity for this repo+machine combination
  user.activities[activityKey] = {
    repoName: activity.repoName,
    branch: activity.branch,
    remoteUrl: activity.remoteUrl,
    modifiedFiles: activity.modifiedFiles,
    lastUpdated: activity.timestamp,
    machineName: activity.machineName
  };
  
  // Upsert to Cosmos DB
  await cont.items.upsert(user);
}

export async function getAllUsers(): Promise<UserStatus[]> {
  const cont = await getContainer();
  
  const { resources } = await cont.items
    .query<UserStatus>("SELECT * FROM c ORDER BY c.lastActivity DESC")
    .fetchAll();
  
  return resources;
}

export async function getActiveUsers(): Promise<UserStatus[]> {
  const users = await getAllUsers();
  const now = Date.now();
  
  return users.filter(user => {
    const lastActivityTime = new Date(user.lastActivity).getTime();
    return (now - lastActivityTime) < ACTIVITY_EXPIRY_MS;
  });
}

export async function getUserByEmail(email: string): Promise<UserStatus | undefined> {
  const cont = await getContainer();
  const id = email.toLowerCase();
  
  try {
    const { resource } = await cont.item(id, id).read<UserStatus>();
    return resource;
  } catch (error: any) {
    if (error.code === 404) {
      return undefined;
    }
    throw error;
  }
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

export async function cleanupOldEntries(): Promise<void> {
  const cont = await getContainer();
  const now = Date.now();
  
  const { resources } = await cont.items
    .query<UserStatus>("SELECT * FROM c")
    .fetchAll();
  
  for (const user of resources) {
    const lastActivityTime = new Date(user.lastActivity).getTime();
    if ((now - lastActivityTime) > CLEANUP_THRESHOLD_MS) {
      await cont.item(user.id, user.userEmail.toLowerCase()).delete();
    }
  }
}
