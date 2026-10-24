# Shared-base Worker

A Cloudflare Worker that receives purchases from the app (`POST /submit`), validates them against the merchant catalog in this repository, and appends them to `library/purchases.json` with a commit.

The only secret is `GITHUB_TOKEN`: a fine-grained GitHub token limited to this repository with **Contents: Read and write**. It is stored in the Worker's settings, never in the code or the app.

## Deploy

1. In the Cloudflare dashboard, open **Workers & Pages → Create → Create Worker**, name it, and press **Deploy**.
2. Press **Edit code**, replace everything with `worker.js`, and press **Deploy**.
3. Open **Settings → Variables and Secrets → Add**. Choose type **Secret**, name `GITHUB_TOKEN`, paste the token, and deploy.
4. Put the Worker URL with `/submit` into `$WORKER` in `sync.ps1`.

## Limits

- At most 25 purchases per request and 32 KB per body.
- At most 12 requests per IP per hour.
- Entries need a known sound and item pair, and a price within ±28% of a merchant pool that holds the item.
- Item name and quality come from the catalog. Labels must be from a fixed list.
