// JSON file-based store for git activity data
// Each user's latest activity is stored and persisted to a JSON file

import { readFileSync, writeFileSync, existsSync, mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

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
  userName: string;
  userEmail: string;
  lastActivity: string;
  activities: Record<string, RepoActivity>; // keyed by repoName
}

interface StoreData {
  users: Record<string, UserStatus>; // keyed by email
}

// Data file path - stored in the data folder
const DATA_DIR = join(process.cwd(), 'data');
const DATA_FILE = join(DATA_DIR, 'users.json');

// Activity expiry time (30 minutes of inactivity = user is idle)
const ACTIVITY_EXPIRY_MS = 30 * 60 * 1000;

// Ensure data directory exists
function ensureDataDir(): void {
  if (!existsSync(DATA_DIR)) {
    mkdirSync(DATA_DIR, { recursive: true });
  }
}

// Load data from JSON file
function loadData(): StoreData {
  ensureDataDir();
  try {
    if (existsSync(DATA_FILE)) {
      const content = readFileSync(DATA_FILE, 'utf-8');
      return JSON.parse(content);
    }
  } catch (error) {
    console.error('Error loading data file:', error);
  }
  return { users: {} };
}

// Save data to JSON file
function saveData(data: StoreData): void {
  ensureDataDir();
  try {
    writeFileSync(DATA_FILE, JSON.stringify(data, null, 2), 'utf-8');
  } catch (error) {
    console.error('Error saving data file:', error);
  }
}

export function recordActivity(activity: GitActivity): void {
  const data = loadData();
  const key = activity.userEmail.toLowerCase();
  
  let user = data.users[key];
  if (!user) {
    user = {
      userName: activity.userName,
      userEmail: activity.userEmail,
      lastActivity: activity.timestamp,
      activities: {}
    };
    data.users[key] = user;
  }
  
  // Update user info
  user.userName = activity.userName;
  user.lastActivity = activity.timestamp;
  
  // Key by repo+machine so each machine's state is tracked separately
  const activityKey = `${activity.repoName}::${activity.machineName}`;
  
  // Completely replace the activity for this repo+machine combination
  // This ensures the file list is always the current state, not merged
  user.activities[activityKey] = {
    repoName: activity.repoName,
    branch: activity.branch,
    remoteUrl: activity.remoteUrl,
    modifiedFiles: activity.modifiedFiles,
    lastUpdated: activity.timestamp,
    machineName: activity.machineName
  };
  
  // Save to file
  saveData(data);
}

export function getAllUsers(): UserStatus[] {
  const data = loadData();
  const users = Object.values(data.users);
  
  // Sort by most recent activity
  return users.sort((a, b) => 
    new Date(b.lastActivity).getTime() - new Date(a.lastActivity).getTime()
  );
}

export function getActiveUsers(): UserStatus[] {
  const now = Date.now();
  return getAllUsers().filter(user => {
    const lastActivityTime = new Date(user.lastActivity).getTime();
    return (now - lastActivityTime) < ACTIVITY_EXPIRY_MS;
  });
}

export function getUserByEmail(email: string): UserStatus | undefined {
  const data = loadData();
  return data.users[email.toLowerCase()];
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

// Cleanup old entries (entries older than 7 days)
const CLEANUP_THRESHOLD_MS = 7 * 24 * 60 * 60 * 1000;

export function cleanupOldEntries(): void {
  const data = loadData();
  const now = Date.now();
  let changed = false;
  
  for (const [key, user] of Object.entries(data.users)) {
    const lastActivityTime = new Date(user.lastActivity).getTime();
    if ((now - lastActivityTime) > CLEANUP_THRESHOLD_MS) {
      delete data.users[key];
      changed = true;
    }
  }
  
  if (changed) {
    saveData(data);
  }
}

// Run cleanup every hour
if (typeof setInterval !== 'undefined') {
  setInterval(cleanupOldEntries, 60 * 60 * 1000);
}
