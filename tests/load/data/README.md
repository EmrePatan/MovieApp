# Load test data files

| File | Commit? | Purpose |
|------|---------|---------|
| `*.example.json` | Yes | Templates |
| `hot-content.json` | Yes (placeholder GUIDs only) | Local/dev validation |
| `hot-content.production.json` | **No** (gitignored pattern `*.production.json`) | Production HOT IDs |
| `varied-content.production.json` | **No** | Production VARIED IDs |
| `tokens.json` | **No** | JWTs |
| `search-terms.json` | Optional | Non-secret search terms |

Production operators should copy production files to gitignored names and set:

```powershell
$env:LOAD_TEST_DATA_DIR = "$PWD\data"
# Temporarily point harness at production file by copying to hot-content.json locally, or symlink.
```

Never commit JWTs or production-specific ID exports with identifiable user data.
