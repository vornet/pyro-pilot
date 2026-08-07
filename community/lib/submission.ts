export const allowedCategories = new Set([
  "Cake", "Shell", "Fountain", "Mine", "Roman candle", "Rocket", "Other",
]);

export type SubmissionInput = {
  manufacturer: string;
  productName: string;
  productCode: string | null;
  upc: string | null;
  category: string;
  durationMs: number;
  burstShape: string | null;
  description: string | null;
  sourceUrl: string;
  contributorName: string | null;
  contributorEmail: string | null;
  duplicateKey: string;
};

function optional(value: unknown, maxLength: number): string | null {
  if (typeof value !== "string") return null;
  const normalized = value.trim();
  return normalized ? normalized.slice(0, maxLength) : null;
}

function required(value: unknown, label: string, maxLength: number): string {
  const normalized = optional(value, maxLength);
  if (!normalized) throw new Error(`${label} is required.`);
  return normalized;
}

export function parseSubmission(value: unknown): SubmissionInput {
  if (!value || typeof value !== "object") throw new Error("Invalid submission.");
  const body = value as Record<string, unknown>;

  if (optional(body.website, 200)) throw new Error("Invalid submission.");
  if (body.attestation !== "accepted") throw new Error("Please confirm the accuracy statement.");

  const manufacturer = required(body.manufacturer, "Manufacturer", 100);
  const productName = required(body.productName, "Product name", 140);
  const category = required(body.category, "Category", 40);
  if (!allowedCategories.has(category)) throw new Error("Choose a valid category.");

  const durationSeconds = Number(body.durationSeconds);
  if (!Number.isFinite(durationSeconds) || durationSeconds < 0.1 || durationSeconds > 900) {
    throw new Error("Duration must be between 0.1 and 900 seconds.");
  }

  const sourceUrl = required(body.sourceUrl, "Product or retailer page", 500);
  let parsedUrl: URL;
  try { parsedUrl = new URL(sourceUrl); } catch { throw new Error("Enter a valid source URL."); }
  if (parsedUrl.protocol !== "https:" && parsedUrl.protocol !== "http:") {
    throw new Error("Source URL must begin with http:// or https://.");
  }

  const contributorEmail = optional(body.contributorEmail, 254);
  if (contributorEmail && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(contributorEmail)) {
    throw new Error("Enter a valid email address or leave it blank.");
  }

  const productCode = optional(body.productCode, 80);
  const duplicateKey = [manufacturer, productCode ?? productName]
    .map((part) => part.toLocaleLowerCase("en-US").replace(/[^a-z0-9]+/g, ""))
    .join(":");

  return {
    manufacturer,
    productName,
    productCode,
    upc: optional(body.upc, 32),
    category,
    durationMs: Math.round(durationSeconds * 1000),
    burstShape: optional(body.burstShape, 40),
    description: optional(body.description, 1200),
    sourceUrl: parsedUrl.toString(),
    contributorName: optional(body.contributorName, 100),
    contributorEmail,
    duplicateKey,
  };
}
