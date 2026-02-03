import { promises as fs } from "node:fs";
import path from "node:path";

const dataDir = process.env.TENANT_DATA_DIR || "./data";
const fileName = "tenant-preferences.json";

let writeQueue: Promise<void> = Promise.resolve();

async function ensureDataDir(): Promise<void> {
  await fs.mkdir(dataDir, { recursive: true });
}

async function readAll(): Promise<Record<string, string>> {
  try {
    await ensureDataDir();
    const filePath = path.join(dataDir, fileName);
    const raw = await fs.readFile(filePath, "utf8");
    const parsed = JSON.parse(raw);
    if (parsed && typeof parsed === "object") {
      return parsed as Record<string, string>;
    }
  } catch (error: any) {
    if (error?.code !== "ENOENT") {
      console.warn("Failed to read tenant preferences:", error);
    }
  }
  return {};
}

async function writeAll(data: Record<string, string>): Promise<void> {
  await ensureDataDir();
  const filePath = path.join(dataDir, fileName);
  const tmpPath = `${filePath}.tmp`;
  const contents = JSON.stringify(data, null, 2);
  await fs.writeFile(tmpPath, contents, "utf8");
  await fs.rename(tmpPath, filePath);
}

export async function getLastTenantForUser(
  userId: string,
): Promise<string | null> {
  const data = await readAll();
  return data[userId] ?? null;
}

export async function setLastTenantForUser(
  userId: string,
  tenant: string,
): Promise<void> {
  writeQueue = writeQueue.then(async () => {
    const data = await readAll();
    data[userId] = tenant;
    await writeAll(data);
  });

  return writeQueue;
}
