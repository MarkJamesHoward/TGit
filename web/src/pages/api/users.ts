import type { APIRoute } from "astro";
import {
  getAllUsers,
  getActiveUsers,
  isUserActive,
  type UserStatus,
  type RepoActivity,
} from "../../lib/storage";

interface SerializableRepoActivity {
  repoName: string;
  branch: string;
  remoteUrl?: string;
  modifiedFiles: { filePath: string; status: string; isStaged: boolean }[];
  lastUpdated: string;
  machineName: string;
}

interface SerializableUserStatus {
  userName: string;
  userEmail: string;
  lastActivity: string;
  isActive: boolean;
  activities: SerializableRepoActivity[];
}

function serializeUser(user: UserStatus): SerializableUserStatus {
  return {
    userName: user.userName,
    userEmail: user.userEmail,
    lastActivity: user.lastActivity,
    isActive: isUserActive(user),
    activities: Object.values(user.activities),
  };
}

export const GET: APIRoute = async ({ url }) => {
  const activeOnly = url.searchParams.get("active") === "true";
  const tenant = url.searchParams.get("tenant");

  // Require a tenant to be specified - don't allow viewing all users across tenants
  if (!tenant) {
    return new Response(
      JSON.stringify({
        error: "Tenant parameter is required",
        users: [],
        totalCount: 0,
        activeCount: 0,
      }),
      {
        status: 400,
        headers: {
          "Content-Type": "application/json",
          "Access-Control-Allow-Origin": "*",
        },
      },
    );
  }

  const users = activeOnly
    ? await getActiveUsers(tenant)
    : await getAllUsers(tenant);
  const serializedUsers = users.map(serializeUser);

  return new Response(
    JSON.stringify({
      users: serializedUsers,
      totalCount: serializedUsers.length,
      activeCount: serializedUsers.filter((u) => u.isActive).length,
    }),
    {
      status: 200,
      headers: {
        "Content-Type": "application/json",
        "Access-Control-Allow-Origin": "*",
      },
    },
  );
};
