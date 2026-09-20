# MovieApp — Production Email (Resend)

Movie Cave transactional email (password reset + email verification) is delivered through [Resend](https://resend.com) HTTP API.

---

## Application configuration

Set these once on Render (preferred shared section):

```bash
Authentication__Email__Resend__ApiKey=re_...
Authentication__Email__Resend__FromAddress=noreply@<your-verified-domain>
Authentication__Email__Resend__FromName=Movie Cave
Authentication__PasswordReset__EmailProvider=Resend
Authentication__PasswordReset__BaseUrl=movieapp://reset-password
Authentication__EmailVerification__BaseUrl=movieapp://verify-email
```

Flow-specific sections (`Authentication:PasswordReset:Resend`, `Authentication:EmailVerification:Resend`) may override the shared values. Hero image paths remain flow-specific.

**Production startup rules:**

- Password reset requires `EmailProvider=Resend`.
- Both flows require configured Resend `ApiKey` + `FromAddress` (shared or per-flow).
- `onboarding@resend.dev` and other `*@resend.dev` senders are rejected in Production.

---

## Delivery behavior

| Flow | Sender | Queue |
|------|--------|-------|
| Password reset | `ResendPasswordResetEmailSender` | Hangfire `PasswordResetDeliveryJob` (5 retries) |
| Email verification | `ResendVerificationEmailSender` | Hangfire `EmailVerificationDeliveryJob` (5 retries) |

**Send failures:** Resend HTTP errors are logged with status code and sanitized provider message. Delivery is not marked complete; Hangfire retries with stable idempotency keys.

**Bounces / complaints:** Not processed in-app. Monitor in the Resend dashboard. Async bounces do not roll back completed deliveries; invalid addresses may fail synchronously at send time (logged, retried).

---

## Operator checklist — custom domain (manual)

Complete in Resend Dashboard + your DNS provider. Do not guess record values — copy them from Resend after adding your domain.

1. **Resend → Domains → Add domain** — enter the domain you will send from (for example the domain in `noreply@...`).
2. **DNS records** — add the SPF, DKIM (and recommended DMARC) records Resend displays. Wait until Resend shows the domain as **Verified**.
3. **Sender address** — set `Authentication__Email__Resend__FromAddress` to an address on that verified domain (for example `noreply@<your-verified-domain>`).
4. **API key** — create a Production API key in Resend; set `Authentication__Email__Resend__ApiKey` on Render.
5. **Deploy** — Production startup fails if the sender is still `*@resend.dev` or Resend is unconfigured.
6. **Smoke test**
   - Register a new account → verification email arrives from the custom domain.
   - Forgot password on a password-backed account → reset email arrives from the custom domain.
7. **Monitor** — review Resend → Logs for bounces/complaints after launch.

---

## Render env summary

| Variable | Purpose |
|----------|---------|
| `Authentication__Email__Resend__ApiKey` | Resend API key |
| `Authentication__Email__Resend__FromAddress` | Verified custom-domain sender |
| `Authentication__Email__Resend__FromName` | Display name (`Movie Cave`) |
| `Authentication__PasswordReset__EmailProvider` | Must be `Resend` in Production |
| `Authentication__PasswordReset__BaseUrl` | Deep link base for reset |
| `Authentication__EmailVerification__BaseUrl` | Deep link base for verify |

See also `docs/PRODUCTION-LAUNCH-CHECKLIST.md`.
