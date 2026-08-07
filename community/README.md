# Pyro Pilot Community Catalog

The public submission portal for Pyro Pilot's community-maintained firework catalog.

## Current slice

- A non-technical, mobile-friendly firework submission form.
- Server-side validation with a bot-trap field.
- Durable D1-backed pending submissions.
- Tracking IDs returned to contributors.
- No direct path from an anonymous submission to the approved catalog.

Submissions deliberately stop in `pending` status. Moderation and approved-catalog
publishing will be added as a separate authenticated workflow.

## Local development

Requires Node.js 22.13 or later.

```bash
npm install
npm run db:generate
npm run dev
```

The Cloudflare/Vinext development environment provides the local D1 binding declared
in `.openai/hosting.json`. Apply `drizzle/*.sql` to the local database before testing
form submission.

## Validation

```bash
npm test
npm run lint
```
