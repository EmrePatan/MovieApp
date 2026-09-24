# #60 LOAD60 identity provisioning (operator)

Operator-local tooling only. **No production HTTP admin endpoint.** No schema migration.

## Identity convention

| Field | Value |
|--------|--------|
| Email | `load60-001@loadtest.invalid` … `load60-050@loadtest.invalid` |
| Harness id | `load60-001` … `load60-050` |
| DisplayName | `LOAD60 #001` … `LOAD60 #050` |

`loadtest.invalid` is accepted by current `MailAddress` validation. Override with `--email-domain` if policy changes.

## JWT lifetime vs 50-VU capacity stage

- Access token lifetime: **60 minutes** (no refresh).
- Capacity preset duration: **~17 minutes** (3m + 12m + 2m).
- Serial mint of 50 identities at **13s** spacing: **~11 minutes** spread between first and last login.
- Preflight + report margin: **~5 minutes**.

**Recommended** `Test-LoadTokens.ps1 -MinMinutesUntilExpiry`: **30** at stage start (not 75).

Run `token-requirements` on the provisioner for the current calculated value.

## Connection strings

`LOAD_TEST_PG_CONNECTION` accepts **Npgsql key/value** strings or **`postgresql://` / `postgres://` URIs** (Render external DB). URIs default port **5432** when omitted and use **SSL Require**. Values are never logged.

## Provision (dry-run default)

```powershell
cd tests\load
$env:LOAD_TEST_PG_CONNECTION = "<from secret store — never commit>"
.\scripts\Invoke-LoadTestIdentityProvisioner.ps1 -Command provision -Count 50
```

Write (after approval):

```powershell
.\scripts\Invoke-LoadTestIdentityProvisioner.ps1 -Command provision -Count 50 -ConfirmProduction
```

Prompts once for campaign password (SecureString; not stored).

## Verify campaign password (read-only, no HTTP login)

Before minting JWTs, confirm the password matches what was used at provision time:

```powershell
$env:LOAD_TEST_PG_CONNECTION = "<from secret store>"
.\scripts\Invoke-LoadTestIdentityProvisioner.ps1 -Command verify-password -SampleSize 3
```

Exit code **2** + `CAMPAIGN PASSWORD MISMATCH` when `passwordMatch=false`.

## Mint tokens (out-of-band login)

Only after **verify-password** succeeds:

```powershell
$env:LOAD_TEST_BASE_URL = "https://movieapp-fpkg.onrender.com"
.\scripts\Mint-LoadTestTokens.ps1
```

- **One preflight login** on `load60-001`; stops on **401/403/429** before batch.
- **15s** delay between subsequent logins (production limit: **5/min/IP including failures**).
- After **5 failed** logins in a minute, wait **60s** before retrying HTTP login.
- Atomic write to `data/tokens.json` (UTF-8 no BOM).
- Fails if pool incomplete (no partial `tokens.json`).

## Validate before k6

```powershell
.\scripts\Test-LoadTokens.ps1 -SampleCount 50 -FailOnAuthError -MinMinutesUntilExpiry 30
```

## Cleanup (dry-run default)

```powershell
.\scripts\Invoke-LoadTestIdentityProvisioner.ps1 -Command cleanup
```

Delete (manifest + LOAD60 verification only):

```powershell
.\scripts\Invoke-LoadTestIdentityProvisioner.ps1 -Command cleanup -ConfirmDelete
```

User rows cascade per EF configuration. Redis recommendation caches may retain entries until TTL unless separately invalidated (#54).
