import { getDb } from "../../../db";
import { fireworkSubmissions } from "../../../db/schema";
import { parseSubmission } from "../../../lib/submission";

function errorMessage(error: unknown) {
  const message = error instanceof Error ? error.message : "Unexpected error";
  if (message.includes("no such table")) {
    return "The submission queue is being prepared. Please try again shortly.";
  }
  return message;
}

export async function POST(request: Request) {
  const contentLength = Number(request.headers.get("content-length") ?? 0);
  if (contentLength > 16_384) {
    return Response.json({ error: "Submission is too large." }, { status: 413 });
  }

  try {
    const raw = await request.json();
    const submission = parseSubmission(raw);
    const trackingId = `FW-${crypto.randomUUID().slice(0, 8).toUpperCase()}`;

    await getDb().insert(fireworkSubmissions).values({ trackingId, ...submission });

    return Response.json(
      { trackingId, status: "pending" },
      { status: 201, headers: { "cache-control": "no-store" } },
    );
  } catch (error) {
    const message = errorMessage(error);
    const clientError = error instanceof SyntaxError ||
      (error instanceof Error && !message.startsWith("The submission queue"));
    return Response.json(
      { error: message },
      { status: clientError ? 400 : 500, headers: { "cache-control": "no-store" } },
    );
  }
}
